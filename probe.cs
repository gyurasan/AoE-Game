using System; using System.Linq;
var a=System.Reflection.Assembly.LoadFrom("");
void Dump(Type t){
  Console.WriteLine("== "+t.FullName);
  foreach(var p in t.GetProperties().Select(p=>p.Name).OrderBy(x=>x)) Console.WriteLine("  P "+p);
  foreach(var m in t.GetMethods().Select(m=>m.Name).Distinct().OrderBy(x=>x).Where(x=>x.Contains("ampler")||x.Contains("ilter")||x.Contains("Address"))) Console.WriteLine("  M "+m);
}
Dump(a.GetType("Microsoft.Xna.Framework.Graphics.SpriteBatch"));
Dump(a.GetType("Microsoft.Xna.Framework.Graphics.Texture2D"));
Dump(a.GetType("Microsoft.Xna.Framework.Graphics.Texture"));
Console.WriteLine("SamplerState? "+(a.GetType("Microsoft.Xna.Framework.Graphics.SamplerState")!=null));
