using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("Willow Lite for Fiddler Classic")]
[assembly: AssemblyDescription("Project-based HOST mapping and traffic filtering plugin for Fiddler Classic")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("FiddlerWillowLite")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]
[assembly: Guid("7b3d8f2a-5c1e-4a0b-9f6d-2e8a1c4b7d90")]
[assembly: AssemblyVersion("0.2.0.0")]
[assembly: AssemblyFileVersion("0.2.0.0")]

// Fiddler Classic 会拒绝加载声明了更高最低版本的扩展。
// 4.6.0.0 覆盖 Fiddler 4/5 的常用安装。
[assembly: Fiddler.RequiredVersion("4.6.0.0")]
