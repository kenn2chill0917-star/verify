using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FiddlerWillowLite
{
    /// <summary>批量解析的结果：成功规则、逐行错误、跳过与重复计数。</summary>
    public sealed class BulkParseResult
    {
        public readonly List<HostRule> Rules = new List<HostRule>();
        public readonly List<string> Errors = new List<string>();
        public int Skipped;
        public int Duplicates;
    }

    /// <summary>
    /// 把一段多行文本解析为 Host 规则。兼容常见写法：
    ///   127.0.0.1 api.example.com          （标准 hosts，IP 在前，可跟多个域名）
    ///   api.example.com 127.0.0.1:8080     （域名在前）
    ///   api.example.com -> 127.0.0.1       （箭头）
    ///   api.example.com=127.0.0.1          （等号）
    /// 以 # 或 // 开头的行视为注释，空行忽略。
    /// </summary>
    public static class HostRuleParser
    {
        private static readonly Regex DomainPattern = new Regex(
            @"^(\*\.)?[A-Za-z0-9]([A-Za-z0-9\-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9\-]*[A-Za-z0-9])?)*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex HostnamePattern = new Regex(
            @"^[A-Za-z0-9][A-Za-z0-9.\-]*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static BulkParseResult Parse(string text, IEnumerable<HostRule> existing)
        {
            var result = new BulkParseResult();
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existing != null)
            {
                foreach (var rule in existing)
                {
                    if (rule != null && !string.IsNullOrEmpty(rule.Domain))
                    {
                        known.Add(rule.Domain.Trim().TrimEnd('.'));
                    }
                }
            }

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

                string domain;
                string target;
                var separator = line.IndexOf("->", StringComparison.Ordinal);
                if (separator >= 0)
                {
                    domain = line.Substring(0, separator).Trim();
                    target = line.Substring(separator + 2).Trim();
                    if (!AddRule(result, known, domain, target, i + 1, line))
                    {
                        continue;
                    }
                    continue;
                }

                separator = line.IndexOf('=');
                if (separator >= 0)
                {
                    domain = line.Substring(0, separator).Trim();
                    target = line.Substring(separator + 1).Trim();
                    AddRule(result, known, domain, target, i + 1, line);
                    continue;
                }

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    result.Errors.Add(LineError(i + 1, "缺少域名或目标地址", line));
                    continue;
                }

                if (LooksLikeTarget(parts[0]))
                {
                    // hosts 风格：目标在前，后面可以跟多个域名。
                    target = parts[0];
                    for (var j = 1; j < parts.Length; j++)
                    {
                        AddRule(result, known, parts[j], target, i + 1, line);
                    }
                }
                else
                {
                    domain = parts[0];
                    target = parts[1];
                    AddRule(result, known, domain, target, i + 1, line);
                }
            }

            return result;
        }

        private static bool AddRule(BulkParseResult result, HashSet<string> known,
            string domain, string target, int lineNumber, string rawLine)
        {
            domain = (domain ?? string.Empty).Trim().TrimEnd('.');
            target = (target ?? string.Empty).Trim();

            if (!DomainPattern.IsMatch(domain))
            {
                result.Errors.Add(LineError(lineNumber, "域名格式无效", rawLine));
                return false;
            }
            if (!IsValidTarget(target))
            {
                result.Errors.Add(LineError(lineNumber, "目标地址格式无效（应为 IP[:端口]）", rawLine));
                return false;
            }
            if (known.Contains(domain))
            {
                result.Duplicates++;
                return false;
            }

            known.Add(domain);
            result.Rules.Add(new HostRule { Domain = domain, Target = target, Enabled = true });
            return true;
        }

        private static bool LooksLikeTarget(string token)
        {
            return IsValidTarget(token) && (token.IndexOf(':') >= 0 || IsNumericAddress(token));
        }

        private static bool IsValidTarget(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }
            // IPv6: [::1] 或 [::1]:8080
            if (token.StartsWith("[", StringComparison.Ordinal))
            {
                var close = token.IndexOf(']');
                if (close < 0)
                {
                    return false;
                }
                var rest = token.Substring(close + 1);
                return rest.Length == 0 || (rest.StartsWith(":", StringComparison.Ordinal)
                    && IsPort(rest.Substring(1)));
            }

            // 带端口：host:8080（数字 IP 或主机名均可）
            var colon = token.LastIndexOf(':');
            if (colon >= 0)
            {
                return IsPort(token.Substring(colon + 1))
                    && HostnamePattern.IsMatch(token.Substring(0, colon));
            }

            // 不带端口只接受数字 IP，避免把误写的域名当作目标。
            return IsNumericAddress(token);
        }

        private static bool IsPort(string value)
        {
            int port;
            return value.Length > 0 && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port)
                && port >= 1 && port <= 65535;
        }

        private static bool IsNumericAddress(string token)
        {
            var withoutPort = token;
            var colon = token.IndexOf(':');
            if (colon >= 0)
            {
                withoutPort = token.Substring(0, colon);
            }
            var octets = withoutPort.Split('.');
            if (octets.Length != 4)
            {
                return false;
            }
            foreach (var octet in octets)
            {
                int value;
                if (octet.Length == 0 || !int.TryParse(octet, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value > 255)
                {
                    return false;
                }
            }
            return true;
        }

        private static string LineError(int lineNumber, string reason, string rawLine)
        {
            return "第 " + lineNumber + " 行「" + rawLine + "」：" + reason;
        }
    }
}
