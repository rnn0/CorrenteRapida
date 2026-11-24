using System; using System.IO; using System.Text; using System.Collections.Generic; using System.Diagnostics;
namespace CorrenteRapida;
static class P { static void Main(string[] args) {
  if (args.Length>0) GemArt.EnergyMode=int.Parse(args[0]);
  if (args.Length>1) GemStyle.MegaraMode=int.Parse(args[1]);
  if (args.Length>2) GemStyle.LuzMode=int.Parse(args[2]);
  if (args.Length>3) GemStyle.GeloMode=int.Parse(args[3]);
  // sempre 3D
  var lc=Environment.GetEnvironmentVariable("LUZCOR"); if (lc!=null) GemArt.LuzColor=lc;
  var lkm=Environment.GetEnvironmentVariable("LKM"); if (lkm!=null) GemStyle.LuzKillMode=int.Parse(lkm);
  var slm=Environment.GetEnvironmentVariable("SLM"); if (slm!=null) GemStyle.StormLegend=int.Parse(slm);
  Directory.CreateDirectory("tex"); foreach (var f in Directory.GetFiles("tex")) File.Delete(f);
  var sw = Stopwatch.StartNew();
  var styles = new GemStyle[6];
  for (int k=0;k<6;k++){ var w0=Stopwatch.StartNew(); styles[k]=GemArt.Build(k); Console.WriteLine($"k{k} {w0.ElapsedMilliseconds} ms"); }
  Console.WriteLine($"gerado em {sw.ElapsedMilliseconds} ms");
  for (int k=0;k<6;k++) for (int i=0;i<styles[k].Tex.Count;i++){
    var p=styles[k].Tex[i]; var b=new byte[p.Length]; for(int q=0;q<p.Length;q++) b[q]=(byte)Math.Clamp((int)(p[q]*255f+0.5f),0,255);
    File.WriteAllBytes($"tex/{k}_{i}_{styles[k].Size[i]}.raw", b);}
  var sb=new StringBuilder(); var list=new List<Spr>();
  int max=0;
  int NF=int.Parse(Environment.GetEnvironmentVariable("NF")??"60");
  for (int f=0; f<NF; f++){ float t=f/20f+10f;
    for (int k=0;k<6;k++) for (int lv=0; lv<8; lv++) foreach (var e in new[]{0f,1f,2f}) {
      float ee = e<1.5f ? e : Math.Clamp(1.4f*(0.5f-0.5f*MathF.Cos((t-10f)*MathF.Tau/6f))-0.2f,0f,1f);
      styles[k].Frame(ee,t,lv,0f,list); max=Math.Max(max,list.Count);
      foreach (var s in list) sb.Append($"{f} {k} {lv} {e} {s.Tex} {s.X} {s.Y} {s.W} {s.H} {s.Rot} {s.A}\n");
    }}
  if (Environment.GetEnvironmentVariable("KILL")!=null){ sb.Clear(); var tl=new List<Spr>();
    for (int f=0; f<NF; f++){ float t=f/20f+10f; float t0=10.3f;
      for (int k=0;k<6;k++){
        styles[k].KillAge = t - (t0 + (Environment.GetEnvironmentVariable("VB")!=null?0.56f:0f) + styles[k].KillArriveT);
        if (styles[k].KillAge < 0) styles[k].KillAge = 99f;
        styles[k].Frame(1f,t,7,0f,list); foreach (var s in list) sb.Append($"{f} {k} 7 1 {s.Tex} {s.X} {s.Y} {s.W} {s.H} {s.Rot} {s.A}\n");
        tl.Clear();
        if (Environment.GetEnvironmentVariable("VB")!=null){
          float tb=t0+0.56f+styles[k].KillArriveT;
          styles[k].VBloodBlast(t-tb,t,0f,0f,1f,tl);
          for(int w=0;w<3;w++){ float tw=t0+w*0.28f; if(t>=tw) styles[k].KillTravel(t-tw,t,-3.2f+(w-1)*0.9f,-1.6f+(w==1?0.5f:-0.2f),0f,0f,1f,tl);} }
        else if (t>=t0) styles[k].KillTravel(t-t0,t,-3.2f,-1.6f,0f,0f,1f,tl);
        foreach (var s in tl) sb.Append($"{f} {k} 99 1 {s.Tex} {s.X} {s.Y} {s.W} {s.H} {s.Rot} {s.A}\n");
        styles[k].KillAge = 99f; }}
    File.WriteAllText("frames.txt", sb.ToString()); return; }
  Console.WriteLine($"max camadas {max}");
  var bw=Stopwatch.StartNew(); int cnt=0; for(int f=0;f<2000;f++){float t=f/60f; for(int k=0;k<6;k++){styles[k].Frame(k==5?1f:0f,t,7,0f,list); cnt+=list.Count;}} Console.WriteLine($"Frame: {bw.Elapsed.TotalMilliseconds/2000:0.000} ms por quadro (6 joias), {cnt/2000} camadas/quadro");
  File.WriteAllText("frames.txt", sb.ToString());
}}
