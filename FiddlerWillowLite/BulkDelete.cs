using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FiddlerWillowLite
{
    /// <summary>域名批量解析结果：有效域名、错误行、跳过与重复计数。</summary>
    public sealed class BulkDeleteParseResult
    {
        public readonly List<string> Domains = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public int Skipped;
        public int Duplicates;
    }

    /// <summary>批量删除的执行结果：已删除规则、未匹配域名、已删除项目。</summary>
    public sealed class BulkDeleteResult
    {
        public readonly List<HostRule> Removed = new List<HostRule>();
        public readonly List<string> NotFound = new List<string>();
        public readonly List<string> RemovedProjects = new List<string>();
    }

    /// <summary>
    /// 批量删除的解析与匹配逻辑。解析支持纯域名、hosts 格式（IP 在前）、
    /// 域名在前 + 目标、箭头/等号分隔；# 或 // 开头为注释。
    /// 匹配规则与 WillowPlugin 一致：精确匹配 + *. 前缀通配，不区分大小写。
    /// </summary>
    public static class BulkDelete
    {
        private static readonly Regex DomainPattern = new Regex(
            @"^(\*\.)?[A-Za-z0-9]([A-Za-z0-9\-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9\-]*[A-Za-z0-9])?)*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex IpPattern = new Regex(
            @"^\d{1,3}(\.\d{1,3}){3}(:\d+)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static BulkDeleteParseResult Parse(string text)
        {
            var result = new BulkDeleteParseResult();
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)
                    || line.StartsWith("//", StringComparison.Ordinal))
                {
                    result.Skipped++;
                    continue;
                }

                // 箭头/等号分隔：取左侧域名。
                var separator = line.IndexOf("->", StringComparison.Ordinal);
                if (separator >= 0)
                {
                    AddToken(result, known, line.Substring(0, separator).Trim(), i + 1, line);
                    continue;
                }
                separator = line.IndexOf('=');
                if (separator >= 0)
                {
                    AddToken(result, known, line.Substring(0, separator).Trim(), i + 1, line);
                    continue;
                }

                // 纯域名 / hosts 格式：按空白切分，跳过 IP 和目标端口，收集所有域名。
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var found = 0;
                foreach (var part in parts)
                {
                    var token = part.Trim().TrimEnd('.');
                    if (!IsDomain(token))
                    {
                        continue;
                    }
                    found++;
                    if (known.Add(token))
                    {
                        result.Domains.Add(token);
                    }
                    else
                    {
                        result.Duplicates++;
                    }
                }
                if (found == 0)
                {
                    result.Errors.Add("第 " + (i + 1) + " 行: " + line);
                }
            }
            return result;
        }

        private static void AddToken(BulkDeleteParseResult result, HashSet<string> known,
            string token, int lineNo, string line)
        {
            token = token.Trim().TrimEnd('.');
            if (!IsDomain(token))
            {
                result.Errors.Add("第 " + lineNo + " 行: " + line);
                return;
            }
            if (known.Add(token))
            {
                result.Domains.Add(token);
            }
            else
            {
                result.Duplicates++;
            }
        }

        private static bool IsDomain(string token)
        {
            return !string.IsNullOrEmpty(token)
                && !IpPattern.IsMatch(token)
                && DomainPattern.IsMatch(token);
        }

        /// <summary>域名匹配：*. 前缀通配，其余精确匹配，均不区分大小写。</summary>
        public static bool Matches(string domain, string pattern)
        {
            domain = (domain ?? "").Trim().TrimEnd('.');
            pattern = (pattern ?? "").Trim().TrimEnd('.');
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                return domain.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(domain, pattern, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 按域名批量删除规则。scope 为 null 时作用于全部项目，否则仅作用于该项目。
        /// </summary>
        public static BulkDeleteResult Remove(WillowConfig config, IEnumerable<string> domains, EnvProject scope)
        {
            var result = new BulkDeleteResult();
            if (config == null || domains == null)
            {
                return result;
            }

            foreach (var domain in domains)
            {
                var removed = 0;
                foreach (var project in config.Projects)
                {
                    if (scope != null && !ReferenceEquals(project, scope))
                    {
                        continue;
                    }
                    for (var i = project.Rules.Count - 1; i >= 0; i--)
                    {
                        var rule = project.Rules[i];
                        if (rule == null || !Matches(rule.Domain, domain))
                        {
                            continue;
                        }
                        result.Removed.Add(rule);
                        project.Rules.RemoveAt(i);
                        removed++;
                    }
                }
                if (removed == 0)
                {
                    result.NotFound.Add(domain);
                }
            }
            return result;
        }

        /// <summary>批量删除项目（按对象引用匹配，避免重名误删）。</summary>
        public static int RemoveProjects(WillowConfig config, IEnumerable<EnvProject> projects)
        {
            var count = 0;
            if (config == null || projects == null)
            {
                return 0;
            }
            foreach (var project in projects)
            {
                if (project != null && config.Projects.Remove(project))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
