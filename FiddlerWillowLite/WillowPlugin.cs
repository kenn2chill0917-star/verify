using System;
using System.Globalization;
using System.Windows.Forms;
using Fiddler;

namespace FiddlerWillowLite
{
    /// <summary>
    /// Fiddler Classic 扩展：以项目为单位管理 Host 映射，并在独立选项卡里展示抓包与过滤结果。
    /// </summary>
    public sealed class WillowPlugin : IAutoTamper
    {
        private readonly WillowConfig _config;
        private readonly object _sequenceLock = new object();
        private WillowView _view;
        private TabPage _tab;
        private int _sequence;

        public WillowPlugin()
        {
            WriteDebug("constructor enter");
            try
            {
                // IAutoTamper 的回调可能早于 OnLoad，因此配置必须在这里就绪。
                _config = RuleStore.Load();
                FiddlerApplication.AfterSessionComplete += OnAfterSessionComplete;
                WriteDebug("constructor ok");
            }
            catch (Exception ex)
            {
                WriteDebug("constructor failed: " + ex);
                throw;
            }
        }

        public void OnLoad()
        {
            WriteDebug("OnLoad enter");
            try
            {
                _view = new WillowView(_config);
                WriteDebug("view created");
                _view.ConfigChanged += OnConfigChanged;

                _tab = new TabPage("Willow Lite");
                WriteDebug("tab created");
                _tab.ImageIndex = (int)SessionIcons.Timeline;
                WriteDebug("image index set: " + _tab.ImageIndex);
                _view.Dock = DockStyle.Fill;
                _tab.Controls.Add(_view);
                WriteDebug("adding tab to tabsViews");
                FiddlerApplication.UI.tabsViews.TabPages.Add(_tab);
                WriteDebug("OnLoad ok, tab count=" + FiddlerApplication.UI.tabsViews.TabPages.Count);
            }
            catch (Exception ex)
            {
                WriteDebug("OnLoad failed: " + ex);
                try
                {
                    FiddlerApplication.Log.LogString("[WillowLite] UI 初始化失败: " + ex.Message);
                }
                catch
                {
                    // ignore logging failure
                }
            }
        }

        private static void WriteDebug(string message)
        {
            try
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FiddlerWillowLite-debug.log");
                System.IO.File.AppendAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + System.Diagnostics.Process.GetCurrentProcess().Id + "] " + message + Environment.NewLine);
            }
            catch
            {
                // diagnostics must never break the plugin
            }
        }

        public void OnBeforeUnload()
        {
            FiddlerApplication.AfterSessionComplete -= OnAfterSessionComplete;
            RuleStore.Save(_config);

            try
            {
                if (_tab != null && FiddlerApplication.UI != null && FiddlerApplication.UI.tabsViews != null)
                {
                    FiddlerApplication.UI.tabsViews.TabPages.Remove(_tab);
                }
            }
            catch
            {
                // Fiddler 退出过程中 UI 可能已经释放。
            }
        }

        public void AutoTamperRequestBefore(Session oSession)
        {
            try
            {
                ApplyHostRules(oSession);
            }
            catch (Exception ex)
            {
                FiddlerApplication.Log.LogString("[WillowLite] 规则匹配失败: " + ex.Message);
            }
        }

        public void AutoTamperRequestAfter(Session oSession)
        {
        }

        public void AutoTamperResponseBefore(Session oSession)
        {
        }

        public void AutoTamperResponseAfter(Session oSession)
        {
        }

        public void OnBeforeReturningError(Session oSession)
        {
        }

        private void OnConfigChanged()
        {
            RuleStore.Save(_config);
        }

        private void ApplyHostRules(Session oSession)
        {
            if (oSession == null || !_config.MappingEnabled)
            {
                return;
            }

            var host = oSession.hostname;
            if (string.IsNullOrEmpty(host))
            {
                return;
            }

            EnvProject matchedProject = null;
            HostRule matchedRule = null;

            // 按项目在树中的顺序匹配；第一个命中的启用项目生效，便于模拟 Willow 的自上而下覆盖。
            foreach (var project in _config.Projects)
            {
                if (project == null || !project.Enabled || project.Rules == null)
                {
                    continue;
                }

                foreach (var rule in project.Rules)
                {
                    if (rule == null || !rule.Enabled || string.IsNullOrEmpty(rule.Domain) || string.IsNullOrEmpty(rule.Target))
                    {
                        continue;
                    }

                    if (HostMatches(host, rule.Domain))
                    {
                        matchedProject = project;
                        matchedRule = rule;
                        break;
                    }
                }

                if (matchedRule != null)
                {
                    break;
                }
            }

            if (matchedRule == null)
            {
                return;
            }

            // x-overrideHost 只改变连接目标，不改写 Host 请求头；上游网关场景下需要绕过网关。
            oSession["x-overrideHost"] = NormalizeTarget(matchedRule.Target, oSession.port);
            oSession.bypassGateway = true;
            oSession["ui-backcolor"] = "LightBlue";
            oSession["ui-bold"] = "true";
            oSession["ui-customcolumn"] = "Willow:" + matchedProject.Name;
            oSession["willow.project"] = matchedProject.Name ?? string.Empty;
        }

        private static bool HostMatches(string host, string pattern)
        {
            host = host.Trim().TrimEnd('.');
            pattern = pattern.Trim().TrimEnd('.');

            // 支持 *.example.com 形式的通配符，必须带点号。
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                return host.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeTarget(string target, int defaultPort)
        {
            target = target.Trim();
            if (target.IndexOf(':') >= 0)
            {
                return target;
            }
            return target + ":" + defaultPort.ToString(CultureInfo.InvariantCulture);
        }

        private void OnAfterSessionComplete(Session oSession)
        {
            if (_view == null || oSession == null)
            {
                return;
            }

            try
            {
                var sequence = 0;
                lock (_sequenceLock)
                {
                    sequence = ++_sequence;
                }

                var project = oSession["willow.project"] ?? string.Empty;
                var item = new CaptureItem
                {
                    No = sequence,
                    Method = oSession.RequestMethod ?? string.Empty,
                    Host = oSession.hostname ?? string.Empty,
                    Path = oSession.PathAndQuery ?? string.Empty,
                    Status = oSession.responseCode > 0
                        ? oSession.responseCode.ToString(CultureInfo.InvariantCulture)
                        : "-",
                    Time = FormatDuration(oSession),
                    Project = project,
                    Matched = project.Length > 0,
                    Url = oSession.fullUrl ?? string.Empty
                };

                _view.Append(item);
            }
            catch
            {
                // 抓包快照失败不应影响 Fiddler 自身流程。
            }
        }

        private static string FormatDuration(Session oSession)
        {
            var timers = oSession.Timers;
            if (timers == null)
            {
                return "-";
            }

            var start = timers.ClientDoneRequest;
            var end = timers.ServerDoneResponse;
            if (end < start)
            {
                return "-";
            }

            return Math.Round((end - start).TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + " ms";
        }
    }
}
