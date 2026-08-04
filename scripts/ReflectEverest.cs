using System;
using System.Linq;
using System.Reflection;
class P {
  static void Main() {
    var asm = Assembly.LoadFrom(@"D:\SteamLibrary\steamapps\common\Celeste\Celeste.Mod.mm.dll");
    foreach (var typeName in new[]{"Celeste.Mod.Everest+Events+Level","Celeste.Mod.Everest+Events+MainMenu"}) {
      var t = asm.GetType(typeName);
      Console.WriteLine("TYPE " + typeName + " => " + t);
      if(t==null) continue;
      foreach(var e in t.GetEvents(BindingFlags.Public|BindingFlags.Static)) {
        var invoke=e.EventHandlerType.GetMethod("Invoke");
        Console.WriteLine($"EVENT {e.Name}: {e.EventHandlerType.FullName} ({string.Join(", ", invoke.GetParameters().Select(p => p.ParameterType.FullName+" "+p.Name))})");
      }
    }
  }
}
