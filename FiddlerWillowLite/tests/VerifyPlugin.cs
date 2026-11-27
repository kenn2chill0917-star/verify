using System; using System.Linq; using System.Reflection;
class V { static int Main(string[] a){
 var f=Assembly.LoadFrom(a[0]); var p=Assembly.LoadFrom(a[1]);
 var t=p.GetType("FiddlerWillowLite.WillowPlugin");
 Console.WriteLine("Plugin type: "+(t!=null?"FOUND":"MISSING"));
 var itf=t.GetInterfaces().Select(x=>x.FullName).ToArray();
 Console.WriteLine("Interfaces: "+string.Join(", ", itf));
 bool ok = itf.Contains("Fiddler.IAutoTamper");
 var req=p.GetCustomAttributes(false).Any(x=>x.GetType().FullName=="Fiddler.RequiredVersionAttribute");
 Console.WriteLine("RequiredVersion attr: "+req);
 Console.WriteLine("RESULT: "+(ok&&req?"PASS":"FAIL"));
 return ok&&req?0:1; } }