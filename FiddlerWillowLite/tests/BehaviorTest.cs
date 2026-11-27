// Runtime behavior test against the shim: verifies Host mapping logic end to end.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Fiddler;
using FiddlerWillowLite;

internal static class BehaviorTest
{
    private static int _failures;

    private static void Check(bool ok, string name)
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name);
        if (!ok) _failures++;
    }

    [STAThread]
    private static int Main()
    {
        // 保存并临时隔离磁盘配置，测试结束后还原，避免污染真实使用环境。
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FiddlerWillowLite", "config.json");
        var backup = File.Exists(configPath) ? File.ReadAllText(configPath) : null;

        var plugin = new WillowPlugin();

        var field = typeof(WillowPlugin).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance);
        var config = (WillowConfig)field.GetValue(plugin);
        config.MappingEnabled = true;
        config.Projects.Clear();
        config.Projects.Add(new EnvProject
        {
            Name = "Dev",
            Enabled = true,
            Rules = new List<HostRule>
            {
                new HostRule { Domain = "api.example.com", Target = "127.0.0.1:8080", Enabled = true },
                new HostRule { Domain = "*.test.local", Target = "10.0.0.5", Enabled = true },
                new HostRule { Domain = "off.example.com", Target = "1.2.3.4", Enabled = false }
            }
        });

        var hit = new Session { hostname = "api.example.com", port = 443, fullUrl = "https://api.example.com/x" };
        plugin.AutoTamperRequestBefore(hit);
        Check(hit["x-overrideHost"] == "127.0.0.1:8080", "exact domain mapping writes x-overrideHost");
        Check(hit.bypassGateway, "matched session sets bypassGateway");
        Check(hit["ui-customcolumn"] == "Willow:Dev", "session list custom column marks project");

        var wildcard = new Session { hostname = "foo.test.local", port = 80 };
        plugin.AutoTamperRequestBefore(wildcard);
        Check(wildcard["x-overrideHost"] == "10.0.0.5:80", "wildcard mapping with port completion");

        var disabled = new Session { hostname = "off.example.com", port = 80 };
        plugin.AutoTamperRequestBefore(disabled);
        Check(disabled["x-overrideHost"] == null, "disabled rule is ignored");

        var other = new Session { hostname = "other.com", port = 80 };
        plugin.AutoTamperRequestBefore(other);
        Check(other["x-overrideHost"] == null, "unmatched host is untouched");

        config.MappingEnabled = false;
        var switchedOff = new Session { hostname = "api.example.com", port = 80 };
        plugin.AutoTamperRequestBefore(switchedOff);
        Check(switchedOff["x-overrideHost"] == null, "master switch disables mapping");

        plugin.OnBeforeUnload();

        // 还原用户真实配置（OnBeforeUnload 会把测试配置写盘）。
        if (backup != null)
        {
            File.WriteAllText(configPath, backup);
        }
        else if (File.Exists(configPath))
        {
            File.Delete(configPath);
        }

        // 批量解析：标准 hosts 格式、域名在前、箭头/等号分隔、注释与多域名。
        var existing = new List<HostRule>
        {
            new HostRule { Domain = "skip.example.com", Target = "1.1.1.1", Enabled = true }
        };
        var bulk = HostRuleParser.Parse(
            "# 注释行\n"
            + "127.0.0.1  api.example.com  web.example.com\n"
            + "dev.example.com 10.0.0.5:8080\n"
            + "stage.example.com -> 10.0.0.6\n"
            + "prod.example.com=10.0.0.7\n"
            + "// 另一条注释\n"
            + "skip.example.com 2.2.2.2\n", existing);
        Check(bulk.Rules.Count == 5, "bulk parse: five valid rules");
        Check(bulk.Rules[0].Domain == "api.example.com" && bulk.Rules[0].Target == "127.0.0.1",
            "bulk parse: hosts format IP first");
        Check(bulk.Rules[1].Domain == "web.example.com", "bulk parse: multiple domains per line");
        Check(bulk.Rules[2].Target == "10.0.0.5:8080", "bulk parse: domain first with port");
        Check(bulk.Rules[3].Domain == "stage.example.com" && bulk.Rules[3].Target == "10.0.0.6",
            "bulk parse: arrow separator");
        Check(bulk.Rules[4].Domain == "prod.example.com", "bulk parse: equals separator");
        Check(bulk.Skipped == 3, "bulk parse: comments and blank lines skipped");
        Check(bulk.Duplicates == 1, "bulk parse: duplicate domain skipped");

        var invalid = HostRuleParser.Parse("bad domain 1.2.3.4\nexample.com not-an-ip\n", null);
        Check(invalid.Errors.Count == 2 && invalid.Rules.Count == 0, "bulk parse: invalid lines reported");

        // 批量删除：域名提取（纯域名 / hosts 格式 / 箭头 / 等号 / 注释 / 去重）。
        var delParse = BulkDelete.Parse(
            "# 注释\n"
            + "api.example.com\n"
            + "127.0.0.1  web.example.com  cdn.example.com\n"
            + "stage.example.com -> 10.0.0.6\n"
            + "prod.example.com=10.0.0.7\n"
            + "*.test.local\n"
            + "api.example.com\n"
            + "10.0.0.1 1.2.3.4\n");
        Check(delParse.Domains.Count == 6, "bulk delete: six unique domains parsed");
        Check(delParse.Domains[0] == "api.example.com" && delParse.Domains[3] == "stage.example.com",
            "bulk delete: plain / arrow / equals domains");
        Check(delParse.Domains[1] == "web.example.com" && delParse.Domains[2] == "cdn.example.com",
            "bulk delete: hosts format multiple domains");
        Check(delParse.Duplicates == 1, "bulk delete: duplicate domain skipped");
        Check(delParse.Skipped == 2 && delParse.Errors.Count == 1, "bulk delete: comment/blank skipped and IP-only line reported invalid");
        Check(BulkDelete.Matches("foo.test.local", "*.test.local")
            && !BulkDelete.Matches("foo.other.com", "*.test.local"), "bulk delete: wildcard match");

        // 批量删除执行：作用域（全部项目 / 当前项目）与仅删除命中规则。
        var dev = new EnvProject
        {
            Name = "Dev",
            Enabled = true,
            Rules = new List<HostRule>
            {
                new HostRule { Domain = "api.example.com", Target = "127.0.0.1:8080", Enabled = true },
                new HostRule { Domain = "foo.test.local", Target = "10.0.0.5", Enabled = true },
                new HostRule { Domain = "keep.example.com", Target = "1.2.3.4", Enabled = true }
            }
        };
        var prod = new EnvProject
        {
            Name = "Prod",
            Enabled = false,
            Rules = new List<HostRule>
            {
                new HostRule { Domain = "api.example.com", Target = "192.168.1.9", Enabled = true }
            }
        };
        var delConfig = new WillowConfig { MappingEnabled = true, Projects = new List<EnvProject> { dev, prod } };
        var scoped = BulkDelete.Remove(delConfig, new[] { "api.example.com" }, dev);
        Check(scoped.Removed.Count == 1 && dev.Rules.Count == 2 && prod.Rules.Count == 1,
            "bulk delete: scoped removal affects only current project");

        var all = BulkDelete.Remove(delConfig, new[] { "api.example.com", "*.test.local" }, null);
        Check(all.Removed.Count == 2 && dev.Rules.Count == 1 && prod.Rules.Count == 0,
            "bulk delete: all-project removal with wildcard");
        Check(all.NotFound.Count == 0 && delConfig.Projects[0].Rules[0].Domain == "keep.example.com",
            "bulk delete: unmatched rules kept");

        var missing = BulkDelete.Remove(delConfig, new[] { "no-such.example.com" }, null);
        Check(missing.NotFound.Count == 1 && missing.Removed.Count == 0, "bulk delete: missing domain reported");

        var removedProjects = BulkDelete.RemoveProjects(delConfig, new[] { dev, prod });
        Check(removedProjects == 2 && delConfig.Projects.Count == 0, "bulk delete: multiple projects removed");

        Console.WriteLine();
        Console.WriteLine("FAILURES: " + _failures);
        return _failures == 0 ? 0 : 1;
    }
}
