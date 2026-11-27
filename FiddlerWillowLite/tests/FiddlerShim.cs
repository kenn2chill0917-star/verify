// 本地编译验证用的 shim：模拟 Fiddler.exe 暴露给插件的类型/接口。
// 只验证语法与引用，最终以真实 Fiddler.exe 为准。
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Fiddler
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class RequiredVersionAttribute : Attribute
    {
        public RequiredVersionAttribute(string version) { }
    }

    public interface IFiddlerExtension
    {
        void OnLoad();
        void OnBeforeUnload();
    }

    public interface IAutoTamper : IFiddlerExtension
    {
        void AutoTamperRequestBefore(Session oSession);
        void AutoTamperRequestAfter(Session oSession);
        void AutoTamperResponseBefore(Session oSession);
        void AutoTamperResponseAfter(Session oSession);
        void OnBeforeReturningError(Session oSession);
    }

    public enum SessionIcons { Timeline = 0 }

    public sealed class SessionTimers
    {
        public DateTime ClientDoneRequest;
        public DateTime ServerDoneResponse;
    }

    public sealed class Session
    {
        private readonly Dictionary<string, string> _flags =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string hostname { get; set; }
        public int port { get; set; }
        public string fullUrl { get; set; }
        public string PathAndQuery { get; set; }
        public string RequestMethod { get; set; }
        public int responseCode { get; set; }
        public bool bypassGateway { get; set; }
        public SessionTimers Timers { get; set; }
        public string this[string key]
        {
            get
            {
                string value;
                return _flags.TryGetValue(key, out value) ? value : null;
            }
            set
            {
                if (value == null) _flags.Remove(key);
                else _flags[key] = value;
            }
        }
    }

    public sealed class FiddlerLog
    {
        public void LogString(string text) { }
    }

    public static class FiddlerApplication
    {
        public static FiddlerLog Log = new FiddlerLog();
        public static event Action<Session> AfterSessionComplete;
        public static FiddlerUI UI = new FiddlerUI();
    }

    public sealed class FiddlerUI
    {
        public TabControl tabsViews = new TabControl();
    }
}
