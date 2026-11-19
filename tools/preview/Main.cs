using System; using System.IO; using System.Text; using System.Collections.Generic; using System.Diagnostics;
namespace CorrenteRapida;
static class P { static void Main() {
  Directory.CreateDirectory("tex"); foreach (var f in Directory.GetFiles("tex")) File.Delete(f);
  var sw = Stopwatch.StartNew();
  var styles = new GemStyle[6];
  for (int k=0;k<6;k++) styles[k]=GemArt.Build(k);
  Console.WriteLine($"gerado em {sw.ElapsedMilliseconds} ms");
  for (int k=0;k<6;k++) for (int i=0;i<styles[k].Tex.Count;i++){
    var p=styles[k].Tex[i]; var b=new byte[p.Length]; for(int q=0;q<p.Length;q++) b[q]=(byte)Math.Clamp((int)(p[q]*255f+0.5f),0,255);
    File.WriteAllBytes($"tex/{k}_{i}_{styles[k].Size[i]}.raw", b);}
  var sb=new StringBuilder(); var list=new List<Spr>();
  int max=0;
  for (int f=0; f<60; f++){ float t=f/20f+10f;
    for (int k=0;k<6;k++) for (int lv=0; lv<4; lv++) foreach (var e in new[]{0f,1f}) {
      styles[k].Frame(e,t,lv,0f,list); max=Math.Max(max,list.Count);
      foreach (var s in list) sb.Append($"{f} {k} {lv} {e} {s.Tex} {s.X} {s.Y} {s.W} {s.H} {s.Rot} {s.A}\n");
    }}
  Console.WriteLine($"max camadas {max}");
  File.WriteAllText("frames.txt", sb.ToString());
}}
