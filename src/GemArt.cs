using System;
using System.Collections.Generic;

namespace CorrenteRapida;

/// <summary>Uma camada a desenhar. Unidades = tamanho interno do botão; (0,0) = centro, y para cima.</summary>
internal struct Spr
{
    public int Tex;
    public float X, Y, W, H, Rot, A;
    public Spr(int tex, float x, float y, float w, float h, float rot, float a)
    { Tex = tex; X = x; Y = y; W = w; H = h; Rot = rot; A = a < 0f ? 0f : a > 1f ? 1f : a; }
}

/// <summary>Texturas (RGBA float, linhas de baixo para cima) e animação de um elemento.</summary>
internal sealed class GemStyle
{
    public readonly List<float[]> Tex = new();
    public readonly List<int> Size = new();
    public int Ball, Hole, Shadow, Gem;
    public readonly int Kind;
    public GemStyle(int kind) { Kind = kind; }

    public int Add(int size, float[] px) { Tex.Add(px); Size.Add(size); return Tex.Count - 1; }

    // Índices das texturas de aura (preenchidos por GemArt.Build).
    public int Halo = -1, A1 = -1, A2 = -1, A3 = -1, A4 = -1, Particle = -1;

    // Evolução por tempo de uso.
    public int Gem2, RingBronze, RingSilver, RingGold, GemGlow, Shard, Progress;
    public int M1 = -1, M2 = -1, M3 = -1, M4 = -1, M5 = -1, M6 = -1, M7 = -1;   // motivos do nível Lendário
    internal static int LuzMode = 6;   // 1 prisma, 2 mandala, 3 esferas, 4 coroa solar, 5 rosácea, 6 fusão (coroa + mandala + esferas)
    internal static int MegaraMode = 2;
    internal static int GeloMode = 6;   // 5 runa vampírica, 6 vampiro dormindo no bloco de gelo   // 0 estrela congela/quebra, 1 coração congelado, 3 tempestade de inverno, 4 prisão de gelo   // 1 ouroboros, 2 tendrils de simbionte
    public int Cracks, EnergyA, EnergyB; // rachaduras e energia no buraco da joia
    public int Veins = -1;               // veios brilhando na superfície da bola
    public int InnerA = -1, InnerB = -1;  // poder obscuro girando dentro da bola (Sangue)
    public int LinkFace = -1, LinkEdge = -1, Collar = -1;   // correntes e gola de vampiro (Sangue)
    public int[] BatFrames = Array.Empty<int>();             // morcego batendo as asas (3 quadros)
    public int[] Liquid = Array.Empty<int>();                // sangue líquido dentro da gota (inclinações da superfície)
    public int GlassTop = -1, HotFace = -1, HotEdge = -1, Spark = -1, VeinA = -1, VeinB = -1, Eyes = -1;
    public int Sclera = -1, Iris = -1, Pupil = -1, MiniEye = -1, Venom = -1;  // olho vivo da Corrompida
    public int[] Lids = Array.Empty<int>();                  // pálpebras de escamas (0 = aberta .. fechada)
    public int IceSpike = -1, SnowDot = -1, Flake = -1, FrostCoat = -1, IceGlowA = -1, IceGlowB = -1,
               IceShell = -1, Mist = -1, FrostRing = -1, Glacier = -1, AuroraA = -1, AuroraB = -1;   // Gelo
    public int VampSharp = -1, JawTop = -1, JawBottom = -1;
    public int SkullJaw = -1, SkullHorns = -1, Shock = -1, Smoke = -1, Mane = -1, WingL = -1, WingR = -1, Feather = -1;
    public int[] EyeFlames = Array.Empty<int>();
    public int[] Filaments = Array.Empty<int>(), BoltVeins = Array.Empty<int>();
    public int PlasmaCore = -1;
    public int BoltCore = -1, CoilBand = -1, CoilGlow = -1, Tesla = -1, ERing = -1;
    public int[] EdgeSparks = Array.Empty<int>(), Lightning = Array.Empty<int>(), CoilRings = Array.Empty<int>();

    /// <summary>Tempestade: carga elétrica acumulando (0..1) e descarga (strike 0..1, -1 = nenhuma).</summary>
    static (float charge, float strike, int cyc) Storm(float t, float e)
    {
        float u = t / 2.4f; int cyc = (int)MathF.Floor(u); u -= cyc;
        float c = u < 0.84f ? MathF.Pow(u / 0.84f, 1.6f) : 1f - (u - 0.84f) / 0.16f;
        float st = u >= 0.84f ? (u - 0.84f) / 0.16f : -1f;
        return (c * e, e > 0.05f ? st : -1f, cyc);
    }
    public int Rift = -1, ChaosCircle = -1, LinkRune = -1;
    int _lv;                       // nível do quadro atual (para as peças que evoluem)
    float _esc;                    // quanto a bola rúnica escapou neste quadro
    public int OrbSwirlA = -1, OrbSwirlB = -1, OrbRune = -1, MLinkFace = -1, MLinkEdge = -1, Plasma = -1;
    public int[] OrbGlyphs = Array.Empty<int>();
    public int FlameInA = -1, FlameInB = -1, ChaosEyes = -1, GarEyes = -1, MouthIn = -1, FireBreath = -1;

    /// <summary>
    /// Bola rúnica presa por correntes de magia: tensão, uma corrente arrebenta e a bola escapa um pouco,
    /// a corrente dispara de volta, gruda e puxa a bola para o lugar.
    /// </summary>
    static (float tension, float escape, int broken, float reattach, float flash) OrbFight(float t, float e)
    {
        if (e < 0.05f) return (0f, 0f, -1, 0f, 0f);
        float u = t / 3.0f; int cyc = (int)MathF.Floor(u); u -= cyc;
        int b = ((cyc % 3) + 3) % 3;
        if (u < 0.45f) return (u / 0.45f * e, 0f, -1, 0f, 0f);
        if (u < 0.75f) { float q = (u - 0.45f) / 0.30f; return (0.3f * e, MathF.Sin(q * MathF.PI / 2f) * e, b, 0f, u < 0.50f ? 1f - (u - 0.45f) / 0.05f : 0f); }
        if (u < 0.85f) { float r = (u - 0.75f) / 0.10f; return (0.5f * e, e, b, r, r > 0.85f ? (r - 0.85f) / 0.15f : 0f); }
        float p = (u - 0.85f) / 0.15f;
        float back = 1f - p; back = back * back - 0.15f * MathF.Sin(p * MathF.PI);   // volta com um tranco
        return (0.6f * (1f - p) * e, back * e, -1, 0f, p < 0.3f ? 1f - p / 0.3f : 0f);
    }

    /// <summary>Combustão do Caos: carga 0..1 crescendo e explosão (burst 0..1, -1 = nenhuma).</summary>
    static (float charge, float burst, int cyc) Combust(float t, float e)
    {
        float u = t / 2.6f; int cyc = (int)MathF.Floor(u); u -= cyc;
        float c = u < 0.82f ? Sq(u / 0.82f) : 1f - (u - 0.82f) / 0.18f;
        float b = u >= 0.82f ? (u - 0.82f) / 0.18f : -1f;
        return (c * e, e > 0.05f ? b : -1f, cyc);
    }
    public int Armor = -1, StormA = -1, StormB = -1, Wind = -1, Blade = -1, Block = -1, BlockFrost = -1, Cube = -1;
    public int[] Glyphs = Array.Empty<int>(); public int RuneCircle = -1;
    public int[] LakeCracks = Array.Empty<int>(), BlockCracks = Array.Empty<int>(), Tornado = Array.Empty<int>();
    internal const float LiquidStep = 7.5f;
    static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    /// <summary>Meia-largura máxima (em unidades da joia) ativa na Lendária e encaixada, medida percorrendo a animação.</summary>
    public float ExtActive, ExtIdle;

    internal void MeasureExtents()
    {
        var l = new List<Spr>();
        float Ext(float e)
        {
            float m = 0.5f;
            for (int f = 0; f < 120; f++)
            {
                Frame(e, 10f + f * 0.05f, Lendaria, 0f, l);
                foreach (var sp in l)
                {
                    if (sp.A < 0.08f) continue;
                    float r = sp.Rot * MathF.PI / 180f;
                    float hw = MathF.Abs(sp.W) * 0.5f * MathF.Abs(MathF.Cos(r)) + MathF.Abs(sp.H) * 0.5f * MathF.Abs(MathF.Sin(r));
                    m = MathF.Max(m, MathF.Abs(sp.X) + hw);
                }
            }
            return m;
        }
        ExtActive = Ext(1f);
        ExtIdle = Ext(0f);
    }

    /// <summary>
    /// Ciclo do gelo: cristais crescem e congelam a estrela, estalam e explodem, e voltam a crescer.
    /// grow 0..1 (tamanho dos cristais), crack 0..1 (tremor/estalo), burst 0..1 (estilhaços voando, -1 = nenhum).
    /// </summary>
    static (float grow, float crack, float burst, int cyc) IceCycle(float t, float e)
    {
        float u = t / 2.8f; int cyc = (int)MathF.Floor(u); u -= cyc;
        float g, cr = 0f, b = -1f;
        if (u < 0.70f) { float q = u / 0.70f; g = q * q * (3f - 2f * q); }
        else if (u < 0.78f) { g = 1f; cr = (u - 0.70f) / 0.08f; }
        else { g = 0f; b = (u - 0.78f) / 0.22f; }
        return (0.22f * (1f - e) + g * e, cr * e, e > 0.05f ? b : -1f, cyc);
    }                           // graus entre quadros do líquido

    /// <summary>Batimento de coração (tum-tum): 0..1. Acelera quando a joia está ativa.</summary>
    static float Beat(float t, float bpm)
    {
        float p = t * bpm / 60f; p -= MathF.Floor(p);
        return MathF.Exp(-Sq((p - 0.04f) / 0.045f)) + 0.65f * MathF.Exp(-Sq((p - 0.22f) / 0.05f));
    }
    /// <summary>Respiração lenta (0..1), mais rápida com a joia ativa.</summary>
    static float Breath(float t, float e)
    {
        float a = 0.5f - 0.5f * MathF.Cos(t * MathF.Tau / 4.2f), b = 0.5f - 0.5f * MathF.Cos(t * MathF.Tau / 2.4f);
        return a * (1f - e) + b * e;
    }
    /// <summary>Coração congelado do vampiro: batida bem lenta (hibernação), acelera ao acordar.</summary>
    static float FrozenHeart(float t, float e) => Beat(t, 34f) * (1f - e) + Beat(t, 74f) * e;
    static float Heart(float t, float e) => Beat(t, 58f) * (1f - e) + Beat(t, 108f) * e;
    public int[] Shine = Array.Empty<int>(); // brilho atravessando a joia (quadros)
    public int Glint = -1;                   // cintilação nas pontas
    public float[] TipX = Array.Empty<float>(), TipY = Array.Empty<float>(); // pontas da joia (coords da textura)
    public bool AlwaysAnimate => Shine.Length > 0;

    public const int Bruta = 0, Lapidada = 1, Bronzea = 2, Prateada = 3, Dourada = 4, Lustrosa = 5, Radiante = 6, Lendaria = 7;

    /// <summary>
    /// Monta as camadas do quadro. e = 0 encaixada .. 1 solta/ativa; t = segundos;
    /// level = nível de evolução; flash = 1..0 logo depois de subir de nível.
    /// </summary>
    public void Frame(float e, float t, int level, float flash, List<Spr> o)
    {
        o.Clear();
        _lv = level;
        float lift = 0.30f * e, s = 1f + 0.06f * e;
        float gx = 0f, gy = lift;            // centro da joia
        float gemRot = 0f, strain = 0f;
        if (Kind == GemArt.Sangue && e > 0.001f)
        {
            // A gota tenta fugir: puxa para cima, treme e se debate; as correntes a seguram.
            float pull = 0.85f + 0.15f * MathF.Sin(t * 6.1f) + 0.06f * MathF.Max(0f, MathF.Sin(t * 2.3f));
            gx = e * (0.035f * MathF.Sin(t * 23f) + 0.022f * MathF.Sin(t * 37f + 1f));
            gy = lift * pull + e * 0.025f * MathF.Sin(t * 29f);
            gemRot = e * (7f * MathF.Sin(t * 19f) + 4f * MathF.Sin(t * 31f + 2f));
            strain = e * Math.Clamp((pull - 0.88f) / 0.12f, 0f, 1f);
        }
        if (Kind == GemArt.Caos)
        {
            // Investida: a caveira avança (cresce) no instante da explosão.
            var (_, bu, _) = Combust(t, e);
            if (bu >= 0f) s *= 1f + 0.10f * Sq(1f - bu);
        }
        float beat = Kind == GemArt.Sangue ? Heart(t, e) : 0f;
        if (Kind == GemArt.Caos) beat = Combust(t, e).charge;
        if (Kind == GemArt.Tempestade && BoltCore >= 0)
        {
            var (sc, ss, _) = Storm(t, e);
            beat = Math.Min(1f, 0.4f * sc + (ss >= 0f ? 1f - ss : 0f));
            gx += sc * sc * 0.02f * MathF.Sin(t * 47f);                       // o raio treme carregando
        }
        float breath = Kind == GemArt.Corrompida ? Breath(t, e) : 0f;
        if (Kind == GemArt.Corrompida) beat = breath;       // a respiração faz o papel do batimento
        if (Kind == GemArt.Gelo && GeloMode == 6) beat = FrozenHeart(t, e);
        _esc = 0f;
        var fight = Kind == GemArt.Caos && OrbRune >= 0 ? OrbFight(t, e) : (0f, 0f, -1, 0f, 0f);
        if (Kind == GemArt.Caos && OrbRune >= 0 && e > 0.001f)
        {
            float ten = fight.Item1, esc = fight.Item2;
            int bk = fight.Item3 >= 0 ? fight.Item3 : 0;
            float side = bk == 0 ? 0.6f : bk == 2 ? -0.6f : 0f;      // foge para o lado oposto da corrente arrebentada
            float lvK = 0.65f + 0.09f * level;                         // mais evoluída, fuga mais violenta
            esc *= lvK;
            gx = e * (0.012f + 0.03f * ten) * MathF.Sin(t * 31f) * lvK + esc * side * 0.12f;
            gy = lift + esc * 0.14f + e * 0.01f * MathF.Sin(t * 23f) * ten;
            _esc = esc;
        }
        float cy = lift * 0.5f;              // centro das auras
        float ea = level >= Lustrosa ? Math.Min(1f, e * 1.3f) : e; // aura mais intensa a partir da Lustrosa
        bool on = e > 0.001f;

        if (on && level >= Lendaria) LegendBack(e, t, gx, gy, cy, o);
        if (on) AuraBack(ea, t, gx, gy, cy, o);
        if (on && level >= Lendaria && Kind == GemArt.Gelo) GeloLegendMid(e, t, cy, o);
        if (level >= Lendaria && Kind == GemArt.Gelo && GeloMode == 6) IceWings(e, t, gx, gy, cy, true, o);
        // Chifres do Sangue Lendário: por cima da escuridão e atrás da bola, para ficarem sólidos.
        if (on && level >= Lendaria && Kind == GemArt.Sangue)
        {
            // Gola alta de capa de vampiro (preta por fora, forro vermelho) abrindo atrás da bola.
            float flutter = 1f + 0.035f * MathF.Sin(t * 2.2f);
            // Olhos vermelhos se abrindo na escuridão, acima da gola (abrem, olham, piscam, somem).
            {
                float u = t / 7.5f; u -= MathF.Floor(u);
                float open = u < 0.12f ? 0f : u < 0.20f ? (u - 0.12f) / 0.08f : u < 0.62f ? 1f : u < 0.70f ? 1f - (u - 0.62f) / 0.08f : 0f;
                float blink = MathF.Abs(u - 0.41f) < 0.012f ? 0.15f : 1f;
                if (open > 0f) o.Add(new Spr(Eyes, 0, cy + 0.98f, 0.70f, 0.24f * open * blink, 0, e * open));
            }
            // Morcegos saindo de trás da gola (desenhados antes dela, então nascem escondidos).
            for (int k = 0; k < 8; k++)
            {
                float side = (k & 1) == 0 ? -1f : 1f;
                float u = t / 2.6f + k / 8f * 2f; u -= MathF.Floor(u);
                float px = side * (0.50f + 0.95f * u + 0.10f * MathF.Sin(u * 7f + k));
                float py = cy + 0.30f + 1.05f * u - 0.35f * u * u + 0.05f * MathF.Sin(t * 5f + k);
                float sz = (0.16f + 0.10f * Hash(k * 17 + 3)) * (0.7f + 0.5f * u);
                float a = u > 0.75f ? (1f - u) / 0.25f : 1f;
                o.Add(new Spr(BatFrame(t, k), px, py, sz * 1.4f, sz, side * -12f, e * a));
            }
            o.Add(new Spr(Collar, 0, cy - 0.05f, 2.0f * flutter, 1.9f, 0, e));
            o.Add(new Spr(M1, 0, cy + 0.02f, 1.25f, 1.25f, 0, e));      // chifres menores
        }
        o.Add(new Spr(Ball, 0, 0, 1, 1, 0, 1));
        if (Filaments.Length > 0) PlasmaGlobe(e, t, o);
        if (Veins >= 0)
        {
            // Veias de corrupção na superfície: se espalham (aparecem mais) com a Corrente ativa.
            float va = Kind == GemArt.Corrompida ? (0.30f + 0.55f * e) * (0.6f + 0.4f * breath) : Kind == GemArt.Gelo ? 0.45f + 0.45f * beat : Kind == GemArt.Caos ? 0.35f + 0.25f * MathF.Sin(t * 1.3f) + 0.4f * beat : 0.75f + 0.25f * MathF.Sin(t * 1.6f);
            float vs = Kind == GemArt.Corrompida ? 0.92f + 0.08f * e : Kind == GemArt.Gelo ? 1f + 0.015f * beat : 1f;
            o.Add(new Spr(Veins, 0, 0, vs, vs, 0, va));
        }
        if (InnerA >= 0)
        {
            // Poder obscuro: duas camadas girando em sentidos opostos e oscilando de intensidade.
            float osc = 0.55f + 0.45f * MathF.Sin(t * 1.9f) * MathF.Sin(t * 0.7f + 1f);
            float hb = 0.75f + 0.25f * beat;
            o.Add(new Spr(InnerA, 0, 0, 1f + 0.03f * beat, 1f + 0.03f * beat, t * 23f, (0.4f + 0.4f * osc + 0.25f * beat) * hb));
            o.Add(new Spr(InnerB, 0, 0, 1, 1, -t * 31f, (0.3f + 0.55f * (1f - osc) + 0.2f * beat) * hb));
        }
        if (Kind == GemArt.Gelo && GeloMode == 6) o.Add(new Spr(IceShell, 0, 0, 1, 1, 0, 0.8f));
        if (Kind == GemArt.Gelo && GeloMode == 1)
        {
            // Lago congelado: rachaduras se espalham a cada batida e congelam de novo.
            float bp = t * (40f + 26f * e) / 60f; int bn = (int)MathF.Floor(bp); float bf = bp - bn;
            int stage = bn % 5;
            o.Add(new Spr(IceShell, 0, 0, 1, 1, 0, 1f));
            if (stage < 4) o.Add(new Spr(LakeCracks[stage], 0, 0, 1, 1, 0, 0.55f + 0.45f * e));
            else o.Add(new Spr(LakeCracks[3], 0, 0, 1, 1, 0, (0.55f + 0.45f * e) * (1f - bf)));
        }
        else if (Kind == GemArt.Gelo && GeloMode == 3)
        {
            // Olho do furacão de neve visto de cima: nuvens em espiral, mais rápidas quando ativa.
            o.Add(new Spr(StormA, 0, 0, 1, 1, -t * (18f + 70f * e), 1f));
            o.Add(new Spr(StormB, 0, 0, 1, 1, -t * (31f + 110f * e) + 40f, 0.8f));
            o.Add(new Spr(IceShell, 0, 0, 1, 1, 0, 0.7f));
        }
        else if (SnowDot >= 0)
        {
            // Globo de neve: flocos girando e caindo dentro da bola, sob a casca de gelo.
            for (int k = 0; k < 14; k++)
            {
                float h1 = Hash(k * 7 + 1), h2 = Hash(k * 13 + 5);
                float ang = t * (0.6f + 0.8f * h1) * (1f + 1.5f * e) + k * 2.4f;
                float r = 0.06f + 0.30f * h2;
                float fy = 0.36f - Frac(t * (0.07f + 0.05f * h1) * (1f + e) + h2) * 0.72f;
                float fx = MathF.Cos(ang) * r;
                if (fx * fx + fy * fy > 0.40f * 0.40f) continue;
                float depth = 0.5f + 0.5f * MathF.Sin(ang);
                float sz = (0.025f + 0.025f * Hash(k * 3)) * (0.7f + 0.5f * depth);
                o.Add(new Spr(SnowDot, fx, fy, sz, sz, 0, 0.45f + 0.5f * depth));
            }
            o.Add(new Spr(IceShell, 0, 0, 1, 1, 0, 1f));
        }
        if (level == Bronzea) o.Add(new Spr(RingBronze, 0, 0, 1, 1, 0, 1));
        else if (level == Prateada) o.Add(new Spr(RingSilver, 0, 0, 1, 1, 0, 1));
        else if (level >= Dourada) o.Add(new Spr(RingGold, 0, 0, 1, 1, 0, 1));
        if (on)
        {
            o.Add(new Spr(Hole, 0, 0, 1, 1, 0, e));
            o.Add(new Spr(Shadow, 0.05f * e, lift - 0.07f * e, s, s, 0, 0.4f * e));
            // Energia da cor da joia ondulando dentro do buraco (duas texturas se alternando).
            float w = 0.5f + 0.5f * MathF.Sin(t * 2.1f);
            float pulseE = 0.88f + 0.12f * MathF.Sin(t * 3.3f);
            o.Add(new Spr(EnergyA, 0, 0, 1, 1, 0, e * pulseE));          // sempre acesa
            o.Add(new Spr(EnergyB, 0, 0, 1, 1, 0, e * w * 0.85f));      // ondulação por cima
            // Rachaduras saindo do buraco, brilho pulsando.
            if (VeinA >= 0)
            {
                // Veias no fundo do buraco, pulsando com o coração.
                o.Add(new Spr(VeinA, 0, 0, 1, 1, 0, e * 0.9f));
                o.Add(new Spr(VeinB, 0, 0, 1, 1, 0, e * beat));
            }
            else o.Add(new Spr(Cracks, 0, 0, 1, 1, 0, e * (0.7f + 0.3f * MathF.Sin(t * 4.1f + 1f))));
        }
        if (LinkFace >= 0) Chains(e, t, gx, gy, s, gemRot, strain, o);
        if (MLinkFace >= 0) MagicChains(e, t, gx, gy, s, fight, o);
        if (Iris >= 0) LiveEye(e, t, gx, gy, s, breath, o);
        if (GarEyes >= 0) Gargoyle(e, t, gx, gy, s, on && level >= Lendaria, o, false);
        o.Add(new Spr(level >= Lapidada ? Gem2 : Gem, gx, gy, s, s, gemRot, 1));
        if (IceGlowA >= 0) TrueIce(e, t, gx, gy, s, o, on && level >= Lendaria);
        if (GarEyes >= 0) Gargoyle(e, t, gx, gy, s, on && level >= Lendaria, o, true);
        if (FlameInA >= 0) ChaosCrystal(e, t, gx, gy, s, o);
        if (OrbRune >= 0) RuneOrb(e, t, gx, gy, s, fight, o);
        if (BoltCore >= 0) LiveBolt(e, t, gx, gy, s, on && level >= Lendaria, cy, o);
        if (level >= Lendaria && Kind == GemArt.Gelo && GeloMode == 6) IceWings(e, t, gx, gy, cy, false, o);
        if (Liquid.Length > 0)
        {
            // O sangue fica nivelado com o "chão": compensa a rotação da gota e balança atrasado.
            float slosh = 4f * MathF.Sin(t * 1.3f) + e * 14f * MathF.Sin(t * 8f - 0.9f) - gemRot * 0.6f;
            float tilt = -gemRot + slosh;
            int li = (int)MathF.Round(tilt / LiquidStep) + Liquid.Length / 2;
            li = Math.Clamp(li, 0, Liquid.Length - 1);
            o.Add(new Spr(Liquid[li], gx, gy, s, s, gemRot, 1f));
            o.Add(new Spr(GemGlow, gx, gy, s * 1.05f, s * 1.05f, 0, 0.12f + 0.38f * beat));   // coração batendo dentro
            o.Add(new Spr(GlassTop, gx, gy, s, s, gemRot, 1f));
        }
        if (Shine.Length > 0)
        {
            // Brilho reluzente em loop: uma faixa de luz cruza o cristal e depois descansa.
            float u = t / 2.6f; u -= MathF.Floor(u);
            if (u < 0.35f)
            {
                int fi = Math.Min(Shine.Length - 1, (int)(u / 0.35f * Shine.Length));
                o.Add(new Spr(Shine[fi], gx, gy, s, s, 0, 0.9f));
            }
            // Cintilação numa das pontas, trocando a cada ciclo.
            int cyc = (int)MathF.Floor(t / 1.3f);
            float v = t / 1.3f - cyc;
            float tw = MathF.Sin(MathF.PI * Math.Min(1f, v / 0.5f));
            if (v < 0.5f && Glint >= 0 && TipX.Length > 0)
            {
                int tipI = (int)(Hash(cyc * 31 + Kind) * TipX.Length) % TipX.Length;
                float gsz = 0.30f * tw;
                o.Add(new Spr(Glint, gx + TipX[tipI] * 0.5f * s, gy + TipY[tipI] * 0.5f * s, gsz, gsz, 0, tw));
            }
        }
        if (level >= Radiante)   // luz interna pulsando, mesmo encaixada
            o.Add(new Spr(GemGlow, gx, gy, s * 1.1f, s * 1.1f, 0, 0.30f + 0.20f * MathF.Sin(t * 2.2f) + 0.2f * e));
        if (on) AuraFront(ea, t, level, o);
        if (on && level >= Lendaria) LegendFront(e, t, cy, o);
        if (on && level >= Radiante) Shards(e, t, cy, o);
        if (flash > 0.001f)
        {
            float g = 1f + (1f - flash) * 1.2f;
            o.Add(new Spr(Halo, 0, cy, 2.4f * g, 2.4f * g, 0, flash));
            o.Add(new Spr(GemGlow, gx, gy, s * (1f + (1f - flash) * 0.6f), s * (1f + (1f - flash) * 0.6f), 0, flash));
        }
    }

    /// <summary>
    /// Três correntes prendendo a joia à bola (ancoradas na borda de baixo).
    /// Encaixada: ficam frouxas, cruzando a joia. Solta: esticam enquanto ela tenta fugir.
    /// </summary>
    void Chains(float e, float t, float gx, float gy, float s, float rot, float strain, List<Spr> o)
    {
        // Pontos de fixação na borda da bola: alto à esquerda, direita e baixo à esquerda.
        float[] anchors = { 138f, 2f, 229f };
        float[] attX = { -0.17f, 0.19f, -0.06f }, attY = { 0.02f, -0.12f, -0.32f };   // lado esquerdo, lado direito e base da gota
        float cr = MathF.Cos(rot * MathF.PI / 180f), sr = MathF.Sin(rot * MathF.PI / 180f);
        for (int c = 0; c < 3; c++)
        {
            float a = anchors[c] * MathF.PI / 180f;
            float ax = MathF.Cos(a) * 0.42f, ay = MathF.Sin(a) * 0.42f;
            float px = attX[c] * s, py = attY[c] * s;
            float bx = gx + px * cr - py * sr, by = gy + px * sr + py * cr;
            float dx = bx - ax, dy = by - ay;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.02f) continue;
            float slack = (1f - e) * 0.04f + 0.012f * MathF.Sin(t * 4f + c);   // frouxa encaixada, esticada solta
            int n = Math.Max(3, (int)(len / 0.058f));
            float ang = MathF.Atan2(dy, dx) * 180f / MathF.PI;
            o.Add(new Spr(LinkFace, ax, ay, 0.13f, 0.13f, 0, 1f));      // argola presa na bola
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n;
                float sag = slack * 4f * u * (1f - u);
                float x = ax + dx * u, y = ay + dy * u - sag;
                // Cada elo alterna: de frente (oval) e de lado (barra).
                bool face = (i & 1) == 0;
                float lw = face ? 0.125f : 0.110f, lh = face ? 0.088f : 0.044f;
                o.Add(new Spr(face ? LinkFace : LinkEdge, x, y, lw, lh, ang, 1f));
                // Tensão: os elos esquentam (mais perto da gota, mais quentes).
                if (strain > 0.01f) o.Add(new Spr(face ? HotFace : HotEdge, x, y, lw * 1.15f, lh * 1.25f, ang, strain * (0.45f + 0.55f * u)));
            }
            // Faíscas saindo da argola quando a corrente está no limite.
            if (strain > 0.25f)
                for (int k = 0; k < 3; k++)
                {
                    float u2 = t * 3.2f + k / 3f + c * 0.29f; int cyc = (int)MathF.Floor(u2); float p = u2 - cyc;
                    float sa = a + (Hash(cyc * 13 + k * 7 + c) - 0.5f) * 2.2f;
                    float dist = 0.05f + 0.22f * p;
                    float sx = ax + MathF.Cos(sa) * dist, sy = ay + MathF.Sin(sa) * dist - 0.10f * p * p;
                    o.Add(new Spr(Spark, sx, sy, 0.09f * (1f - p), 0.035f, sa * 180f / MathF.PI, strain * (1f - p)));
                }
        }
    }

    /// <summary>
    /// Corrompida: tentáculos erguendo o olho (atrás) e o olho vivo: esclera, íris girando,
    /// pupila em fenda que olha em volta, pálpebras de escamas. A moldura (Gem) vem por cima.
    /// </summary>
    void LiveEye(float e, float t, float gx, float gy, float s, float breath, List<Spr> o)
    {
        if (e > 0.001f)
        {
            int[] tex = { M4, M5, M6 };
            // Garra de tentáculos: dois descem até o fundo do buraco, dois abraçam as laterais do olho.
            float[] angs = { 150f, -150f, 72f, -72f };
            float[] lens = { 0.42f, 0.42f, 0.66f, 0.66f };
            for (int k = 0; k < 4; k++)
            {
                float baseAng = angs[k] + 7f * MathF.Sin(t * 2.1f + k * 1.9f) + 4f * MathF.Sin(t * 4.3f + k);
                float len = lens[k] * e * (1f + 0.07f * MathF.Sin(t * 3f + k));
                float ang = baseAng * MathF.PI / 180f;
                float dx = -MathF.Sin(ang), dy = MathF.Cos(ang);
                float bx = gx, by = gy - 0.06f;
                o.Add(new Spr(tex[k % 3], bx + dx * len * 0.5f, by + dy * len * 0.5f, len * 0.85f, len, baseAng, e));
            }
        }
        // Para onde o olho olha: alvo novo a cada ~1,7 s, deslizando suave.
        float seg = t / 1.7f; int si = (int)MathF.Floor(seg); float f = seg - si;
        float sm = f < 0.22f ? Sq(f / 0.22f) * (3f - 2f * f / 0.22f) : 1f;
        float lx0 = (Hash(si * 7 + 1) - 0.5f) * 0.11f, ly0 = (Hash(si * 11 + 4) - 0.5f) * 0.03f;
        float lx1 = (Hash(si * 7 + 8) - 0.5f) * 0.11f, ly1 = (Hash(si * 11 + 11) - 0.5f) * 0.03f;
        float lx = (lx0 + (lx1 - lx0) * sm) * s, ly = (ly0 + (ly1 - ly0) * sm) * s;
        o.Add(new Spr(Sclera, gx, gy, s, s, 0, 1f));
        float isz = 0.33f * s * (1f + 0.04f * breath);
        o.Add(new Spr(Iris, gx + lx, gy + ly, isz, isz, t * 14f, 1f));
        // Pupila: arredondada em repouso, fenda fininha ativa (prestes a dar o bote).
        float pw = (0.085f - 0.055f * e) * (1f - 0.25f * breath) * s;
        o.Add(new Spr(Pupil, gx + lx * 1.15f, gy + ly * 1.15f, pw, 0.27f * s, 0, 1f));
        // Pálpebras: meio fechadas (sonolentas) em repouso, arregaladas ativa; piscada a cada ~5 s.
        float lid = 0.55f * (1f - e);
        float bu = t / 5.3f; bu -= MathF.Floor(bu);
        if (bu < 0.05f) lid = Math.Max(lid, MathF.Sin(bu / 0.05f * MathF.PI));
        int li = (int)MathF.Round(lid * (Lids.Length - 1));
        if (li > 0) o.Add(new Spr(Lids[li], gx, gy, s, s, 0, 1f));
    }

    /// <summary>
    /// Gelo: brilho azul oscilando dentro do gelo escuro, geada fosca quando encaixada
    /// e cristais congelando a estrela, que trincam e explodem em ciclo quando ativa.
    /// </summary>
    void TrueIce(float e, float t, float gx, float gy, float s, List<Spr> o, bool inCube = false)
    {
        if (GeloMode == 6)
        {
            // Coração congelado: a cada batida o vulto fica nítido, o gelo estala e brilha.
            // Com a Corrente ativa o coração acelera e os olhos se abrem no pico da batida.
            float hb = FrozenHeart(t, e);
            if (e < 0.999f) o.Add(new Spr(IceGlowB, gx, gy, s, s, 0, 0.3f * (1f - e)));
            o.Add(new Spr(VampSharp, gx, gy, s, s, 0, 0.55f * Math.Min(1f, hb)));
            float open = e * Math.Min(1f, hb * 1.3f);
            o.Add(new Spr(IceGlowA, gx, gy, s, s, 0, open));
            float cr = e * (0.3f + 0.7f * Math.Min(1f, hb));
            int ci = Math.Min(BlockCracks.Length - 1, (int)(cr * BlockCracks.Length));
            if (cr > 0.05f) o.Add(new Spr(BlockCracks[ci], gx, gy, s, s, 0, Math.Min(1f, cr * 1.3f)));
            o.Add(new Spr(GemGlow, gx, gy, s * (1.06f + 0.05f * hb), s * (1.06f + 0.05f * hb), 0, 0.10f + 0.30f * Math.Min(1f, hb) * (0.4f + 0.6f * e)));
            // Cacos de gelo na base do bloco, dos dois lados.
            float[] sxs = { -0.27f, -0.20f, 0.22f, 0.29f }, sh = { 0.20f, 0.13f, 0.15f, 0.22f }, sr = { 18f, -6f, 8f, -16f };
            for (int k = 0; k < 4; k++)
                o.Add(new Spr(Particle, gx + sxs[k] * s, gy - 0.34f * s + sh[k] * 0.4f * s, sh[k] * 0.45f * s, sh[k] * s, sr[k], 0.95f));
            return;
        }
        if (GeloMode == 5)
        {
            // Runa acesa: pulsa fraca em repouso, forte e viva com a Corrente ativa.
            float pr = 0.5f + 0.5f * MathF.Sin(t * 2.4f) * MathF.Sin(t * 0.8f + 0.5f);
            if (e < 0.999f) o.Add(new Spr(IceGlowB, gx, gy, s, s, 0, 0.55f * (1f - e)));     // geada leve encaixada
            o.Add(new Spr(IceGlowA, gx, gy, s, s, 0, Math.Clamp(0.40f + 0.25f * pr + e * (0.45f + 0.1f * pr), 0f, 1f)));
            if (e > 0.01f) o.Add(new Spr(IceGlowA, gx, gy, s, s, 0, e * (0.3f + 0.4f * pr)));   // segunda passada: brilho forte
            o.Add(new Spr(GemGlow, gx, gy, s * 1.08f, s * 1.08f, 0, e * (0.20f + 0.25f * pr)));
            return;
        }
        if (GeloMode == 1)
        {
            // Coração congelado: o brilho azul bate devagar dentro do gelo escuro.
            float b = Beat(t, 40f + 26f * e);
            o.Add(new Spr(IceGlowA, gx, gy, s, s, 0, Math.Clamp(0.25f + 0.65f * b + 0.1f * e, 0f, 1f)));
            o.Add(new Spr(IceGlowB, gx, gy, s, s, 0, Math.Clamp(0.15f + 0.3f * MathF.Sin(t * 1.1f) * MathF.Sin(t * 0.6f) + 0.4f * b, 0f, 1f)));
            o.Add(new Spr(GemGlow, gx, gy, s * (1.05f + 0.08f * b), s * (1.05f + 0.08f * b), 0, 0.35f * b * (0.4f + 0.6f * e)));
            if (e < 0.999f) o.Add(new Spr(FrostCoat, gx, gy, s, s, 0, 0.85f * (1f - e)));
            return;
        }
        if (GeloMode == 3)
        {
            float osc3 = 0.5f + 0.5f * MathF.Sin(t * 2.3f) * MathF.Sin(t * 0.9f + 0.6f);
            o.Add(new Spr(IceGlowA, gx, gy, s, s, 0, 0.3f + 0.5f * osc3));
            o.Add(new Spr(IceGlowB, gx, gy, s, s, 0, 0.25f + 0.5f * (1f - osc3)));
            if (e < 0.999f) o.Add(new Spr(FrostCoat, gx, gy, s, s, 0, 0.85f * (1f - e)));
            return;
        }
        if (GeloMode == 4)
        {
            // Prisão de gelo: a estrela empurra, brilha forte, o bloco trinca e solta vapor, mas não quebra.
            float pz = e * MathF.Pow(Math.Max(0f, MathF.Sin(t * 2.2f)), 3f) + e * 0.6f * MathF.Pow(Math.Max(0f, MathF.Sin(t * 5.1f + 1f)), 6f);
            pz = Math.Min(1f, pz);
            float osc4 = 0.5f + 0.5f * MathF.Sin(t * 2.3f) * MathF.Sin(t * 0.9f + 0.6f);
            o.Add(new Spr(IceGlowA, gx, gy, s, s, 0, Math.Clamp(0.25f + 0.4f * osc4 + 0.6f * pz, 0f, 1f)));
            o.Add(new Spr(IceGlowB, gx, gy, s, s, 0, Math.Clamp(0.2f + 0.4f * (1f - osc4) + 0.5f * pz, 0f, 1f)));
            o.Add(new Spr(GemGlow, gx, gy, s * 1.15f, s * 1.15f, 0, 0.55f * pz));
            if (e < 0.999f) o.Add(new Spr(FrostCoat, gx, gy, s, s, 0, 0.85f * (1f - e)));
            float jx = 0.012f * pz * MathF.Sin(t * 70f), bs = s * 1.18f;
            // Na Lendária o bloco vira um cubo que prende a bola inteira.
            float bx = inCube ? jx : gx + jx, by = inCube ? 0.06f : gy;
            if (inCube) bs = 1.55f;
            o.Add(new Spr(Block, bx, by, bs, bs, 0, 1f));
            o.Add(new Spr(BlockFrost, bx, by, bs, bs, 0, 0.2f + 0.3f * (1f - e)));
            // Trincas: aumentam com a força do empurrão (e somem devagar, o gelo se refaz).
            float cr = e * (0.35f + 0.65f * pz);
            int ci = Math.Min(BlockCracks.Length - 1, (int)(cr * BlockCracks.Length));
            if (cr > 0.05f) o.Add(new Spr(BlockCracks[ci], bx, by, bs, bs, 0, Math.Min(1f, cr * 1.5f)));
            // Vapor gelado subindo do bloco.
            for (int k = 0; k < 4; k++)
            {
                float u = t / 1.8f + k / 4f; u -= MathF.Floor(u);
                float vx = gx + (Hash(k * 7 + (int)(t / 1.8f + k / 4f)) - 0.5f) * 0.5f * s;
                o.Add(new Spr(Mist, vx, gy + 0.45f * s + u * 0.35f, 0.35f + 0.3f * u, 0.18f + 0.12f * u, 0, e * (0.3f + 0.5f * pz) * MathF.Sin(MathF.PI * u)));
            }
            return;
        }
        var (grow, crack, burst, cyc) = IceCycle(t, e);
        float osc = 0.5f + 0.5f * MathF.Sin(t * 2.3f) * MathF.Sin(t * 0.9f + 0.6f);
        float push = crack * crack;                                    // fica mais forte antes de quebrar
        o.Add(new Spr(IceGlowA, gx, gy, s, s, 0, Math.Clamp(0.25f + 0.50f * osc + 0.15f * e + 0.45f * push, 0f, 1f)));
        o.Add(new Spr(IceGlowB, gx, gy, s, s, 0, Math.Clamp(0.20f + 0.50f * (1f - osc) + 0.12f * e + 0.45f * push, 0f, 1f)));
        if (e < 0.999f) o.Add(new Spr(FrostCoat, gx, gy, s, s, 0, 0.85f * (1f - e)));
        // Cristais crescendo em volta e por cima da estrela.
        float shake = crack > 0f ? 0.012f * MathF.Sin(t * 90f) * crack : 0f;
        if (grow > 0.01f)
            for (int k = 0; k < 9; k++)
            {
                // Crescem da parte de baixo e das laterais, subindo por cima da estrela.
                float a = (192f + k * 19.5f + (Hash(k * 17 + 3) - 0.5f) * 10f) * MathF.PI / 180f;
                float bx = gx + MathF.Cos(a) * 0.40f * s, by = gy + MathF.Sin(a) * 0.40f * s;
                float tilt = MathF.Atan2(MathF.Cos(a), -MathF.Sin(a)) * 180f / MathF.PI * 0.6f + 16f * (Hash(k * 5) - 0.5f);   // para dentro e para cima
                float len = (0.20f + 0.14f * Hash(k * 11 + 1)) * grow * s;
                float tr = tilt * MathF.PI / 180f;
                float dx = -MathF.Sin(tr), dy = MathF.Cos(tr);
                o.Add(new Spr(IceSpike, bx + dx * len * 0.5f + shake, by + dy * len * 0.5f, len * 0.42f, len, tilt, 0.78f));
            }
        if (crack > 0f) o.Add(new Spr(GemGlow, gx, gy, s * 1.1f, s * 1.1f, 0, 0.6f * crack));
        if (burst >= 0f)
        {
            // Estilhaços voando para fora e um clarão rápido.
            o.Add(new Spr(GemGlow, gx, gy, s * (1.1f + burst), s * (1.1f + burst), 0, 0.8f * (1f - burst)));
            for (int k = 0; k < 10; k++)
            {
                float a = (k * 36f + Hash(cyc * 31 + k) * 30f) * MathF.PI / 180f;
                float d = 0.18f + 0.65f * (1f - (1f - burst) * (1f - burst));
                float sz = (0.07f + 0.06f * Hash(cyc * 7 + k)) * (1f - 0.4f * burst);
                o.Add(new Spr(Particle, gx + MathF.Cos(a) * d, gy + MathF.Sin(a) * d - 0.15f * burst * burst, sz, sz,
                    a * 180f / MathF.PI + burst * 300f, e * (1f - burst)));
            }
        }
    }

    /// <summary>
    /// Asas de morcego de gelo: fechadas em volta do bloco quando encaixada (por cima dele),
    /// abrindo com tudo atrás da bola quando a Corrente é ativada, e batendo devagar.
    /// </summary>
    void IceWings(float e, float t, float gx, float gy, float cy, bool back, List<Spr> o)
    {
        float op = e * e * (3f - 2f * e);                              // abertura suave
        float a = back ? op : 1f - op;
        if (a < 0.01f) return;
        float flap = MathF.Sin(t * 1.6f) * op;
        for (int sd = -1; sd <= 1; sd += 2)
        {
            int tex = sd < 0 ? AuroraA : AuroraB;
            // Fechada: estreita, em pé, colada na lateral do bloco. Aberta: grande, para fora e para cima.
            float x = sd * (0.20f + 0.46f * op), y = (gy + 0.06f) * (1f - op) + (cy + 0.22f) * op;
            float w = 0.72f + (1.36f - 0.72f) * op, h = 0.58f + (1.32f - 0.62f) * op;
            float rot = sd * (78f * (1f - op)) + sd * 6f * flap;
            o.Add(new Spr(tex, x, y, w * (1f + 0.06f * flap), h, rot, a));
        }
    }

    /// <summary>
    /// Caos: mandíbula que gargalha (e ruge na explosão) e fogo roxo saindo das órbitas.
    /// </summary>
    void DemonSkull(float e, float t, float gx, float gy, float s, List<Spr> o) { }

    /// <summary>
    /// Caos: gárgula demoníaca. Encaixada parece estátua (olhos semicerrados, boca fechada);
    /// ativa rosna, os olhos pegam fogo e sai fumaça das narinas; na explosão ruge.
    /// </summary>
    void Gargoyle(float e, float t, float gx, float gy, float s, bool legend, List<Spr> o, bool front)
    {
        var (ch, bu, cyc) = Combust(t, e);
        float roar = bu >= 0f ? MathF.Sin(MathF.Min(1f, bu * 1.3f) * MathF.PI) : 0f;
        float snarl = e * (0.25f + 0.25f * ch + 0.1f * Math.Max(0f, MathF.Sin(t * 9f)));
        float open = Math.Min(1f, snarl + roar);
        if (!front)
        {
            o.Add(new Spr(SkullHorns, gx, gy, s, s, 0, 1f));
            // Interior da boca (aparece quando a mandíbula desce).
            o.Add(new Spr(MouthIn, gx, gy - 0.20f * s, s * 0.62f, s * (0.10f + 0.22f * open), 0, 0.6f + 0.4f * e));
            return;
        }
        o.Add(new Spr(SkullJaw, gx, gy - (0.01f + 0.12f * open) * s, s, s, 0, 1f));
        // Olhos: fenda fraca encaixada, fogo aberto ativa.
        float ey = 0.25f + 0.75f * e;
        float ea = 0.35f + 0.65f * e * (0.7f + 0.3f * ch) + 0.5f * roar;
        o.Add(new Spr(GarEyes, gx, gy + 0.035f * s, s, s * ey, 0, Math.Min(1f, ea)));
        // Fumaça das narinas.
        for (int k = 0; k < 4; k++)
        {
            float u = t / 1.4f + k / 4f; int c2 = (int)MathF.Floor(u); u -= c2;
            float sd = (k & 1) == 0 ? -1f : 1f;
            o.Add(new Spr(Mist, gx + sd * (0.035f + 0.06f * u) * s, gy - 0.09f * s + u * 0.18f * s, (0.10f + 0.16f * u) * s, (0.10f + 0.16f * u) * s, u * 90f, e * 0.55f * MathF.Sin(MathF.PI * u)));
        }
        // Lendária: de vez em quando cospe uma bola de fogo roxa (na explosão, um ciclo sim, um não).
        if (legend && bu >= 0f && (cyc & 1) == 0)
        {
            float g = 0.25f + 1.1f * MathF.Sqrt(bu);
            float fy = gy - (0.22f + 0.55f * bu) * s;          // a bola de fogo sai da boca e vem para frente/baixo
            o.Add(new Spr(FireBreath, gx, fy, g * 0.8f * s, g * 0.8f * s, bu * 60f, e * (1f - bu * bu)));
            o.Add(new Spr(FireBreath, gx, fy, g * 0.5f * s, g * 0.5f * s, -bu * 90f + 30f, e * (1f - bu)));
            for (int k = 0; k < 12; k++)
            {
                float ang = (k * 30f + Hash(cyc * 13 + k) * 25f) * MathF.PI / 180f;
                float d = (0.1f + 0.9f * bu) * s;
                o.Add(new Spr(Particle, gx + MathF.Cos(ang) * d, gy - (0.22f + 0.55f * bu) * s + MathF.Sin(ang) * d, 0.08f * s, 0.08f * s, 0, e * (1f - bu)));
            }
        }
    }

    /// <summary>
    /// Caos: fogo roxo tremulando dentro do cristal de chama e os olhos do vulto acesos
    /// (fracos encaixado, fortes carregando, clarão na explosão).
    /// </summary>
    void ChaosCrystal(float e, float t, float gx, float gy, float s, List<Spr> o)
    {
        var (ch, bu, _) = Combust(t, e);
        float fl = 0.5f + 0.5f * MathF.Sin(t * 7.3f) * MathF.Sin(t * 3.1f + 1f);
        o.Add(new Spr(FlameInA, gx, gy, s, s * (1f + 0.03f * fl), 0, Math.Clamp(0.15f + 0.25f * fl + 0.30f * ch, 0f, 1f)));
        o.Add(new Spr(FlameInB, gx, gy, s, s * (1f + 0.03f * (1f - fl)), 0, Math.Clamp(0.12f + 0.25f * (1f - fl) + 0.25f * ch, 0f, 1f)));
        float flash = bu >= 0f ? 1f - bu : 0f;
        float eyes = 0.35f + 0.65f * e * (0.6f + 0.4f * ch) + flash;
        o.Add(new Spr(ChaosEyes, gx, gy, s, s, 0, Math.Min(1f, eyes)));
        o.Add(new Spr(GemGlow, gx, gy, s * 1.08f, s * 1.08f, 0, 0.12f + 0.3f * ch + 0.6f * flash));
    }

    /// <summary>Bola rúnica: redemoinhos de energia girando e a runa acesa no centro.</summary>
    void RuneOrb(float e, float t, float gx, float gy, float s, (float, float, int, float, float) f, List<Spr> o)
    {
        // Energia oscilante: núcleo de plasma pulsando e dois redemoinhos que se alternam.
        float sp = 1f + 1.5f * e + 3f * f.Item2;
        float osc = 0.5f + 0.5f * MathF.Sin(t * 2.6f) * MathF.Sin(t * 1.1f + 0.7f);
        float pz = 0.80f + 0.12f * MathF.Sin(t * 4.3f) + 0.10f * f.Item1;
        o.Add(new Spr(Plasma, gx, gy, s * pz, s * pz, t * 20f, Math.Clamp(0.45f + 0.35f * osc + 0.2f * e + 0.4f * f.Item5, 0f, 1f)));
        o.Add(new Spr(OrbSwirlA, gx, gy, s, s, -t * 35f * sp, 0.25f + 0.6f * osc));
        o.Add(new Spr(OrbSwirlB, gx, gy, s * 0.92f, s * 0.92f, t * 48f * sp + 30f, 0.25f + 0.6f * (1f - osc)));
        o.Add(new Spr(GemGlow, gx, gy, s * (1.05f + 0.1f * f.Item2), s * (1.05f + 0.1f * f.Item2), 0, 0.05f + 0.2f * e * f.Item1 + 0.5f * f.Item5));
        OrbitRunes(e, t, gx, gy, s, true, o);
        if (_lv >= Lustrosa && e > 0.01f) Rifts(e, t, o);
    }

    /// <summary>Fendas no espaço abrindo e fechando em volta da bola, soltando fagulhas.</summary>
    void Rifts(float e, float t, List<Spr> o)
    {
        int n = _lv >= Lendaria ? 4 : _lv >= Radiante ? 3 : 2;
        float boost = _lv >= Lendaria ? Clamp01(_esc * 1.5f) : 0f;
        for (int k = 0; k < n; k++)
        {
            float u = t / 2.3f + k / (float)n; int cyc = (int)MathF.Floor(u); u -= cyc;
            float open = MathF.Sin(MathF.PI * Math.Min(1f, u / 0.7f));
            if (u > 0.7f) open = 0f;
            open = Math.Max(open, boost);
            if (open <= 0.02f) continue;
            float ang = (Hash(k * 17 + cyc * 7) * 360f) * MathF.PI / 180f;
            float r = 0.68f + 0.35f * Hash(k * 5 + cyc * 3);
            float x = MathF.Cos(ang) * r, y = 0.08f + MathF.Sin(ang) * r * 0.85f;
            float rot = Hash(k * 11 + cyc) * 180f;
            o.Add(new Spr(Rift, x, y, 0.36f, 0.36f * (0.15f + 0.85f * open), rot, e * open));
            for (int q = 0; q < 2; q++)
            {
                float pu = (u * 3f + q * 0.5f) % 1f;
                float pa = rot * MathF.PI / 180f + MathF.PI / 2f * (q == 0 ? 1f : -1f);
                o.Add(new Spr(Particle, x + MathF.Cos(pa) * pu * 0.2f, y + MathF.Sin(pa) * pu * 0.2f + pu * 0.08f, 0.06f, 0.06f, 0, e * open * (1f - pu)));
            }
        }
    }

    /// <summary>Runas pequenas flutuando em órbita inclinada em volta da bola (metade de trás atrás dela).</summary>
    void OrbitRunes(float e, float t, float gx, float gy, float s, bool front, List<Spr> o)
    {
        int n = Math.Min(OrbGlyphs.Length, 2 + _lv);                 // Bruta 2 runas ... Dourada 6
        int rings = _lv >= Lustrosa ? 2 : 1;
        float spread = _lv >= Lendaria ? Clamp01(_esc * 1.6f) : 0f;   // Lendária: as runas abrem num círculo grande ao escapar
        for (int ring = 0; ring < rings; ring++)
        {
            float dir = ring == 0 ? 1f : -1f;
            float spin = dir * t * (0.5f + 0.7f * e) + ring * 0.5f;
            float tilt = ring == 0 ? 0.30f : -0.22f;
            float rr = ring == 0 ? 0.47f : 0.60f;
            for (int k = 0; k < n; k++)
            {
                float a = spin + k * MathF.Tau / n;
                float z = MathF.Sin(a) * dir;
                float zz = z * (1f - spread);
                if ((zz >= 0f) != front) continue;
                float r = (rr + spread * (0.55f + 0.15f * ring)) * s;
                float flat = tilt * (1f - spread) + spread;                    // vira círculo de frente
                float x = gx + MathF.Cos(a) * r, y = gy + MathF.Sin(a) * r * flat + 0.04f * s * MathF.Sin(t * 2f + k * 1.7f) * (1f - spread) + 0.06f * s * (1f - spread);
                float sz = s * (0.15f + 0.04f * zz + 0.05f * spread);
                float flick = 0.75f + 0.25f * MathF.Sin(t * 5f + k * 2.1f + ring);
                o.Add(new Spr(OrbGlyphs[(k + ring * 3) % OrbGlyphs.Length], x, y, sz, sz, spread * (a * 180f / MathF.PI - 90f),
                    Math.Min(1f, (0.45f + 0.35f * zz + 0.4f * spread) * (0.6f + 0.4f * e) * flick)));
            }
        }
    }

    /// <summary>
    /// Correntes de magia prendendo a bola (ancoradas embaixo). Uma arrebenta de vez em quando,
    /// a ponta solta chicoteia, depois dispara de volta e gruda na bola com um clarão.
    /// </summary>
    void MagicChains(float e, float t, float gx, float gy, float s, (float, float, int, float, float) f, List<Spr> o)
    {
        OrbitRunes(e, t, gx, gy, s, false, o);
        float[] anchors = { 205f, 270f, 335f };
        float orbR = 0.30f * s;
        for (int c = 0; c < 3; c++)
        {
            float a = anchors[c] * MathF.PI / 180f;
            float ax = MathF.Cos(a) * 0.44f, ay = MathF.Sin(a) * 0.44f;
            float dx0 = ax - gx, dy0 = ay - gy, dl = MathF.Sqrt(dx0 * dx0 + dy0 * dy0) + 1e-4f;
            float bx = gx + dx0 / dl * orbR, by = gy + dy0 / dl * orbR;     // ponto da bola virado para a âncora
            bool broken = f.Item3 == c;
            float ex = bx, ey = by;
            if (broken)
            {
                // Ponta solta caída e chicoteando; ao reconectar, dispara até a bola.
                float lx = ax + (bx - ax) * 0.55f + 0.10f * MathF.Sin(t * 13f + c), ly = ay + (by - ay) * 0.35f - 0.08f;
                float r = f.Item4;
                ex = lx + (bx - lx) * r; ey = ly + (by - ly) * r;
            }
            float dx = ex - ax, dy = ey - ay;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len < 0.02f) continue;
            float slack = (1f - e) * 0.05f + (broken ? 0.06f : 0.01f) * MathF.Sin(t * 5f + c);
            int n = Math.Max(3, (int)(len / 0.055f));
            float ang = MathF.Atan2(dy, dx) * 180f / MathF.PI;
            float glowPulse = (0.55f + 0.06f * _lv) * (0.75f + 0.25f * MathF.Sin(t * 6f + c * 2f)) + 0.4f * f.Item1;
            float th = 0.80f + 0.07f * _lv;                               // elos mais grossos conforme evolui
            o.Add(new Spr(MLinkFace, ax, ay, 0.13f, 0.13f, 0, 1f));
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n;
                float sag = slack * 4f * u * (1f - u);
                float x = ax + dx * u, y = ay + dy * u - sag;
                bool face = (i & 1) == 0;
                o.Add(new Spr(face ? MLinkFace : MLinkEdge, x, y, (face ? 0.11f : 0.10f) * th, (face ? 0.078f : 0.04f) * th, ang, Math.Min(1f, glowPulse)));
                if (face && _lv >= Radiante && (i & 3) == 2)
                    o.Add(new Spr(LinkRune, x, y, 0.07f * th, 0.07f * th, 0, Math.Min(1f, glowPulse) * (0.6f + 0.4f * MathF.Sin(t * 4f + i))));
            }
            if (broken && f.Item4 > 0.9f) o.Add(new Spr(Glint, ex, ey, 0.35f, 0.35f, t * 90f, 1f));      // clarão ao grudar
        }
    }

    /// <summary>
    /// Globo de plasma: filamentos elétricos saindo do centro até o vidro, se mexendo sem parar.
    /// Conforme a carga sobe, convergem para cima (onde está o raio); na descarga, todos nele.
    /// </summary>
    void PlasmaGlobe(float e, float t, List<Spr> o)
    {
        var (c, st, _) = Storm(t, e);
        float flash = st >= 0f ? 1f - st : 0f;
        o.Add(new Spr(PlasmaCore, 0, 0, 0.55f, 0.55f, t * 30f, Math.Min(1f, 0.45f + 0.15f * MathF.Sin(t * 7f) + 0.3f * c + flash)));
        int fr = (int)MathF.Floor(t * 12f);
        float pull = Math.Min(1f, c * c * 0.85f + flash);
        for (int k = 0; k < 9; k++)
        {
            float baseA = k * 40f + 25f * MathF.Sin(t * (0.6f + 0.13f * k) + k * 1.7f) + t * 9f;
            // Converge para 90° (para cima) pelo caminho mais curto.
            float da = ((90f - baseA) % 360f + 540f) % 360f - 180f;
            float ang = baseA + da * pull * (0.75f + 0.25f * Hash(k * 7));
            float len = 0.36f + 0.05f * MathF.Sin(t * 3f + k);
            float rad = ang * MathF.PI / 180f;
            float mx = MathF.Cos(rad) * len * 0.5f, my = MathF.Sin(rad) * len * 0.5f;
            o.Add(new Spr(Filaments[(fr + k) % Filaments.Length], mx, my, 0.26f, len, ang - 90f, Math.Min(1f, 0.55f + 0.3f * e + 0.4f * flash)));
        }
    }

    /// <summary>Desenha um relâmpago esticado entre dois pontos (textura vertical, base embaixo).</summary>
    void Strike(float x0, float y0, float x1, float y1, float w, int frame, float a, List<Spr> o)
    {
        float dx = x1 - x0, dy = y1 - y0, len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f || a <= 0f) return;
        float ang = MathF.Atan2(dy, dx) * 180f / MathF.PI - 90f;
        o.Add(new Spr(Lightning[frame % Lightning.Length], (x0 + x1) * 0.5f, (y0 + y1) * 0.5f, w, len, ang, a));
    }

    /// <summary>
    /// Raio vivo: núcleo brilhando conforme carrega, faíscas correndo pelas bordas e,
    /// na descarga, um relâmpago cai na joia (das bobinas, na Lendária) com clarão e onda de choque.
    /// </summary>
    void LiveBolt(float e, float t, float gx, float gy, float s, bool legend, float cy, List<Spr> o)
    {
        var (c, st, cyc) = Storm(t, e);
        int fr = (int)MathF.Floor(t * 14f);
        float flash = st >= 0f ? 1f - st : 0f;
        o.Add(new Spr(BoltCore, gx, gy, s, s, 0, Math.Min(1f, 0.10f + 0.55f * c + flash)));
        // Eletricidade correndo por dentro do cristal.
        o.Add(new Spr(BoltVeins[fr % BoltVeins.Length], gx, gy, s, s, 0, Math.Min(1f, 0.25f + 0.6f * e * (0.5f + 0.5f * c) + flash)));
        // Faíscas nas bordas: encaixado só de vez em quando; ativo sem parar, mais forte carregando.
        float idle = Hash(fr * 7 + 3) > 0.82f ? 0.7f : 0f;
        float sa = Math.Max(idle * (1f - e), e * (0.45f + 0.55f * c));
        o.Add(new Spr(EdgeSparks[fr % EdgeSparks.Length], gx, gy, s * 1.08f, s * 1.08f, 0, Math.Min(1f, sa + flash)));
        if (st >= 0f)
        {
            float a = 1f - st;
            int lf = cyc * 3 + (int)(st * 6f);
            if (legend)
            {
                for (int sd = -1; sd <= 1; sd += 2)
                    Strike(sd * (0.90f + 0.17f), cy + 0.70f, gx + sd * 0.05f, gy + 0.25f * s, 0.30f, lf + sd, a, o);
            }
            else Strike(gx + 0.05f * MathF.Sin(cyc * 3.1f), gy + 1.45f, gx, gy + 0.40f * s, 0.32f, lf, a, o);
            o.Add(new Spr(GemGlow, gx, gy, s * (1.1f + 0.4f * st), s * (1.1f + 0.4f * st), 0, a));
        }
    }

    /// <summary>Gelo Lendário: camada entre a aura e a bola (armadura, redemoinhos, cubo).</summary>
    void GeloLegendMid(float e, float t, float cy, List<Spr> o)
    {
        switch (GeloMode)
        {
            case 5:   // selo de runas: anéis girando em sentidos opostos
            {
                o.Add(new Spr(RuneCircle, 0, cy, 2.55f, 2.55f, -t * 9f, e * 0.8f));
                for (int k = 0; k < 12; k++)
                {
                    float a = (k * 30f - t * 9f) * MathF.PI / 180f;
                    float tw = 0.6f + 0.4f * MathF.Sin(t * 3f + k * 0.9f);
                    o.Add(new Spr(Glyphs[k], MathF.Cos(a) * 1.10f, cy + MathF.Sin(a) * 1.10f, 0.22f, 0.22f, a * 180f / MathF.PI - 90f, e * tw));
                }
                break;
            }
            case 1:   // armadura de gelo em volta da bola
            {
                float b = Beat(t, 66f);
                o.Add(new Spr(Armor, 0, 0, 1.75f * (1f + 0.015f * b), 1.75f * (1f + 0.015f * b), 0, e));
                break;
            }
            case 3:   // dois redemoinhos de neve subindo dos lados, como asas
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    int i = (int)MathF.Floor(t * 9f);
                    float tilt = sd * 24f * MathF.PI / 180f;
                    for (int j = 0; j < 13; j++)
                    {
                        float h = j * 0.095f;
                        float w = 0.14f + 0.05f * j;
                        float wob = 0.05f * MathF.Sin(t * 3.1f + j * 0.8f + sd);
                        float x = sd * 0.55f + MathF.Sin(tilt) * h + wob, y = cy - 0.35f + MathF.Cos(tilt) * h;
                        if ((j & 1) == 0) o.Add(new Spr(Mist, x, y, w * 1.5f, w * 0.9f, -sd * 24f + 90f, e * 0.30f));   // corpo de neve do funil
                        o.Add(new Spr(Tornado[(i + j + (sd > 0 ? 1 : 0)) % Tornado.Length], x, y, w, w * 0.30f, -sd * 24f, e * (0.30f + 0.35f * j / 12f)));
                    }
                }
                break;
        }
    }

    /// <summary>Quadro do morcego batendo as asas (cima, meio, baixo, meio...).</summary>
    int BatFrame(float t, int k)
    {
        if (BatFrames.Length == 0) return M2;
        float f = t * 9f + k * 0.37f; f -= MathF.Floor(f);
        return f < 0.25f ? BatFrames[0] : f < 0.5f ? BatFrames[1] : f < 0.75f ? BatFrames[2] : BatFrames[1];
    }

    // ───────── nível Lendário: traços dos bosses ─────────

    void LegendBack(float e, float t, float gx, float gy, float cy, List<Spr> o)
    {
        // Segunda camada de aura, maior e mais suave.
        o.Add(new Spr(Halo, 0, cy, 3.0f, 3.0f, 0, 0.35f * e * (0.8f + 0.2f * MathF.Sin(t * 1.3f))));
        switch (Kind)
        {
            case GemArt.Sangue:      // chifres: desenhados depois da escuridão (ver Frame)
                break;
            case GemArt.Tempestade:  // duas bobinas de Tesla dos lados, estalando no topo
            {
                var (c, _, _) = Storm(t, e);
                int fr = (int)MathF.Floor(t * 14f);
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    // Arco contínuo da esfera da bobina até o vidro do globo (alimentando a joia).
                    float tx = sd * (0.90f + 0.17f), ty = cy + 0.70f;
                    float ex = sd * 0.40f, ey = 0.22f;
                    Strike(tx, ty, ex, ey, 0.20f + 0.08f * c, fr + (sd > 0 ? 7 : 0), e * (0.75f + 0.25f * MathF.Sin(t * 20f + sd)), o);
                    o.Add(new Spr(Tesla, sd * 0.90f, cy + 0.10f, 0.72f, 1.55f, -sd * 16f, e));
                    // Energia subindo pelos anéis, de baixo para cima (mais rápida com a carga).
                    float up = t * (1.2f + 2.5f * c) + (sd > 0 ? 0.5f : 0f);
                    for (int k = 0; k < 3; k++)
                    {
                        float ph = up - k * 0.28f; ph -= MathF.Floor(ph);
                        float ga = MathF.Max(0f, 1f - ph * 2.2f);
                        o.Add(new Spr(CoilRings[k], sd * 0.90f, cy + 0.10f, 0.72f, 1.55f, -sd * 16f, e * Math.Min(1f, 0.05f + 0.2f * c + 0.9f * ga)));
                    }
                    // Arcos pequenos dançando no anel do topo (mais com a carga).
                    for (int k = 0; k < 2; k++)
                    {
                        if (Hash(fr * 11 + k * 5 + sd * 3) > 0.35f + 0.6f * c) continue;
                        float ang = Hash(fr * 13 + k + sd) * MathF.Tau;
                        float l = 0.14f + 0.18f * c;
                        Strike(sd * (0.90f + 0.17f), cy + 0.70f, sd * (0.90f + 0.17f) + MathF.Cos(ang) * l, cy + 0.70f + MathF.Sin(ang) * l * 0.8f, 0.10f, fr + k, e, o);
                    }
                }
                break;
            }
            case GemArt.Caos:        // asas de penas pretas batendo e juba desgrenhada em chamas
            {
                float flap = MathF.Sin(t * 2.0f);
                var (ch, _, _) = Combust(t, e);
                if (WingL >= 0)
                    for (int sd = -1; sd <= 1; sd += 2)
                        o.Add(new Spr(sd < 0 ? WingL : WingR, sd * 0.70f, cy + 0.26f + 0.05f * flap, 1.45f, 1.45f * (1f - 0.08f * flap), sd * -10f * flap, e));
                // Selo: um círculo mágico atrás da bola que se abre quando ela escapa e se fecha quando volta.
                if (ChaosCircle >= 0)
                {
                    float esc = Clamp01(_esc * 1.6f);
                    float cs = 1.3f + 1.2f * esc;
                    o.Add(new Spr(ChaosCircle, 0, cy, cs, cs, t * 18f, e * (0.35f + 0.65f * esc)));
                    o.Add(new Spr(ChaosCircle, 0, cy, cs * 0.72f, cs * 0.72f, -t * 26f + 20f, e * (0.25f + 0.6f * esc)));
                }
                else o.Add(new Spr(A1, 0, cy, 1.9f + 0.2f * ch, 1.9f + 0.2f * ch, t * 25f, e * 0.8f));
                break;
            }
            case GemArt.Gelo when GeloMode == 6:   // asas: desenhadas em Frame (fechadas em volta do bloco / abertas)
                break;
            case GemArt.Gelo when GeloMode != 0:   // véu espectral discreto (o resto vem em GeloLegendMid)
                o.Add(new Spr(M1, 0, cy, 2.7f, 2.7f, 8f * MathF.Sin(t * 0.7f), e * 0.45f));
                o.Add(new Spr(M2, 0, cy, 2.5f, 2.5f, -10f * MathF.Sin(t * 0.9f + 1f), e * 0.40f));
                break;
            case GemArt.Gelo:        // aurora boreal, coroa de geleira e véu espectral
                o.Add(new Spr(AuroraA, 0.10f * MathF.Sin(t * 0.5f), cy + 0.95f, 2.8f, 1.1f * (1f + 0.06f * MathF.Sin(t * 0.8f)), 0, e * (0.55f + 0.3f * MathF.Sin(t * 0.7f))));
                o.Add(new Spr(AuroraB, -0.12f * MathF.Sin(t * 0.4f + 1f), cy + 1.0f, 2.6f, 1.0f, 0, e * (0.55f + 0.3f * MathF.Sin(t * 0.9f + 2f))));
                o.Add(new Spr(M1, 0, cy, 2.7f, 2.7f, 8f * MathF.Sin(t * 0.7f), e * 0.45f));
                o.Add(new Spr(M2, 0, cy, 2.5f, 2.5f, -10f * MathF.Sin(t * 0.9f + 1f), e * 0.40f));
                o.Add(new Spr(Glacier, 0, cy + 0.02f, 2.15f, 2.15f, 0, e));
                break;
            case GemArt.Corrompida:
                if (MegaraMode == 2)
                {
                    // Tendrils de simbionte: cada um sai de trás da bola e se contorce sozinho.
                    int[] tex = { M4, M5, M6 };
                    for (int k = 0; k < 8; k++)
                    {
                        float baseAng = k * 45f + 22.5f + (Hash(k * 29 + 3) - 0.5f) * 26f;   // irregular, orgânico
                        float sway = 11f * MathF.Sin(t * 1.3f + k * 1.7f) + 5f * MathF.Sin(t * 2.9f + k);
                        float ang = (baseAng + sway) * MathF.PI / 180f;
                        float len = (0.80f + 0.50f * Hash(k * 13 + 5)) * (1f + 0.08f * MathF.Sin(t * 1.9f + k * 2.3f)) * e;
                        float wid = len * 0.55f;
                        // Centro da peça = base (perto da borda da bola) + metade do comprimento na direção do tentáculo.
                        float dx = -MathF.Sin(ang), dy = MathF.Cos(ang);
                        float bx = dx * 0.30f, by = cy + dy * 0.30f;
                        o.Add(new Spr(tex[k % 3], bx + dx * len * 0.5f, by + dy * len * 0.5f, wid * 1.3f, len, baseAng + sway, e));
                        // Veneno pingando da ponta.
                        float u = t / 1.6f + Hash(k * 41 + 9); int cyc = (int)MathF.Floor(u); float p = u - cyc;
                        float tx = bx + dx * len * 0.93f, ty = by + dy * len * 0.93f;
                        float dsz = p < 0.3f ? 0.05f + 0.05f * p / 0.3f : 0.10f;
                        float fy = p < 0.3f ? 0f : Sq((p - 0.3f) / 0.7f) * 0.55f;
                        o.Add(new Spr(Venom, tx, ty - fy - dsz * 0.3f, dsz, dsz * 1.3f, 0, e * (p > 0.8f ? (1f - p) / 0.2f : 1f)));
                    }
                    // Olhinhos se abrindo e fechando na escuridão.
                    for (int k = 0; k < 6; k++)
                    {
                        float u = t / 4.1f + k / 6f * 1.7f; int cyc = (int)MathF.Floor(u); float p = u - cyc;
                        float open = p < 0.1f ? p / 0.1f : p < 0.55f ? 1f : p < 0.65f ? 1f - (p - 0.55f) / 0.1f : 0f;
                        if (open <= 0f) continue;
                        float ang = (Hash(k * 19 + cyc * 7) * 2f - 1f) * MathF.PI;
                        float r = 0.80f + 0.45f * Hash(k * 3 + cyc * 13);
                        float sz = 0.22f + 0.10f * Hash(k * 5 + cyc);
                        o.Add(new Spr(MiniEye, MathF.Cos(ang) * r, cy + MathF.Sin(ang) * r * 0.85f, sz * 1.6f, sz * open, 0, e));
                    }
                }
                break;
            case GemArt.Luz:
                switch (LuzMode)
                {
                    case 2:   // mandala de luz girando atrás
                        o.Add(new Spr(M4, gx, gy, 2.35f, 2.35f, t * 6f, e * (0.85f + 0.15f * MathF.Sin(t * 2f))));
                        break;
                    case 4:   // coroa solar pulsando
                    {
                        float p = 1f + 0.06f * MathF.Sin(t * 2.6f);
                        o.Add(new Spr(M7, gx, gy, 2.5f * p, 2.5f * p, t * 4f, e));
                        o.Add(new Spr(M7, gx, gy, 2.0f / p, 2.0f / p, -t * 6f + 11f, e * 0.6f));
                        break;
                    }
                    case 6:   // fusão: coroa solar no núcleo + mandala girando em volta
                    {
                        float p = 1f + 0.05f * MathF.Sin(t * 2.6f);
                        o.Add(new Spr(M7, gx, gy, 2.15f * p, 2.15f * p, t * 4f, e * 0.75f));
                        o.Add(new Spr(M4, gx, gy, 2.35f, 2.35f, t * 6f, e * (0.7f + 0.15f * MathF.Sin(t * 2f))));
                        break;
                    }
                    case 5:   // rosácea de vitral grande, com a luz atravessando
                        o.Add(new Spr(M1, gx, gy, 2.05f, 2.05f, t * 5f, e * 0.95f));
                        break;
                    default:  // rosácea discreta (prisma e esferas)
                        o.Add(new Spr(M1, gx, gy, 1.45f, 1.45f, t * 8f, e * 0.45f));
                        break;
                }
                break;
        }
    }

    void LegendFront(float e, float t, float cy, List<Spr> o)
    {
        if (Kind == GemArt.Caos && Feather >= 0)
        {
            // Penas pretas caindo das asas e queimando em fogo roxo.
            for (int k = 0; k < 5; k++)
            {
                float u = t / 3.0f + k / 5f; int cyc = (int)MathF.Floor(u); u -= cyc;
                float sd = (k & 1) == 0 ? -1f : 1f;
                float x = sd * (0.7f + 0.5f * Hash(k * 7 + cyc)) + 0.12f * MathF.Sin(u * 9f + k);
                float y = cy + 0.7f - u * 1.5f;
                float a = e * Math.Min(1f, u * 6f) * (1f - u);
                o.Add(new Spr(Feather, x, y, 0.13f, 0.30f, 40f * MathF.Sin(u * 7f + k) + sd * 30f, a));
            }
        }
        if (Kind == GemArt.Gelo && GeloMode == 6)
        {
            // Mandíbula de gelo: abre devagar e morde de repente; o bloco fica dentro da boca.
            float u = t / 3.4f; u -= MathF.Floor(u);
            float open = u < 0.75f ? 0.5f - 0.5f * MathF.Cos(u / 0.75f * MathF.PI) : 1f - Sq((u - 0.75f) / 0.25f);
            float gap = 0.03f + 0.12f * open;
            o.Add(new Spr(JawTop, 0, cy + 0.72f + gap, 2.0f, 2.0f, 0, e));
            o.Add(new Spr(JawBottom, 0, cy - 0.60f - gap, 2.0f, 2.0f, 0, e));
        }
        switch (Kind)
        {
            case GemArt.Luz:
            {
                float gy = cy * 2f;
                if (LuzMode == 1)       // prisma flutuante abrindo a luz em arco-íris
                {
                    float bob = 0.035f * MathF.Sin(t * 1.8f);
                    o.Add(new Spr(M3, 0.05f, gy + 0.70f + bob, 1.55f, 0.78f, 4f * MathF.Sin(t * 0.9f), e * (0.9f + 0.1f * MathF.Sin(t * 3f))));
                }
                else if (LuzMode == 6)  // esferas correndo sobre o anel externo da mandala
                {
                    const float R = 1.08f;   // raio do anel externo da mandala (2.35 x 0.92 / 2)
                    for (int k = 0; k < 3; k++)
                    {
                        float a = t * 0.9f + k * MathF.Tau / 3f;
                        float ox = MathF.Cos(a) * R, oy = gy + MathF.Sin(a) * R;
                        float tx = -MathF.Sin(a), ty = MathF.Cos(a);
                        o.Add(new Spr(M6, ox - tx * 0.36f, oy - ty * 0.36f, 0.85f, 0.30f, MathF.Atan2(ty, tx) * 180f / MathF.PI, e));
                        o.Add(new Spr(M5, ox, oy, 0.40f, 0.40f, 0, e));
                    }
                }
                else if (LuzMode == 3)  // três esferas de luz em órbita, com rastro de cometa
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float a = t * 1.1f + k * MathF.Tau / 3f;
                        float ox = MathF.Cos(a) * 0.86f, oy = gy * 0.5f + MathF.Sin(a) * 0.86f * 0.55f;
                        float tang = (a + MathF.PI / 2f) * 180f / MathF.PI;     // direção do movimento
                        float tx = -MathF.Sin(a) * 0.86f, ty = MathF.Cos(a) * 0.86f * 0.55f;
                        float tl = MathF.Sqrt(tx * tx + ty * ty);
                        float back = 0.40f;
                        o.Add(new Spr(M6, ox - tx / tl * back, oy - ty / tl * back, 0.95f, 0.34f, MathF.Atan2(ty, tx) * 180f / MathF.PI, e));
                        o.Add(new Spr(M5, ox, oy, 0.46f, 0.46f, 0, e));
                    }
                }
                break;
            }
            case GemArt.Sangue:      // morcegos agora saem de trás da gola (ver Frame)
                for (int k = 0; k < 0; k++)
                {
                    float speed = 1.4f + 0.9f * Hash(k * 7 + 1);
                    float ang = t * speed + k * 2.39996f;                 // ângulo áureo: espalha bem
                    float r = 0.95f + 0.40f * Hash(k * 11 + 3) + 0.06f * MathF.Sin(t * 2.3f + k);
                    float rise = (Hash(k * 5 + 9) - 0.5f) * 0.6f + 0.1f * MathF.Sin(t * 1.7f + k);
                    float sz = 0.20f + 0.14f * Hash(k * 13 + 2);
                    float tilt = MathF.Cos(ang) * 20f;
                    o.Add(new Spr(BatFrame(t, k), MathF.Cos(ang) * r, cy + rise + MathF.Sin(ang) * r * 0.5f, sz * 1.4f, sz, tilt, e * (0.7f + 0.3f * Hash(k))));
                }
                break;
            case GemArt.Corrompida:  // ouroboros: a serpente mordendo a própria cauda, girando em volta da bola
                if (MegaraMode == 1)
                    o.Add(new Spr(M3, 0, cy, 2.05f, 2.05f, -t * 12f, e));
                break;
        }
    }

    /// <summary>Fragmentos de cristal orbitando (a partir da Radiante).</summary>
    void Shards(float e, float t, float cy, List<Spr> o)
    {
        for (int k = 0; k < 3; k++)
        {
            float ang = t * 1.2f + k * MathF.Tau / 3f;
            o.Add(new Spr(Shard, MathF.Cos(ang) * 0.68f, cy + MathF.Sin(ang) * 0.68f * 0.6f, 0.15f, 0.24f, t * 90f + k * 40f, e * 0.95f));
        }
    }

    const float R = 0.47f; // raio da bola

    void AuraBack(float e, float t, float gx, float gy, float cy, List<Spr> o)
    {
        float pulse = MathF.Sin(t * 2.4f);
        switch (Kind)
        {
            case GemArt.Luz:
                o.Add(new Spr(Halo, gx, gy, 2.4f * (1 + 0.07f * pulse), 2.4f * (1 + 0.07f * pulse), 0, e * (0.85f + 0.15f * pulse)));
                o.Add(new Spr(A2, gx, gy, 2.0f, 2.0f, t * 9f, e * (0.55f + 0.15f * pulse)));      // anel prismático
                o.Add(new Spr(A1, gx, gy, 2.8f, 2.8f, -t * 10f, e * (0.85f - 0.15f * pulse)));    // raios com bordas de arco-íris
                o.Add(new Spr(A1, gx, gy, 2.3f, 2.3f, t * 6f + 15f, e * 0.45f));
                break;

            case GemArt.Sangue:
                // Escuridão negro-avermelhada "apagando" o fundo, com brilho de sangue.
                o.Add(new Spr(A1, 0, cy, 2.9f, 2.9f, t * 5f, e * 0.95f));
                {
                    float hb = Heart(t, e);
                    float hs = 2.0f * (1f + 0.07f * hb);
                    o.Add(new Spr(Halo, 0, cy, hs, hs, 0, e * (0.55f + 0.35f * hb)));
                }
                // Anéis vermelhos se expandindo, como o aviso do enxame.
                for (int k = 0; k < 3; k++)
                {
                    float u = t / 2.1f + k / 3f; u -= MathF.Floor(u);
                    float sz = 0.9f + 1.8f * u;
                    o.Add(new Spr(A2, 0, cy, sz, sz, 0, e * (1f - u) * Math.Min(1f, u * 6f)));
                }
                break;

            case GemArt.Gelo when GeloMode == 6:
            {
                float hb = FrozenHeart(t, e);
                o.Add(new Spr(Halo, 0, cy, 2.0f * (1f + 0.06f * hb), 2.0f * (1f + 0.06f * hb), 0, e * (0.5f + 0.35f * hb)));
                // Onda de geada saindo a cada batida (mistura os dois ritmos conforme acorda).
                for (int r = 0; r < 2; r++)
                {
                    float bpm = r == 0 ? 34f : 74f, wgt = r == 0 ? 1f - e : e;
                    float u = t * bpm / 60f - 0.04f; u -= MathF.Floor(u);
                    float sz = 0.95f + 1.7f * MathF.Sqrt(u);
                    o.Add(new Spr(FrostRing, 0, cy, sz, sz, 0, e * wgt * (1f - u) * Math.Min(1f, u * 8f)));
                }
                // Frio de túmulo: névoa escorrendo para baixo da bola.
                for (int k = 0; k < 6; k++)
                {
                    float u = t / 3.4f + k / 6f; int cyc = (int)MathF.Floor(u); u -= cyc;
                    float x = (Hash(k * 13 + cyc * 7) - 0.5f) * 1.3f;
                    o.Add(new Spr(Mist, x, cy - 0.35f - u * 0.95f, 0.8f + 0.6f * u, 0.45f + 0.3f * u, 0, e * 0.55f * MathF.Sin(MathF.PI * u)));
                }
                o.Add(new Spr(Mist, 0.12f * MathF.Sin(t * 0.6f), cy - 0.66f, 2.6f, 0.9f, 0, e * 0.6f));
                break;
            }
            case GemArt.Gelo when GeloMode == 5:
            {
                o.Add(new Spr(Halo, 0, cy, 2.0f, 2.0f, 0, e * (0.6f + 0.15f * pulse)));
                o.Add(new Spr(Mist, 0.12f * MathF.Sin(t * 0.6f), cy - 0.62f, 2.6f, 0.95f, 0, e * 0.6f));
                // Círculo de runas em volta da bola, acendendo uma por uma.
                o.Add(new Spr(RuneCircle, 0, cy, 1.75f, 1.75f, t * 6f, e * 0.75f));
                float u = t / 2.6f; u -= MathF.Floor(u);
                int lit = (int)(u * 10f);                       // 8 acendem, depois todas brilham e apagam juntas
                float fadeAll = u > 0.8f ? 1f - (u - 0.8f) / 0.2f : 1f;
                for (int k = 0; k < 8; k++)
                {
                    float a = (90f - k * 45f + t * 6f) * MathF.PI / 180f;
                    float on = k < lit ? 1f : 0f;
                    float ga = e * (0.25f + 0.75f * on * fadeAll);
                    o.Add(new Spr(Glyphs[k], MathF.Cos(a) * 0.74f, cy + MathF.Sin(a) * 0.74f, 0.20f, 0.20f, a * 180f / MathF.PI - 90f, ga));
                }
                break;
            }
            case GemArt.Gelo when GeloMode == 1:
            {
                float b = Beat(t, 66f);
                o.Add(new Spr(Halo, 0, cy, 2.0f * (1f + 0.07f * b), 2.0f * (1f + 0.07f * b), 0, e * (0.55f + 0.35f * b)));
                // Um anel de geada sai a cada batida.
                float bp = t * 66f / 60f; float u = bp - MathF.Floor(bp);
                o.Add(new Spr(FrostRing, 0, cy, 1.0f + 1.6f * u, 1.0f + 1.6f * u, 0, e * (1f - u)));
                o.Add(new Spr(Mist, 0.12f * MathF.Sin(t * 0.6f), cy - 0.62f, 2.6f, 0.95f, 0, e * 0.7f));
                break;
            }
            case GemArt.Gelo when GeloMode == 3:
                o.Add(new Spr(Halo, 0, cy, 2.1f, 2.1f, 0, e * (0.65f + 0.15f * pulse)));
                o.Add(new Spr(Wind, 0, cy, 2.5f, 2.5f, -t * 140f, e * 0.8f));
                o.Add(new Spr(Wind, 0, cy, 2.0f, 2.0f, -t * 210f + 70f, e * 0.6f));
                break;
            case GemArt.Gelo when GeloMode == 4:
                o.Add(new Spr(Halo, 0, cy, 2.0f, 2.0f, 0, e * (0.6f + 0.15f * pulse)));
                o.Add(new Spr(Mist, 0.12f * MathF.Sin(t * 0.6f), cy - 0.62f, 2.6f, 0.95f, 0, e * 0.8f));
                o.Add(new Spr(Mist, -0.15f * MathF.Sin(t * 0.45f + 1f), cy - 0.70f, 2.2f, 0.8f, 180f, e * 0.55f));
                break;
            case GemArt.Gelo:
            {
                o.Add(new Spr(Halo, 0, cy, 2.1f, 2.1f, 0, e * (0.7f + 0.15f * pulse)));
                // Anéis de geada se espalhando.
                for (int k = 0; k < 2; k++)
                {
                    float u = t / 3.0f + k * 0.5f; u -= MathF.Floor(u);
                    float sz = 1.0f + 1.6f * u;
                    o.Add(new Spr(FrostRing, 0, cy, sz, sz, k * 17f, e * (1f - u) * Math.Min(1f, u * 5f)));
                }
                // Névoa gelada rolando por baixo.
                o.Add(new Spr(Mist, 0.12f * MathF.Sin(t * 0.6f), cy - 0.62f, 2.6f, 0.95f, 0, e * 0.85f));
                o.Add(new Spr(Mist, -0.15f * MathF.Sin(t * 0.45f + 1f), cy - 0.70f, 2.2f, 0.8f, 180f, e * 0.6f));
                break;
            }

            case GemArt.Corrompida:
            {
                // Miasma venenoso respirando: fumaça roxa em duas camadas e halo que incha e murcha.
                float br = Breath(t, e);
                o.Add(new Spr(A1, 0, cy, 2.7f * (1f + 0.06f * br), 2.7f * (1f + 0.06f * br), t * 7f, e * 0.95f));
                o.Add(new Spr(A2, 0, cy, 2.3f * (1f + 0.08f * br), 2.3f * (1f + 0.08f * br), -t * 11f, e * (0.6f + 0.35f * br)));
                o.Add(new Spr(Halo, 0, cy, 2.0f * (1f + 0.08f * br), 2.0f * (1f + 0.08f * br), 0, e * (0.45f + 0.35f * br)));
                break;
            }

            case GemArt.Caos:
            {
                var (ch, bu, _) = Combust(t, e);
                o.Add(new Spr(Smoke, 0, cy + 0.15f, 2.7f, 2.7f, t * 4f, e * 0.9f));
                o.Add(new Spr(Halo, 0, cy, 2.1f * (1f + 0.08f * ch), 2.1f * (1f + 0.08f * ch), 0, e * (0.45f + 0.35f * ch)));
                if (bu >= 0f)
                {
                    float sz = 1.0f + 2.0f * MathF.Sqrt(bu);
                    o.Add(new Spr(Shock, 0, cy, sz, sz, 0, e * (1f - bu)));
                    o.Add(new Spr(Halo, 0, cy, 2.6f, 2.6f, 0, e * 0.8f * (1f - bu)));
                }
                float u = t * 7f;
                int i = (int)MathF.Floor(u) % 3;
                float f = u - MathF.Floor(u);
                int[] fire = { A1, A2, A3 };
                float fl = 1f + 0.03f * MathF.Sin(t * 13f);
                float fs = 2.1f + 0.4f * ch;
                o.Add(new Spr(fire[i], 0, cy + 0.10f, fs, fs * fl, 0, e * (1f - f)));
                o.Add(new Spr(fire[(i + 1) % 3], 0, cy + 0.10f, fs, fs * fl, 0, e * f));
                break;
            }

            case GemArt.Tempestade:
            {
                var (sc, ss, _) = Storm(t, e);
                o.Add(new Spr(Halo, 0, cy, 2.0f * (1f + 0.08f * sc), 2.0f * (1f + 0.08f * sc), 0, e * (0.45f + 0.3f * sc + 0.15f * MathF.Sin(t * 9f) * MathF.Sin(t * 5.3f))));
                if (ss >= 0f)
                {
                    float sz = 1.6f + 1.4f * MathF.Sqrt(ss);
                    o.Add(new Spr(Halo, 0, cy, sz, sz, 0, e * 0.9f * (1f - ss)));
                }
                // Arcos pulando da bola para pontos em volta (mais frequentes com a carga).
                if (Lightning.Length > 0)
                {
                    int tk = (int)MathF.Floor(t * 9f);
                    for (int j = 0; j < 3; j++)
                    {
                        if (Hash(tk * 19 + j * 7) > 0.25f + 0.55f * sc) continue;
                        float a = Hash(tk * 23 + j) * MathF.Tau;
                        float r1 = 0.85f + 0.35f * Hash(tk * 5 + j);
                        Strike(MathF.Cos(a) * 0.46f, MathF.Sin(a) * 0.46f, MathF.Cos(a + 0.25f) * r1 * 0.85f, MathF.Sin(a + 0.25f) * r1 * 0.85f, 0.13f, tk + j, e * 0.9f, o);
                    }
                }
                break;
            }
        }
    }

    void AuraFront(float e, float t, int level, List<Spr> o)
    {
        int more = level >= Lustrosa ? 1 : 0;
        switch (Kind)
        {
            case GemArt.Luz:     // brilhos em estrela piscando em volta
                for (int k = 0, n = 7 + 3 * more; k < n; k++)
                {
                    float u = t / 1.6f + k / (float)n;
                    int cyc = (int)MathF.Floor(u);
                    float p = u - cyc;
                    float ang = Hash(k * 37 + cyc * 11) * MathF.Tau;
                    float r = 0.55f + 0.45f * Hash(k * 19 + cyc * 7);
                    float tw = MathF.Sin(MathF.PI * p);
                    float sz = (0.16f + 0.14f * Hash(k * 5 + cyc)) * tw;
                    o.Add(new Spr(Particle, MathF.Cos(ang) * r, MathF.Sin(ang) * r + 0.15f, sz, sz, 0, e * tw));
                }
                break;

            case GemArt.Sangue:  // orbes de morcegos convergindo + gotas pingando
                for (int k = 0; k < 3; k++)
                {
                    float u = t / 1.7f + k / 3f;
                    int cyc = (int)MathF.Floor(u);
                    float p = u - cyc;
                    float ang = (Hash(k * 53 + cyc * 17) * 2f - 1f) * MathF.PI;
                    float r = 1.35f * (1f - p * p);
                    float sz = 0.46f + 0.08f * MathF.Sin(t * 12f + k);
                    float a = Math.Min(1f, p / 0.15f) * (p > 0.85f ? (1f - p) / 0.15f : 1f);
                    float bx = MathF.Cos(ang) * r, by = 0.1f + MathF.Sin(ang) * r;
                    o.Add(new Spr(Halo, bx, by, sz * 0.9f, sz * 0.9f, 0, e * a * 0.55f));
                    o.Add(new Spr(BatFrame(t, k + 7), bx, by, sz * 0.95f, sz * 0.68f, MathF.Cos(ang) * 15f, e * a));
                }
                goto case 1000;
            case 1000:
                for (int k = 0, n = 5 + 2 * more; k < n; k++)
                {
                    float u = t / 1.8f + k / (float)n;
                    int cyc = (int)MathF.Floor(u);
                    float p = u - cyc;
                    float rx = (Hash(k * 31 + cyc * 7) - 0.5f) * 0.5f;
                    float y0 = -MathF.Sqrt(R * R - rx * rx) + 0.03f;
                    float y = p < 0.25f ? y0 : y0 - Sq((p - 0.25f) / 0.75f) * 0.75f;
                    float sz = p < 0.25f ? 0.06f + 0.10f * (p / 0.25f) : 0.16f;
                    float a = Math.Min(1f, p / 0.1f) * (p > 0.7f ? 1f - (p - 0.7f) / 0.3f : 1f);
                    o.Add(new Spr(Particle, rx, y - sz * 0.3f, sz, sz * 1.25f, 0, e * a));
                }
                break;

            case GemArt.Gelo when GeloMode == 3:   // lâminas de neve girando rápido
                for (int k = 0, n = 10 + 3 * more; k < n; k++)
                {
                    float h = Hash(k * 7 + 2);
                    float ang = k * 2.39996f + t * (2.6f + 1.4f * h);
                    float r = 0.62f + 0.45f * Hash(k * 3 + 1) + 0.05f * MathF.Sin(t * 3f + k);
                    float sz = 0.20f + 0.14f * h;
                    o.Add(new Spr(Blade, MathF.Cos(ang) * r, 0.1f + MathF.Sin(ang) * r * 0.6f, sz, sz * 0.45f,
                        ang * 180f / MathF.PI + 90f, e * (0.6f + 0.4f * MathF.Sin(ang + 1f))));
                }
                break;
            case GemArt.Gelo when GeloMode != 0:   // neve caindo devagar
                for (int k = 0, n = 10 + 4 * more; k < n; k++)
                {
                    float u = Frac(t * (0.10f + 0.06f * Hash(k)) + Hash(k + 3));
                    float x = (Hash(k * 11 + 5) - 0.5f) * 2.0f + 0.08f * MathF.Sin(t * 1.3f + k);
                    float y = 1.0f - u * 2.0f;
                    float sz = 0.08f + 0.07f * Hash(k + 11);
                    o.Add(new Spr(Flake, x, y, sz, sz, t * 40f + k * 30f, e * MathF.Sin(MathF.PI * u) * 0.9f));
                }
                break;
            case GemArt.Gelo:    // nevasca: flocos num redemoinho de vento
                for (int k = 0, n = 12 + 4 * more; k < n; k++)
                {
                    float h = Hash(k * 7 + 2);
                    float u = Frac(t * (0.22f + 0.12f * h) + Hash(k + 3));        // cada floco entra em espiral e sai
                    float ang = k * 2.39996f + t * (1.4f + 0.8f * h) + u * 3f;
                    float r = 1.25f - 0.65f * u + 0.05f * MathF.Sin(t * 2f + k);
                    float sz = (0.10f + 0.09f * Hash(k + 11)) * (0.8f + 0.4f * MathF.Sin(ang));
                    float a = MathF.Sin(MathF.PI * u);
                    o.Add(new Spr(Flake, MathF.Cos(ang) * r, 0.1f + MathF.Sin(ang) * r * 0.55f, sz, sz, t * 70f + k * 30f, e * a * 0.95f));
                }
                break;

            case GemArt.Corrompida: // bolhas subindo e estourando
                for (int k = 0, n = 6 + 3 * more; k < n; k++)
                {
                    float u = t / 2.2f + k / (float)n;
                    int cyc = (int)MathF.Floor(u);
                    float p = u - cyc;
                    float x0 = (Hash(k * 17 + cyc * 5) - 0.5f) * 0.95f;
                    float y0 = -0.05f + Hash(k * 23 + cyc * 3) * 0.3f;
                    float sz = (0.08f + 0.07f * Hash(k * 5 + cyc)) * (0.6f + 0.4f * p);
                    float a = Math.Min(1f, p / 0.12f) * (p > 0.85f ? 1f - (p - 0.85f) / 0.15f : 1f);
                    // Estoura no fim: incha rápido e some.
                    float pop = p > 0.85f ? 1f + (p - 0.85f) / 0.15f * 0.8f : 1f;
                    o.Add(new Spr(Particle, x0 + 0.06f * MathF.Sin(t * 3f + k), 0.25f + y0 + p * 0.75f, sz * pop, sz * pop, 0, e * a));
                }
                for (int k = 0, n = 4 + more; k < n; k++)       // gotas de veneno pingando da bola
                {
                    float u = t / 1.9f + k / (float)n; int cyc = (int)MathF.Floor(u); float p = u - cyc;
                    float rx = (Hash(k * 31 + cyc * 7) - 0.5f) * 0.5f;
                    float y0 = -MathF.Sqrt(0.2209f - rx * rx) + 0.03f;
                    float y = p < 0.25f ? y0 : y0 - Sq((p - 0.25f) / 0.75f) * 0.7f;
                    float sz = p < 0.25f ? 0.05f + 0.08f * (p / 0.25f) : 0.13f;
                    float a = Math.Min(1f, p / 0.1f) * (p > 0.7f ? 1f - (p - 0.7f) / 0.3f : 1f);
                    o.Add(new Spr(Venom, rx, y - sz * 0.3f, sz, sz * 1.25f, 0, e * a));
                }
                break;

            case GemArt.Caos:    // brasas roxas subindo
                for (int k = 0, n = 9 + 4 * more; k < n; k++)
                {
                    float u = t / 1.4f + k / (float)n;
                    int cyc = (int)MathF.Floor(u);
                    float p = u - cyc;
                    float x0 = (Hash(k * 41 + cyc * 9) - 0.5f) * 0.9f;
                    float y0 = 0.05f + Hash(k * 19 + cyc) * 0.3f;
                    float sz = 0.07f + 0.05f * Hash(k * 3 + cyc * 2);
                    float a = Math.Min(1f, p / 0.15f) * (1f - p);
                    o.Add(new Spr(Particle, x0 + 0.08f * MathF.Sin(t * 4f + k * 1.7f), y0 + p * 0.9f, sz, sz, 0, e * a));
                }
                {
                    var (_, bu, cyc) = Combust(t, e);
                    if (bu >= 0f)
                        for (int k = 0; k < 16; k++)
                        {
                            float ang = (k * 22.5f + Hash(cyc * 17 + k) * 20f) * MathF.PI / 180f;
                            float d = 0.30f + 0.95f * (1f - Sq(1f - bu));
                            float sz = (0.06f + 0.06f * Hash(k * 5 + cyc)) * (1f - 0.5f * bu);
                            o.Add(new Spr(Particle, MathF.Cos(ang) * d, 0.1f + MathF.Sin(ang) * d - 0.2f * bu * bu, sz, sz, 0, e * (1f - bu)));
                        }
                }
                break;
        }
    }

    internal static float Hash(int n)
    {
        unchecked
        {
            uint x = (uint)n * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
            x = (x >> 22) ^ x;
            return (x & 0xFFFFFF) / 16777216f;
        }
    }
    static float Frac(float v) => v - MathF.Floor(v);
    static float Sq(float v) => v * v;
}

/// <summary>Gera as texturas de cada elemento. C# puro: roda fora do Unity (em segundo plano ou na prévia).</summary>
internal static class GemArt
{
    // Mesma ordem dos botões.
    public const int Gelo = 0, Caos = 1, Tempestade = 2, Corrompida = 3, Luz = 4, Sangue = 5;
    const int S = 128, P = 48;

    public static GemStyle Build(int kind)
    {
        var g = new GemStyle(kind);
        switch (kind)
        {
            case Luz:
                // Branco perolado com toque dourado; luz branca que se abre em arco-íris (prisma).
                Base(g, 0xB9B4C8, 0xFFFFFF, 0x8C7E5A, Sun, 0xD8B266, 0xFFFDF2, 0x8A6A2A, 0x3A3020, energy: 0xFFF4D6);
                g.Halo = g.Add(S, Halo(0xFFFFFF, 0xFFE9C0));
                g.A1 = g.Add(S, PrismBeams());
                g.A2 = g.Add(S, PrismRing());
                g.Particle = g.Add(64, GlintTex());
                g.M1 = g.Add(S, Rosette());
                g.M2 = g.Add(S, Aureola());
                g.M3 = g.Add(256, PrismScene());
                g.M4 = g.Add(256, Mandala());
                g.M5 = g.Add(64, LightOrb());
                g.M6 = g.Add(S, CometTail());
                g.M7 = g.Add(256, Corona());
                break;

            case Sangue:
                // Vermelho-escuro, cristal de sangue em aglomerado, pedra com veios brilhando (inspirado na arena do Styx).
                // Bola vermelho-escura com poder obscuro girando dentro; gota de rubi acorrentada.
                Base(g, 0x10010A, 0x420612, 0x060004, Drop, 0x420010, 0xF02842, 0x16000A, 0x120004, energy: 0xFF2A44, gemWhite: 0.4f);
                g.InnerA = g.Add(S, InnerPower(2.3f));
                g.InnerB = g.Add(S, InnerPower(7.9f));
                g.LinkFace = g.Add(64, ChainLink(true));
                g.LinkEdge = g.Add(64, ChainLink(false));
                g.Collar = g.Add(256, VampireCollar());
                g.BatFrames = new[] { g.Add(64, BatPose(0.55f)), g.Add(64, BatPose(0f)), g.Add(64, BatPose(-0.5f)) };
                // Gota de vidro rubi com sangue líquido dentro (em vez das facetas).
                g.Gem = g.Add(S, GlassDrop(false));
                g.Gem2 = g.Add(S, GlassDrop(true));
                g.Liquid = new int[9];
                for (int i = 0; i < 9; i++) g.Liquid[i] = g.Add(S, LiquidTex((i - 4) * GemStyle.LiquidStep));
                g.GlassTop = g.Add(S, GlassShine());
                g.HotFace = g.Add(64, ChainHot(true));
                g.HotEdge = g.Add(64, ChainHot(false));
                g.Spark = g.Add(32, SparkTex());
                g.VeinA = g.Add(S, HoleVeins(Drop, false));
                g.VeinB = g.Add(S, HoleVeins(Drop, true));
                g.Eyes = g.Add(128, EyesTex());
                g.Halo = g.Add(S, Halo(0xFF2A40, 0x3A0008));
                g.A1 = g.Add(S, Darkness());
                g.A2 = g.Add(S, RedRing());
                g.A3 = g.Add(64, BatOrb());
                g.Particle = g.Add(P, Small(P, Drop, 0x8A0010, 0xFF4A5A, 0x2A0004));
                g.M1 = g.Add(256, Horns());
                g.M2 = g.Add(64, Bat());
                break;

            case Gelo:
                if (GemStyle.GeloMode == 6)
                {
                    // Vampiro clássico dormindo, de braços cruzados, preso num bloco de gelo cristalino.
                    Base(g, 0x2F7FC0, 0x8FD0F8, 0x1A4F80, IceBlockShape, 0x6FB4E8, 0xE8F8FF, 0x1E5A94, 0x0F3A68, energy: 0xA8F0FF);
                    g.Tex[g.Gem] = FrozenVampire(false);
                    g.Tex[g.Gem2] = FrozenVampire(true);
                    g.IceGlowA = g.Add(S, VampEyesTex());
                    g.IceGlowB = g.Add(S, FrostCoatTex(IceBlockShape));
                    g.BlockCracks = new int[3];
                    for (int i = 0; i < 3; i++) g.BlockCracks[i] = g.Add(S, IceBlockCracks(i));
                    g.IceShell = g.Add(S, IceShellTex());
                    g.Flake = g.Add(64, FlakeTex());
                    g.Mist = g.Add(S, MistTex());
                    g.FrostRing = g.Add(S, FrostRingTex());
                    g.AuroraA = g.Add(256, IceBatWing(-1f));     // asa esquerda
                    g.AuroraB = g.Add(256, IceBatWing(1f));      // asa direita
                    g.VampSharp = g.Add(S, VampSharpTex());
                    g.JawTop = g.Add(256, JawTex(true));
                    g.JawBottom = g.Add(256, JawTex(false));
                    g.InnerA = g.Add(S, InnerPower(2.9f, 0x3A7AC0, 0xBFE6FF, 0xFFFFFF));
                    g.InnerB = g.Add(S, InnerPower(8.3f, 0x2A5A9A, 0x9AD0F8, 0xF0FAFF));
                    g.Veins = g.Add(S, FrostRimTex());
                    g.VeinA = g.Add(S, HoleVeins(IceBlockShape, false, 0x0A2A5A, 0x5AA0E0));
                    g.VeinB = g.Add(S, HoleVeins(IceBlockShape, true, b0: 0xBFF0FF, b1: 0xFFFFFF));
                    g.M1 = g.Add(S, Veil(4.2f));
                    g.M2 = g.Add(S, Veil(17.9f));
                    g.Halo = g.Add(S, Halo(0xCFF4FF, 0x3A90E0));
                    g.Particle = g.Add(P, CrystalSmall(P, IceShard, 0x6FB4E8, 0xF2FBFF, 0x1E5A94));
                    break;
                }
                if (GemStyle.GeloMode == 5)
                {
                    // Placa de gelo claro em forma de caixão, com uma runa vampírica entalhada que brilha em ciano.
                    Base(g, 0x3A8AD0, 0xBFE6FF, 0x1E5A94, Coffin, 0x6FB4E8, 0xE8F8FF, 0x1E5A94, 0x0F3A68, energy: 0xA8F0FF);
                    g.Tex[g.Gem] = RuneTablet(false);
                    g.Tex[g.Gem2] = RuneTablet(true);
                    g.IceGlowA = g.Add(S, GlyphTex(MainRune(), S, 0.014f, true, 0.35f));
                    g.IceGlowB = g.Add(S, FrostCoatTex(Coffin));
                    g.Glyphs = new int[12];
                    for (int i = 0; i < 12; i++) g.Glyphs[i] = g.Add(64, GlyphTex(SmallRune(i), 64, 0.07f, true));
                    g.RuneCircle = g.Add(256, RuneCircleTex());
                    g.Flake = g.Add(64, FlakeTex());
                    g.Mist = g.Add(S, MistTex());
                    g.FrostRing = g.Add(S, FrostRingTex());
                    g.M1 = g.Add(S, Veil(4.2f));
                    g.M2 = g.Add(S, Veil(17.9f));
                    g.Halo = g.Add(S, Halo(0xCFF4FF, 0x3A90E0));
                    g.Particle = g.Add(P, CrystalSmall(P, IceShard, 0x6FB4E8, 0xF2FBFF, 0x1E5A94));
                    break;
                }
                Base(g, 0x2F7FC0, 0x8FD0F8, 0x1A4F80, GemStyle.GeloMode == 1 ? HeartShape : IceStar, 0x0E2A52, 0x5FA8E6, 0x05132A, 0x0F2A48, energy: 0xA8E6FF);
                // Cristal de gelo escuro: facetas claras/escuras por ponta, no lugar do cristal genérico.
                // Gelo escuro verdadeiro com brilho azul-claro oscilando dentro.
                if (GemStyle.GeloMode == 1)
                {
                    g.Tex[g.Gem] = HeartIceTex(false);
                    g.Tex[g.Gem2] = HeartIceTex(true);
                }
                else
                {
                    g.Tex[g.Gem] = TrueIceTex(false);
                    g.Tex[g.Gem2] = TrueIceTex(true);
                }
                Sd gs = GemStyle.GeloMode == 1 ? HeartShape : IceStar;
                g.Tex[g.Ball] = BallTex(0x041838, 0x1C4C8E, 0x020C22);
                g.IceGlowA = g.Add(S, IceGlow(1.7f, gs));
                g.IceGlowB = g.Add(S, IceGlow(6.3f, gs));
                g.FrostCoat = g.Add(S, FrostCoatTex(gs));
                if (GemStyle.GeloMode == 1)
                {
                    g.LakeCracks = new int[4];
                    for (int i = 0; i < 4; i++) g.LakeCracks[i] = g.Add(S, LakeCrackTex(i));
                    g.Armor = g.Add(256, ArmorTex());
                }
                if (GemStyle.GeloMode == 3)
                {
                    g.StormA = g.Add(S, StormTex(2.1f, 3.2f));
                    g.StormB = g.Add(S, StormTex(7.7f, 4.4f));
                    g.Wind = g.Add(S, WindTex());
                    g.Blade = g.Add(64, BladeTex());
                    g.Tornado = new[] { g.Add(64, SwirlRing(0f)), g.Add(64, SwirlRing(2.1f)), g.Add(64, SwirlRing(4.2f)) };
                }
                if (GemStyle.GeloMode == 4)
                {
                    g.Block = g.Add(S, BlockTex(0.88f));
                    g.BlockFrost = g.Add(S, BlockFrostTex(0.88f));
                    g.BlockCracks = new int[4];
                    for (int i = 0; i < 4; i++) g.BlockCracks[i] = g.Add(S, BlockCrackTex(i, 0.88f));
                }
                g.IceSpike = g.Add(64, IceSpikeTex());
                g.SnowDot = g.Add(32, SnowDotTex());
                g.IceShell = g.Add(S, IceShellTex());
                g.Flake = g.Add(64, FlakeTex());
                g.Mist = g.Add(S, MistTex());
                g.FrostRing = g.Add(S, FrostRingTex());
                g.Glacier = g.Add(256, GlacierTex());
                g.AuroraA = g.Add(S, AuroraTex(1.3f));
                g.AuroraB = g.Add(S, AuroraTex(5.1f));

                g.Halo = g.Add(S, Halo(0xBFEFFF, 0x2A78D0));
                g.A1 = g.Add(S, Frost());
                g.Particle = g.Add(P, CrystalSmall(P, IceShard, 0x3A78B8, 0xE8F6FF, 0x0E2A52));
                g.M1 = g.Add(S, Veil(4.2f));
                g.M2 = g.Add(S, Veil(17.9f));
                break;

            case Corrompida:
                // Bola da cor do líquido corrompido, com névoa venenosa girando dentro e veias na superfície.
                Base(g, 0x4A0C7A, 0xB848F0, 0x1E0434, Eye, 0x3A0F5A, 0x6E2A9E, 0x16031F, 0x1A0430, EyeColor, energy: 0xB45CFF);
                g.InnerA = g.Add(S, InnerPower(3.1f, 0x5A1490, 0xC060FF, 0xE8C8FF));
                g.InnerB = g.Add(S, InnerPower(8.7f, 0x1E6A1A, 0x7CF04A, 0xD8FFB0));
                g.Veins = g.Add(S, CorruptVeins());
                // Olho vivo dentro de uma moldura de obsidiana.
                g.Gem = g.Add(S, CrystalTex(EyeFrame, 0x14041E, 0x6A2E9E, 0x0A0210, null, white: 0.6f));
                g.Gem2 = g.Add(S, CrystalTex(EyeFrame, 0x14041E, 0x7A3AB8, 0x0A0210, null, facets: 12f, polished: true, white: 0.7f));
                g.Sclera = g.Add(S, ScleraTex());
                g.Iris = g.Add(128, IrisTex());
                g.Pupil = g.Add(64, PupilTex());
                g.Lids = new int[6];
                for (int i = 0; i < 6; i++) g.Lids[i] = g.Add(S, LidTex(i / 5f));
                g.MiniEye = g.Add(64, MiniEyeTex());
                g.VeinA = g.Add(S, HoleVeins(Eye, false, 0x16041E, 0x5A1E8A));
                g.VeinB = g.Add(S, HoleVeins(Eye, true, b0: 0x7CF04A, b1: 0xE0FFC0));
                g.Venom = g.Add(P, Small(P, Drop, 0x2A8A1A, 0xB0FF6A, 0x0A2A04));
                g.Halo = g.Add(S, Halo(0xC070FF, 0x3A0A60));
                g.A1 = g.Add(S, Miasma(2.3f, 0x1A0428, 0x6A1EA8));
                g.A2 = g.Add(S, Miasma(9.1f, 0x2A0A3A, 0x9A3AE0));
                g.Particle = g.Add(P, Bubble());
                g.M1 = g.Add(P, SnakeBody());
                g.M2 = g.Add(64, SnakeHead());
                g.M3 = g.Add(256, Ouroboros());
                g.M4 = g.Add(S, Tendril(0.0f, 1f));
                g.M5 = g.Add(S, Tendril(1.7f, -1f));
                g.M6 = g.Add(S, Tendril(3.1f, 1f));
                break;

            case Tempestade:
                // Raio dourado vivo sobre uma bola com bobina de cobre; bobinas de Tesla na Lendária.
                Base(g, 0x0B1A4E, 0x3456C0, 0x050C28, Bolt, 0xE0A000, 0xFFF080, 0x6A3E00, 0x02081E, energy: 0x8AD8FF);
                g.BoltCore = g.Add(S, BoltCoreTex());
                g.EdgeSparks = new[] { g.Add(S, EdgeSparkTex(3)), g.Add(S, EdgeSparkTex(17)), g.Add(S, EdgeSparkTex(29)) };
                g.Lightning = new[] { g.Add(128, LightningTex(5)), g.Add(128, LightningTex(13)), g.Add(128, LightningTex(21)), g.Add(128, LightningTex(34)) };
                g.Tex[g.Ball] = PlasmaGlassTex();
                g.Tex[g.Gem] = BoltCrystalTex(false);
                g.Tex[g.Gem2] = BoltCrystalTex(true);
                g.PlasmaCore = g.Add(S, InnerPower(3.3f, 0x3A3AD0, 0xA8B8FF, 0xFFFFFF));
                g.Filaments = new[] { g.Add(64, FilamentTex(2)), g.Add(64, FilamentTex(9)), g.Add(64, FilamentTex(17)), g.Add(64, FilamentTex(26)) };
                g.BoltVeins = new[] { g.Add(S, BoltVeinTex(4)), g.Add(S, BoltVeinTex(11)), g.Add(S, BoltVeinTex(23)) };
                g.Tesla = g.Add(256, TeslaTex());
                g.CoilRings = new[] { g.Add(256, CoilRingGlow(0)), g.Add(256, CoilRingGlow(1)), g.Add(256, CoilRingGlow(2)) };
                g.ERing = g.Add(S, ElectricRingTex());
                g.VeinA = g.Add(S, HoleVeins(Bolt, false, 0x06143A, 0x2A5AB0));
                g.VeinB = g.Add(S, HoleVeins(Bolt, true, b0: 0x8AD8FF, b1: 0xFFFFFF));
                g.Halo = g.Add(S, Halo(0xA8DCFF, 0x2040C0));
                g.A1 = g.Add(S, Arcs(11)); g.A2 = g.Add(S, Arcs(23)); g.A3 = g.Add(S, Arcs(37)); g.A4 = g.Add(S, Arcs(53));
                break;

            case Caos:
                // Caveira de obsidiana com chifres e mandíbula rugindo (inspirada no Talzur), bola de rocha vulcânica com lava roxa.
                // Bola de energia rúnica presa por correntes de magia, sobre a bola roxa de caos.
                Base(g, 0x2E0A58, 0x8A3AD8, 0x14022A, OrbShape, 0x7A2EC4, 0x9A38F0, 0x2E0A50, 0x0E0218, energy: 0xC060FF);
                g.Tex[g.Gem] = RuneOrbTex(false);
                g.Tex[g.Gem2] = RuneOrbTex(true);
                g.OrbSwirlA = g.Add(S, OrbEnergy(1.3f));
                g.OrbSwirlB = g.Add(S, OrbEnergy(7.1f));
                g.Shine = Array.Empty<int>();                         // sem a faixa de brilho (deixava a esfera acinzentada)
                g.OrbRune = g.Add(S, GlyphTex(ChaosRune(), S, 0.022f, true, 0.6f, 0xB050FF, 0xFFF0FF));
                g.Plasma = g.Add(S, PlasmaTex());
                g.Rift = g.Add(64, RiftTex());
                g.ChaosCircle = g.Add(256, RuneCircleTex(0xA040FF, 0xF4E0FF));
                g.LinkRune = g.Add(32, GlyphTex(SmallRune(3), 32, 0.10f, false, 0f, 0xE8C8FF, 0xFFFFFF));
                g.OrbGlyphs = new int[6];
                for (int i = 0; i < 6; i++) g.OrbGlyphs[i] = g.Add(64, GlyphTex(SmallRune(i * 2 + 1), 64, 0.075f, true, 0.7f, 0xB050FF, 0xFFF0FF));
                g.MLinkFace = g.Add(64, MagicLink(true));
                g.MLinkEdge = g.Add(64, MagicLink(false));
                g.Veins = g.Add(S, ChaosSparksTex());
                g.InnerA = g.Add(S, InnerPower(4.1f, 0x5A1090, 0xC060FF, 0xFFD8FF));
                g.InnerB = g.Add(S, InnerPower(9.7f, 0x7A1060, 0xFF50C0, 0xFFD8F0));
                g.VeinA = g.Add(S, HoleVeins(OrbShape, false, 0x2A0838, 0x8A2AD0));
                g.VeinB = g.Add(S, HoleVeins(OrbShape, true, b0: 0xE070FF, b1: 0xFFD8FF));
                g.Smoke = g.Add(S, Miasma(5.3f, 0x08020E, 0x2A0E40, 0x8A3AD0));
                g.Mist = g.Add(64, PuffTex());
                g.Shock = g.Add(S, ShockRingTex());
                g.Halo = g.Add(S, Halo(0xC27CFF, 0x4A1080));
                g.A1 = g.Add(S, Fire(1.3f)); g.A2 = g.Add(S, Fire(5.9f)); g.A3 = g.Add(S, Fire(12.4f));
                g.Particle = g.Add(P, Ember());
                break;
        }
        g.MeasureExtents();
        return g;
    }

    delegate float Sd(float x, float y);
    delegate (float r, float g, float b)? GemColorFn(float x, float y, float d);

    static void Base(GemStyle g, int ballDark, int ballLight, int ballRim, Sd shape,
                     int gemDark, int gemLight, int gemEdge, int hole, GemColorFn? custom = null, int energy = -1, float gemWhite = 1f)
    {
        int en = energy >= 0 ? energy : gemLight;
        g.Ball = g.Add(S, BallTex(ballDark, ballLight, ballRim));
        g.Hole = g.Add(S, HoleTex(shape, hole, ballLight));
        g.Shadow = g.Add(S, ShadowTex(shape));
        Sd? fs = shape == (Sd)Skull ? SkullSilhouette : null;
        g.Gem = g.Add(S, CrystalTex(shape, gemDark, gemLight, gemEdge, custom, facetShape: fs, white: gemWhite));
        g.Gem2 = g.Add(S, CrystalTex(shape, gemDark, gemLight, gemEdge, custom, facets: 12f, polished: true, facetShape: fs, white: gemWhite));
        g.RingBronze = g.Add(S, Ring(0));
        g.RingSilver = g.Add(S, Ring(1));
        g.RingGold = g.Add(S, Ring(2));
        g.GemGlow = g.Add(S, GlowTex(shape, gemLight));
        g.Shard = g.Add(P, CrystalSmall(P, ShardShape, gemDark, gemLight, gemEdge));
        g.Progress = g.Add(S, ProgressRing());
        g.Cracks = g.Add(S, CracksTex(shape, en, g.Kind * 97 + 13));
        // Brilho em loop e cintilação nas pontas, para todas as joias.
        g.Shine = new int[8];
        for (int i = 0; i < 8; i++) g.Shine[i] = g.Add(S, ShineTex(shape, -1.1f + 2.2f * i / 7f));
        g.Glint = g.Add(64, GlintTex());
        FindTips(shape, out g.TipX, out g.TipY);
        g.EnergyA = g.Add(S, EnergyTex(shape, en, 3.7f + g.Kind));
        g.EnergyB = g.Add(S, EnergyTex(shape, en, 11.3f + g.Kind));
    }

    // ───────────────────────── formas (distância com sinal) ─────────────────────────

    static float Sun(float x, float y)
    {
        float d = Len(x, y) - 0.30f;
        float ang = MathF.Atan2(y, x);
        float step = MathF.Tau / 8f;
        float ra = MathF.PI / 2f + MathF.Round((ang - MathF.PI / 2f) / step) * step;
        Rot(x, y, MathF.PI / 2f - ra, out float px, out float py);
        return MathF.Min(d, Poly(px, py, -0.17f, 0.40f, 0.17f, 0.40f, 0f, 0.80f));
    }

    static float Drop(float x, float y)
    {
        float c = Len(x, y + 0.22f) - 0.46f;
        float t = Poly(x, y, -0.33f, -0.05f, 0.33f, -0.05f, 0f, 0.84f);
        return SMin(c, t, 0.12f);
    }

    static float Flake(float x, float y)
    {
        // Floco natural: hexágono fino ligando os braços pelo meio, e as pontas
        // cristalizadas saindo para fora dele.
        float d = Len(x, y) - 0.12f;
        const float hexR = 0.46f, tip = 0.86f;
        for (int k = 0; k < 6; k++)
        {
            float a = MathF.PI / 2f + k * MathF.PI / 3f;
            float dx = MathF.Cos(a), dy = MathF.Sin(a);
            // Braço principal, afinando até a ponta.
            float pax = x, pay = y, bax = dx * tip, bay = dy * tip;
            float h = Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            d = MathF.Min(d, Len(pax - bax * h, pay - bay * h) - (0.05f - 0.025f * h));
            // Ponta cristalizada: losango no fim do braço.
            Rot(x - dx * 0.80f, y - dy * 0.80f, MathF.PI / 2f - a, out float qx, out float qy);
            d = MathF.Min(d, PolyN(qx, qy, new[] { 0f, 0.12f, 0.055f, 0f, 0f, -0.08f, -0.055f, 0f }));
            // Galhos: um par dentro do hexágono, um par maior fora dele.
            foreach (var (dist, len, w) in new[] { (0.28f, 0.12f, 0.030f), (0.62f, 0.17f, 0.034f) })
            {
                float bx = dx * dist, by = dy * dist;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float ba = a + sgn * MathF.PI / 3.6f;
                    d = MathF.Min(d, Seg(x, y, bx, by, bx + MathF.Cos(ba) * len, by + MathF.Sin(ba) * len) - w);
                }
            }
            // Hexágono fino ligando este braço ao próximo.
            float a2 = a + MathF.PI / 3f;
            d = MathF.Min(d, Seg(x, y, dx * hexR, dy * hexR, MathF.Cos(a2) * hexR, MathF.Sin(a2) * hexR) - 0.028f);
        }
        return d;
    }

    /// <summary>Estrela de gelo de 8 pontas: 4 longas (cima/baixo/lados) e 4 curtas nas diagonais.</summary>
    static float IceStar(float x, float y)
    {
        var v = new float[32];
        for (int k = 0; k < 8; k++)
        {
            float a = MathF.PI / 2f + k * MathF.PI / 4f;
            float r = k % 2 == 0 ? 0.92f : 0.64f;
            v[k * 4] = MathF.Cos(a) * r; v[k * 4 + 1] = MathF.Sin(a) * r;
            float b = a + MathF.PI / 8f;
            v[k * 4 + 2] = MathF.Cos(b) * 0.34f; v[k * 4 + 3] = MathF.Sin(b) * 0.34f;
        }
        return PolyN(x, y, v);
    }

    /// <summary>Gelo escuro verdadeiro: facetas azul-marinho quase pretas, arestas e contorno ciano.</summary>
    static float[] TrueIceTex(bool polished) => Paint(S, (x, y) =>
    {
        float d = IceStar(x, y);
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float ang = MathF.Atan2(y, x);
        float step = MathF.PI / 4f;
        float k = MathF.Round((ang - MathF.PI / 2f) / step);
        float pa = MathF.PI / 2f + k * step;
        float side = MathF.Sin(ang - pa);
        float nx = -MathF.Sin(pa) * MathF.Sign(side), ny = MathF.Cos(pa) * MathF.Sign(side);
        float lit = Clamp01(0.5f + 0.5f * (nx * -0.6f + ny * 0.8f));
        var dark = Rgb(0x02060F); var mid = Rgb(0x0A1A3A); var light = Rgb(polished ? 0x2E5C9A : 0x234A80);
        var c = lit > 0.5f ? Mix(mid, light, (lit - 0.5f) * 2f) : Mix(dark, mid, lit * 2f);
        float r = Len(x, y);
        // Fraturas internas (linhas escuras finas).
        float fr = Clamp01(1f - MathF.Abs(Fbm(x * 5f + 3f, y * 5f - 2f) - 0.5f) / 0.02f);
        c = Mix(c, Rgb(0x010309), fr * 0.6f);
        float ridge = Clamp01(1f - MathF.Abs(side) * r / 0.010f) * Clamp01(r / 0.15f);
        c = Mix(c, Rgb(0x7ADFFF), ridge * 0.75f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.014f) / 0.014f);
        c = Mix(c, Rgb(0x8AE8FF), rim * 0.85f);
        float outer = Clamp01(1f - MathF.Abs(d + 0.003f) / 0.006f);
        c = Mix(c, Rgb(0x020814), outer * 0.8f);
        float sx = x + 0.12f, sy = y - 0.30f;
        float star = Clamp01(1f - Len(sx, sy) / 0.04f) + Clamp01(1f - MathF.Abs(sx) / 0.01f) * Clamp01(1f - MathF.Abs(sy) / 0.12f)
                   + Clamp01(1f - MathF.Abs(sy) / 0.01f) * Clamp01(1f - MathF.Abs(sx) / 0.12f);
        c = Mix(c, Rgb(0xE8FAFF), Clamp01(star) * (polished ? 0.9f : 0.6f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Brilho azul-claro dentro do gelo: veios e núcleo (duas sementes que se alternam).</summary>
    static float[] IceGlow(float seed, Sd shape) => Paint(S, (x, y) =>
    {
        float d = shape(x, y) + 0.03f;
        if (d > 0f) return default;
        float r = Len(x, y);
        float n = Fbm(x * 3.6f + seed, y * 3.6f - seed);
        float vein = Clamp01(1f - MathF.Abs(n - 0.5f) / 0.028f);
        float core = Clamp01(1f - r / 0.30f);
        float fade = Clamp01(-d / 0.05f);
        float a = Clamp01(vein * 0.85f + core * core * 0.7f) * fade;
        var c = Mix(Rgb(0x2A9CFF), Rgb(0xD8F8FF), Clamp01(vein * 0.6f + core * 0.5f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Geada fosca cobrindo a estrela (some quando ela é ativada).</summary>
    static float[] FrostCoatTex(Sd shape) => Paint(S, (x, y) =>
    {
        float d = shape(x, y) - 0.015f;
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float n = Fbm(x * 9f + 1f, y * 9f + 5f), n2 = Noise(x * 40f, y * 40f);
        float fringe = Clamp01((-d) / 0.04f);
        var c = Mix(Rgb(0x9AC4E8), Rgb(0xF2FAFF), n);
        float alpha = (0.35f + 0.35f * n + (n2 > 0.8f ? 0.3f : 0f)) * (0.6f + 0.4f * fringe);
        return (c.r, c.g, c.b, a * alpha);
    });

    /// <summary>Cristal de gelo escuro (base embaixo, ponta em cima) que cresce em volta da estrela.</summary>
    static float[] IceSpikeTex() => Paint(64, (x, y) =>
    {
        float d = PolyN(x, y, new[] { -0.55f, -0.95f, 0.55f, -0.95f, 0.65f, 0.25f, 0f, 0.97f, -0.65f, 0.25f });
        float a = Cover(d * 0.5f, 64);
        if (a <= 0f) return default;
        var c = x < 0f ? Mix(Rgb(0x0E2A55), Rgb(0x3A78C0), Clamp01(0.5f + y * 0.5f)) : Mix(Rgb(0x040C1E), Rgb(0x14325E), Clamp01(0.5f + y * 0.5f));
        float ridge = Clamp01(1f - MathF.Abs(x) / 0.06f) * Clamp01(y + 0.9f);
        c = Mix(c, Rgb(0xA8EEFF), ridge * 0.7f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.03f) / 0.04f);
        c = Mix(c, Rgb(0x8AE8FF), rim * 0.8f);
        return (c.r, c.g, c.b, a * 0.92f);
    });

    static float[] SnowDotTex() => Paint(32, (x, y) =>
    {
        float r = Len(x, y);
        float a = Clamp01(1f - r) * Clamp01(1f - r);
        return (0.95f, 0.98f, 1f, Clamp01(a * 1.6f));
    });

    /// <summary>Casca de gelo sobre a bola: reflexo, trincas finas e borda congelada.</summary>
    static float[] IceShellTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.95f) return default;
        float a = 0f; var c = (0.92f, 0.97f, 1f);
        float hl = Clamp01(1f - MathF.Abs(Len(x + 0.10f, y + 0.12f) - 0.70f) / 0.06f) * Clamp01((-x + y) * 1.5f - 0.2f);
        a = Math.Max(a, hl * 0.55f);
        float n = Fbm(x * 3.2f + 7f, y * 3.2f + 1f);
        float crack = Clamp01(1f - MathF.Abs(n - 0.5f) / 0.012f) * Clamp01((r - 0.35f) / 0.3f);
        a = Math.Max(a, crack * 0.55f);
        float rim = Clamp01((r - 0.80f) / 0.14f);
        a = Math.Max(a, rim * rim * 0.45f * (0.6f + 0.4f * Noise(x * 30f, y * 30f)));
        a = Math.Max(a, 0.08f);
        return (c.Item1, c.Item2, c.Item3, a * Clamp01((0.95f - r) / 0.02f));
    });

    /// <summary>Floco de neve de seis braços com brilho.</summary>
    static float[] FlakeTex() => Paint(64, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float sec = MathF.Tau / 6f;
        float aa = ang - MathF.Round(ang / sec) * sec;
        float px = MathF.Cos(aa) * r, py = MathF.Abs(MathF.Sin(aa) * r);
        float arm = Math.Max(py - 0.05f, px - 0.85f);
        float bpos = px - MathF.Round(px / 0.3f) * 0.3f;
        float branch = (px > 0.2f && px < 0.8f) ? MathF.Abs(py - MathF.Abs(bpos) * 0.0f - (px - MathF.Floor(px / 0.3f) * 0.3f) * 0.9f) - 0.035f : 9f;
        float d = MathF.Min(arm, MathF.Min(branch, r - 0.12f));
        float a = Cover(d * 0.6f, 64);
        float glow = Clamp01(1f - r) * 0.35f;
        return (0.92f, 0.98f, 1f, Math.Max(a, glow * glow));
    });

    /// <summary>Névoa gelada em faixa (rola por baixo da bola).</summary>
    static float[] MistTex() => Paint(S, (x, y) =>
    {
        float band = MathF.Exp(-Sq(y / 0.32f)) * Clamp01(1f - MathF.Abs(x) / 1.0f);
        float n = Fbm(x * 2.2f + 3f, y * 4f);
        float a = band * (0.35f + 0.65f * n) * 0.75f;
        var c = Mix(Rgb(0x9ACBF0), Rgb(0xF0FAFF), n);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Anel de geada com dentes de cristal.</summary>
    static float[] FrostRingTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float teeth = MathF.Pow(MathF.Abs(MathF.Cos(ang * 18f)), 10f);
        float ring = Clamp01(1f - MathF.Abs(r - 0.86f) / (0.018f + 0.06f * teeth));
        var c = Mix(Rgb(0x7ACFFF), Rgb(0xF0FCFF), teeth);
        return (c.r, c.g, c.b, ring * 0.85f);
    });

    /// <summary>Coroa de geleira: picos de gelo escuro em leque atrás da bola.</summary>
    static float[] GlacierTex() => Paint(256, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(x, y);          // 0 = para cima
        if (MathF.Abs(ang) > 1.55f || r < 0.25f) return default;
        float w = ang / 1.55f;                                    // -1..1
        // Picos: um alto no meio, alternando altos e baixos para os lados.
        float seg = (w + 1f) * 4.5f; int si = (int)MathF.Floor(seg); float f = seg - si;
        float hA = 0.55f + 0.38f * GemStyle.Hash(si * 7 + 3) * (1f - 0.5f * MathF.Abs(w)), hB = 0.55f + 0.38f * GemStyle.Hash((si + 1) * 7 + 3) * (1f - 0.5f * MathF.Abs(w));
        float peak = 1f - MathF.Abs(f - 0.5f) * 2f;
        float R = 0.48f + (0.20f + 0.6f * peak) * (hA * (1f - f) + hB * f) * 0.75f;
        float d = r - R;
        float a = Cover(d * 0.8f, 256) * Clamp01((1.50f - MathF.Abs(ang)) / 0.15f);
        if (a <= 0f) return default;
        bool left = f < 0.5f;
        var c = left ? Mix(Rgb(0x0A1A3A), Rgb(0x2E5C9A), Clamp01((r - 0.4f) / 0.6f)) : Mix(Rgb(0x02060F), Rgb(0x10284E), Clamp01((r - 0.4f) / 0.6f));
        float ridge = Clamp01(1f - MathF.Abs(f - 0.5f) / 0.03f) * Clamp01((r - 0.5f) / 0.2f);
        c = Mix(c, Rgb(0x8AE8FF), ridge * 0.6f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.012f) / 0.014f);
        c = Mix(c, Rgb(0x9AF0FF), rim * 0.9f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Aurora boreal: cortinas verdes-ciano-violeta ondulando.</summary>
    static float[] AuroraTex(float seed) => Paint(S, (x, y) =>
    {
        float y0 = -0.25f + 0.18f * MathF.Sin(x * 2.6f + seed) + 0.08f * MathF.Sin(x * 5.3f - seed);
        float h = y - y0;
        if (h < -0.08f) return default;
        float body = Clamp01(1f + h / 0.08f) * MathF.Exp(-Sq(Math.Max(0f, h) / 0.45f));
        float rays = 0.45f + 0.55f * Clamp01(MathF.Sin(x * 38f + Fbm(x * 3f + seed, y) * 7f) * 0.5f + 0.5f);
        float fade = Clamp01((1f - MathF.Abs(x)) / 0.35f);
        float a = body * rays * fade * 0.75f;
        var c = Mix(Rgb(0x3CFFB0), Rgb(0x60C8FF), Clamp01(h / 0.35f));
        c = Mix(c, Rgb(0xA078FF), Clamp01((h - 0.35f) / 0.3f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>
    /// Asa de morcego de gelo (side = -1 esquerda, 1 direita), com anatomia de verdade: braço, cotovelo,
    /// pulso com garra, dedos longos de tamanhos diferentes e membrana côncava, rasgada e trincada.
    /// </summary>
    static float[] IceBatWing(float side)
    {
        (float x, float y) sh = (-0.86f, -0.10f), el = (-0.42f, 0.38f), wr = (-0.02f, 0.58f);
        var tips = new (float x, float y)[] { (0.92f, 0.86f), (0.96f, 0.22f), (0.62f, -0.30f), (0.10f, -0.58f) };
        (float x, float y) body = (-0.84f, -0.52f);
        // Contorno da membrana: pulso, pontas dos dedos com recortes côncavos entre elas, até o corpo.
        var poly = new List<float> { wr.x, wr.y, tips[0].x, tips[0].y };
        for (int k = 0; k + 1 < tips.Length; k++)
        {
            float mx = (tips[k].x + tips[k + 1].x) * 0.5f, my = (tips[k].y + tips[k + 1].y) * 0.5f;
            float pull = k == 0 ? 0.30f : 0.36f;
            poly.Add(mx + (wr.x - mx) * pull); poly.Add(my + (wr.y - my) * pull);
            poly.Add(tips[k + 1].x); poly.Add(tips[k + 1].y);
        }
        float bx = (tips[^1].x + body.x) * 0.5f, by = (tips[^1].y + body.y) * 0.5f;
        poly.Add(bx + 0.10f); poly.Add(by + 0.22f);
        poly.Add(body.x); poly.Add(body.y);
        poly.Add(sh.x); poly.Add(sh.y);
        poly.Add(el.x); poly.Add(el.y);
        var pv = poly.ToArray();
        return Paint(256, (x0, y) =>
        {
            float x = x0 * side;
            float bone = MathF.Min(Seg(x, y, sh.x, sh.y, el.x, el.y) - 0.055f, Seg(x, y, el.x, el.y, wr.x, wr.y) - 0.045f);
            for (int k = 0; k < tips.Length; k++)
            {
                float l = Len(x - wr.x, y - wr.y) / Len(tips[k].x - wr.x, tips[k].y - wr.y);
                bone = MathF.Min(bone, Seg(x, y, wr.x, wr.y, tips[k].x, tips[k].y) - 0.032f * (1f - 0.7f * Clamp01(l)));
            }
            float claw = Poly(x, y, wr.x - 0.05f, wr.y, wr.x + 0.04f, wr.y + 0.02f, wr.x - 0.10f, wr.y + 0.22f);   // garra do polegar
            float joint = MathF.Min(Len(x - el.x, y - el.y) - 0.07f, Len(x - wr.x, y - wr.y) - 0.06f);
            bone = MathF.Min(MathF.Min(bone, claw), joint);
            // Membrana com borda irregular, furos e rasgos.
            float mem = PolyN(x, y, pv) + (Fbm(x * 7f + 3f, y * 7f) - 0.5f) * 0.05f;
            float holeN = Fbm(x * 3.3f + 11f, y * 3.3f - 4f);
            float hole = (0.70f - holeN) * 0.6f;                       // < 0 = furo
            float tear = MathF.Abs(Fbm(x * 2.2f - 7f, y * 5f) - 0.5f) - 0.012f;
            float memD = MathF.Max(mem, -MathF.Min(hole, tear * 3f));
            float am = Cover(memD * 0.5f, 256);
            float ab = Cover(bone * 0.5f, 256);
            if (am <= 0f && ab <= 0f) return default;
            // Membrana translúcida azul mais forte, veios e trincas claras; borda (também dos furos) clara.
            var c = Mix(Rgb(0x2E6AB4), Rgb(0x8CC0F0), Clamp01(Fbm(x * 4f, y * 4f) * 1.2f - 0.1f));
            float crack = Clamp01(1f - MathF.Abs(Fbm(x * 6f + 2f, y * 6f) - 0.5f) / 0.015f);
            c = Mix(c, Rgb(0xE8F6FF), crack * 0.65f);
            float rim = Clamp01(1f - MathF.Abs(memD + 0.01f) / 0.016f);
            c = Mix(c, Rgb(0xDDF0FF), rim * 0.85f);
            float a = am * (0.68f + 0.3f * rim);
            if (ab > 0f)
            {
                var bc = Mix(Rgb(0x081A3C), Rgb(0x1E4C86), Clamp01(0.5f + y * 0.6f));
                float brim = Clamp01(1f - MathF.Abs(bone + 0.012f) / 0.014f);
                bc = Mix(bc, Rgb(0x9AD4FF), brim * 0.8f);
                c = Mix(c, bc, ab); a = Math.Max(a, ab);
            }
            return (c.r, c.g, c.b, a);
        });
    }

    /// <summary>Vulto nítido do vampiro (pisca por cima do bloco a cada batida).</summary>
    static float[] VampSharpTex() => Paint(S, (x, y) =>
    {
        float vs = VampSilhouette(x, y + 0.05f);
        float a = Clamp01(0.5f - vs / 0.012f);
        if (a <= 0f) return default;
        return (0.04f, 0.08f, 0.16f, a * 0.85f);
    });

    /// <summary>Mandíbula de gelo: gengiva de gelo escuro em arco, dois caninos grandes e curvos e dentes menores.</summary>
    static float[] JawTex(bool upper) => Paint(256, (x, y0) =>
    {
        float y = upper ? y0 : -y0;                                    // desenhada como a de cima; a de baixo é espelhada
        const float cyc = -0.98f, r0 = 1.13f, r1 = 1.27f;
        float r = Len(x, y - cyc), ang = MathF.Atan2(x, y - cyc);      // 0 = topo do arco
        float gum = MathF.Max(MathF.Max(r0 - r, r - r1 + 0.05f * Sq(ang / 0.8f)), MathF.Abs(ang) - 0.80f);
        gum = MathF.Max(gum, -(r - r0 - 0.04f * MathF.Abs(MathF.Sin(ang * 22f))));   // borda de baixo serrilhada
        float teeth = 9f;
        float[] ta = upper ? new[] { -0.70f, -0.52f, -0.34f, -0.17f, 0f, 0.17f, 0.34f, 0.52f, 0.70f } : new[] { -0.62f, -0.42f, -0.21f, 0f, 0.21f, 0.42f, 0.62f };
        for (int k = 0; k < ta.Length; k++)
        {
            bool canine = upper ? (k == 2 || k == 6) : (k == 1 || k == 5);
            float len = canine ? (upper ? 0.55f : 0.36f) : (upper ? 0.20f : 0.16f);
            float wid = canine ? 0.085f : 0.050f;
            // Eixo do dente: da gengiva para dentro do arco, com curva para o meio.
            float bx = MathF.Sin(ta[k]) * r0, by = cyc + MathF.Cos(ta[k]) * r0;
            float dx = -MathF.Sin(ta[k]), dy = -MathF.Cos(ta[k]);
            float px = x - bx, py = y - by;
            float along = px * dx + py * dy, side = px * -dy + py * dx;
            float u = along / len;
            if (u < -0.05f || u > 1f) continue;
            float bend = (canine ? 0.10f : 0.03f) * u * u * MathF.Sign(-ta[k] + 1e-4f);
            float w = wid * (1f - u) + 0.006f;
            teeth = MathF.Min(teeth, MathF.Abs(side - bend) - w);
        }
        float aGum = Cover(gum * 0.5f, 256), aT = Cover(teeth * 0.5f, 256);
        if (aGum <= 0f && aT <= 0f) return default;
        var gc = Mix(Rgb(0x081A3C), Rgb(0x24508C), Clamp01((r - r0) / (r1 - r0)));
        float grim = Clamp01(1f - MathF.Abs(gum + 0.012f) / 0.014f);
        gc = Mix(gc, Rgb(0x8AD0FF), grim * 0.8f);
        var tc = Mix(Rgb(0xEAF8FF), Rgb(0x7AB4E8), Clamp01(0.5f + teeth * -6f));
        float trim = Clamp01(1f - MathF.Abs(teeth + 0.008f) / 0.010f);
        tc = Mix(tc, Rgb(0x2A5C96), trim * 0.6f);
        var c = aT > 0f ? Mix(gc, tc, aT) : gc;
        return (c.r, c.g, c.b, Math.Max(aGum, aT));
    });

    /// <summary>Cristais de geada crescendo da borda da bola para dentro.</summary>
    static float[] FrostRimTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        if (r > 0.93f || r < 0.55f) return default;
        float spikes = MathF.Pow(MathF.Abs(MathF.Cos(ang * 13f + Fbm(x * 3f, y * 3f) * 2f)), 6f);
        float reach = 0.93f - (0.08f + 0.22f * spikes);
        float a = Clamp01((r - reach) / 0.04f) * Clamp01((0.93f - r) / 0.02f);
        float fern = Clamp01(1f - MathF.Abs(Fbm(x * 9f + 1f, y * 9f) - 0.5f) / 0.05f);
        a *= 0.55f + 0.45f * fern;
        return (0.92f, 0.98f, 1f, a * 0.85f);
    });

    // ───────── Tempestade: raio vivo, bobinas ─────────

    /// <summary>Vidro escuro do globo de plasma: azul-noite com reflexo e borda clara.</summary>
    static float[] PlasmaGlassTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float d = r - 0.94f;
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float k = r / 0.94f;
        var c = Mix(Rgb(0x0A0A2A), Rgb(0x1A1A4A), k * k);
        c = Mix(c, Rgb(0x8A9AFF), Clamp01((k - 0.88f) / 0.10f) * 0.6f);
        float hl = Clamp01(1f - MathF.Abs(Len(x + 0.08f, y + 0.08f) - 0.70f) / 0.05f) * Clamp01((-x + y) * 1.6f - 0.3f);
        c = Mix(c, Rgb(0xE8F0FF), hl * 0.55f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.01f) / 0.014f);
        c = Mix(c, Rgb(0x04041A), rim * 0.8f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Filamento de plasma (base no centro embaixo, ponta no vidro em cima com um ponto aceso).</summary>
    static float[] FilamentTex(int seed) => Paint(64, (x, y) =>
    {
        float u = Clamp01((y + 0.95f) / 1.9f);
        float wob = 0.30f * (Fbm(u * 3f + seed, seed * 0.37f) - 0.5f) * 2f * MathF.Sin(u * MathF.PI);
        float d = MathF.Abs(x - wob);
        float w = 0.16f + 0.12f * (1f - u);
        float core = Clamp01(1f - d / w);
        float glow = MathF.Exp(-d / 0.30f) * 0.6f;
        float tip = Clamp01(1f - Len(x - wob, (y - 0.86f) * 1.2f) / 0.16f);
        var c = Mix(Rgb(0x8A5AFF), Rgb(0xF0F0FF), Math.Max(core, tip));
        return (c.r, c.g, c.b, Clamp01(Math.Max(Math.Max(core, glow), tip)) * Clamp01(u * 5f));
    });

    /// <summary>Raio de cristal dourado facetado e translúcido.</summary>
    static float[] BoltCrystalTex(bool polished)
    {
        var fx = new[] { 0.28f, 0.10f, 0.30f, -0.05f, -0.18f, 0.05f, -0.15f };
        var fy = new[] { 0.70f, 0.45f, 0.20f, 0.12f, -0.30f, -0.15f, -0.65f };
        var fl = new[] { 0.95f, 0.45f, 0.75f, 0.30f, 0.85f, 0.55f, 0.40f };
        return Paint(S, (x, y) =>
        {
            float d = Bolt(x, y);
            float a = Cover(d, S);
            if (a <= 0f) return default;
            int best = 0; float b1 = 9f, b2 = 9f;
            for (int k = 0; k < fx.Length; k++)
            {
                float q = Len(x - fx[k], y - fy[k]);
                if (q < b1) { b2 = b1; b1 = q; best = k; } else if (q < b2) b2 = q;
            }
            float lit = fl[best];
            var c = Mix(Rgb(0x8A5A08), Rgb(polished ? 0xFFF0A0 : 0xF0D060), lit);
            float alpha = 0.78f + 0.15f * lit;
            float edge = Clamp01(1f - (b2 - b1) / 0.016f);
            c = Mix(c, Rgb(0xFFF8D8), edge * 0.6f);
            float rim = Clamp01(1f - MathF.Abs(d + 0.010f) / 0.012f);
            c = Mix(c, Rgb(0x4A2A00), rim * 0.85f);
            alpha = Math.Max(alpha, rim);
            float sx = x - 0.20f, sy = y - 0.62f;
            float star = Clamp01(1f - Len(sx, sy) / 0.035f) + Clamp01(1f - MathF.Abs(sx) / 0.009f) * Clamp01(1f - MathF.Abs(sy) / 0.10f)
                       + Clamp01(1f - MathF.Abs(sy) / 0.009f) * Clamp01(1f - MathF.Abs(sx) / 0.10f);
            c = Mix(c, (1f, 1f, 1f), Clamp01(star) * (polished ? 0.9f : 0.6f));
            return (c.r, c.g, c.b, a * Clamp01(alpha));
        });
    }

    /// <summary>Eletricidade dentro do raio: linhas de ruído finas, só no interior.</summary>
    static float[] BoltVeinTex(int seed) => Paint(S, (x, y) =>
    {
        float d = Bolt(x, y);
        if (d > -0.025f) return default;
        float n = Fbm(x * 5f + seed, y * 5f - seed * 0.5f);
        float line = MathF.Abs(n - 0.5f);
        float core = Clamp01(1f - line / 0.012f);
        float glow = MathF.Exp(-line / 0.04f) * 0.5f;
        float a = Clamp01(Math.Max(core, glow)) * Clamp01((-d - 0.025f) / 0.03f);
        var c = Mix(Rgb(0x6AC8FF), Rgb(0xFFFFFF), core);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Núcleo do raio: brilho branco-azulado por dentro da forma.</summary>
    static float[] BoltCoreTex() => Paint(S, (x, y) =>
    {
        float d = Bolt(x, y);
        if (d > 0f) return default;
        float k = Clamp01(-d / 0.10f);
        var c = Mix(Rgb(0x6AC8FF), Rgb(0xFFFFFF), k);
        return (c.r, c.g, c.b, Clamp01(k * 1.2f) * 0.9f);
    });

    /// <summary>Eletricidade correndo pelas bordas do raio (zigue-zague fino com brilho).</summary>
    static float[] EdgeSparkTex(int seed) => Paint(S, (x, y) =>
    {
        float d = Bolt(x, y);
        float n = (Noise(x * 18f + seed, y * 18f - seed) - 0.5f) * 0.06f;
        float line = MathF.Abs(d - 0.02f + n);
        float core = Clamp01(1f - line / 0.008f);
        float glow = MathF.Exp(-line / 0.03f) * 0.5f;
        float gate = Clamp01((Noise(x * 4f + seed * 0.3f, y * 4f) - 0.40f) * 4f);      // só em trechos
        float a = Clamp01(Math.Max(core, glow)) * gate;
        if (a <= 0f) return default;
        var c = Mix(Rgb(0x5AB8FF), Rgb(0xFFFFFF), core);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Relâmpago vertical (de baixo para cima), com galhos; esticado entre dois pontos ao desenhar.</summary>
    static float[] LightningTex(int seed)
    {
        var rng = new Random(seed);
        var pts = new List<(float x, float y)>();
        float px = 0f;
        for (int i = 0; i <= 12; i++)
        {
            float y = -0.95f + 1.9f * i / 12f;
            if (i > 0 && i < 12) px = Math.Clamp(px + ((float)rng.NextDouble() - 0.5f) * 0.55f, -0.55f, 0.55f);
            else px = 0f;
            pts.Add((px, y));
        }
        var branches = new List<(float, float, float, float)>();
        for (int b = 0; b < 3; b++)
        {
            int i = 3 + rng.Next(6);
            float bx = pts[i].x + ((float)rng.NextDouble() - 0.5f) * 0.9f, by = pts[i].y + 0.18f;
            branches.Add((pts[i].x, pts[i].y, bx, by));
        }
        return Paint(128, (x, y) =>
        {
            float d = 9f;
            for (int i = 0; i + 1 < pts.Count; i++) d = MathF.Min(d, Seg(x, y, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y));
            float db = 9f;
            foreach (var (ax, ay, bx, by) in branches) db = MathF.Min(db, Seg(x, y, ax, ay, bx, by));
            float core = Clamp01(1f - (d - 0.03f) / 0.04f);
            float bcore = Clamp01(1f - (db - 0.015f) / 0.03f) * 0.8f;
            float glow = MathF.Exp(-d / 0.18f) * 0.6f + MathF.Exp(-db / 0.10f) * 0.3f;
            float a = Clamp01(Math.Max(Math.Max(core, bcore), glow));
            var c = Mix(Rgb(0x4AA8FF), Rgb(0xFFFFFF), Math.Max(core, bcore));
            return (c.r, c.g, c.b, a);
        });
    }

    /// <summary>Bobina de cobre enrolada no meio da bola (faixa de espiras com sombreado cilíndrico).</summary>
    static float[] CoilBandTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.94f || MathF.Abs(y + 0.05f) > 0.17f) return default;
        float half = MathF.Sqrt(Math.Max(0f, 0.94f * 0.94f - y * y));
        float u = x / Math.Max(half, 0.01f);                         // -1..1 através da bola (cilindro)
        float shade = MathF.Sqrt(Math.Max(0f, 1f - u * u));
        float wire = 0.5f + 0.5f * MathF.Cos(y * 140f);
        var c = Mix(Rgb(0x4A2408), Rgb(0xE08A40), shade * (0.55f + 0.45f * wire));
        c = Mix(c, Rgb(0xFFE0B0), MathF.Pow(Clamp01(shade - 0.2f + u * -0.3f), 6f) * wire * 0.8f);
        float edge = Clamp01((0.17f - MathF.Abs(y + 0.05f)) / 0.015f);
        return (c.r, c.g, c.b, edge * Clamp01((0.94f - r) / 0.02f));
    });

    /// <summary>Brilho azul entre as espiras da bobina (acende com a carga).</summary>
    static float[] CoilGlowTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.94f || MathF.Abs(y + 0.05f) > 0.19f) return default;
        float gap = MathF.Pow(0.5f - 0.5f * MathF.Cos(y * 140f), 4f);
        float band = Clamp01((0.19f - MathF.Abs(y + 0.05f)) / 0.04f);
        var c = Mix(Rgb(0x4AB0FF), Rgb(0xE8F8FF), gap);
        return (c.r, c.g, c.b, gap * band * 0.9f * Clamp01((0.94f - r) / 0.02f));
    });

    // Antena de bobina: haste de metal com esfera no topo e anéis em volta, menores para cima.
    static readonly float[] CoilY = { -0.58f, -0.08f, 0.34f }, CoilR = { 0.88f, 0.64f, 0.42f };
    const float CoilK = 0.36f, CoilTube = 0.06f, CoilH = 0.09f;

    static float[] TeslaTex() => Paint(256, (x, y) =>
    {
        float rod = Box(x, y + 0.12f, 0.035f, 0.82f);
        float ball = (Len(x / 0.17f, (y - 0.80f) / 0.08f) - 1f) * 0.08f;
        float aRod = Cover(MathF.Min(rod, ball) * 0.5f, 256);
        (float r, float g, float b) cRod;
        if (ball < 0f)
        {
            float sh = Clamp01(1f - Len((x + 0.05f) / 0.17f, (y - 0.82f) / 0.08f));
            cRod = Mix(Rgb(0x3A4250), Rgb(0xF4F8FF), sh * sh);
        }
        else cRod = Mix(Rgb(0x4A5468), Rgb(0xE0E8F4), Clamp01(1f - MathF.Abs(x / 0.04f + 0.35f)));
        // Anéis com espessura (cilindros curtos): parede de dentro ciano visível atrás,
        // metal prateado na frente e aro claro em cima. Metade de trás atrás da haste.
        (float r, float g, float b) cBack = default, cFront = default; float aBack = 0f, aFront = 0f;
        for (int k = 0; k < CoilY.Length; k++)
        {
            float R = CoilR[k];
            for (int st = 0; st <= 6; st++)
            {
                float hh = CoilH * R * st / 6f;
                float yy = (y - CoilY[k] + hh) / CoilK;
                float q = Len(x, yy) - R;
                float d = (MathF.Abs(q) - CoilTube) * CoilK * 1.4f;
                float a = Cover(d * 0.5f, 256);
                if (a <= 0f) continue;
                bool back = yy > 0f;
                float top = st == 0 ? 1f : 0f;
                float u = Clamp01(MathF.Abs(x) / R);
                (float r, float g, float b) c = back
                    ? Mix(Rgb(0x0E6A80), Rgb(0x48F0FF), Clamp01(0.3f + 0.7f * (1f - u)))              // face interna ciano
                    : Mix(Rgb(0x5A6274), Rgb(0xF2F6FC), Clamp01(0.85f - u * 0.7f + (x < 0 ? 0.15f : -0.1f)));   // metal
                if (top > 0f) c = Mix(c, Rgb(0xFFFFFF), 0.55f);
                float rim = Clamp01(1f - MathF.Abs(d + 0.005f) / 0.007f);
                c = Mix(c, Rgb(0x1A2030), rim * 0.6f);
                if (back) { if (a >= aBack) { cBack = c; aBack = a; } }
                else if (a >= aFront || st == 0) { cFront = c; aFront = Math.Max(aFront, a); }
            }
        }
        float A = aBack; var C = cBack;
        if (aRod > 0f) { C = A > 0f ? Mix(C, cRod, aRod) : cRod; A = Math.Max(A, aRod); }
        if (aFront > 0f) { C = A > 0f ? Mix(C, cFront, aFront) : cFront; A = Math.Max(A, aFront); }
        return A <= 0f ? default : (C.r, C.g, C.b, A);
    });

    /// <summary>Brilho ciano de um anel da bobina (acende em sequência, subindo).</summary>
    static float[] CoilRingGlow(int k) => Paint(256, (x, y) =>
    {
        float q = Len(x, (y - CoilY[k]) / CoilK) - CoilR[k];
        float d = MathF.Abs(q);
        float core = Clamp01(1f - d / (CoilTube * 0.7f));
        float glow = MathF.Exp(-d / 0.10f) * 0.6f;
        var c = Mix(Rgb(0x30D8F0), Rgb(0xF0FFFF), core);
        return (c.r, c.g, c.b, Clamp01(Math.Max(core * 0.9f, glow)));
    });

    /// <summary>Onda de choque elétrica (anel azul irregular).</summary>
    static float[] ElectricRingTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float wob = 0.03f * MathF.Sin(ang * 23f) + 0.02f * MathF.Sin(ang * 51f);
        float line = MathF.Abs(r - 0.86f - wob);
        float core = Clamp01(1f - line / 0.012f);
        float glow = MathF.Exp(-line / 0.05f) * 0.5f;
        var c = Mix(Rgb(0x4AA8FF), Rgb(0xFFFFFF), core);
        return (c.r, c.g, c.b, Clamp01(Math.Max(core, glow)));
    });

    // ───────── Caos: bola de energia rúnica ─────────

    static float OrbShape(float x, float y) => Len(x, y) - 0.62f;

    /// <summary>Esfera de energia: interior escuro, borda violeta acesa e reflexo.</summary>
    static float[] RuneOrbTex(bool polished) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float d = r - 0.62f;
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float k = r / 0.62f;
        var c = Mix(Rgb(0x06010F), Rgb(0x2A0858), k * k);
        c = Mix(c, Rgb(polished ? 0xC88AFF : 0xA868F0), Clamp01((k - 0.84f) / 0.14f));
        float rim = Clamp01(1f - MathF.Abs(d + 0.006f) / 0.008f);
        c = Mix(c, Rgb(0x2A0850), rim * 0.8f);
        float hl = Clamp01(1f - MathF.Abs(Len(x + 0.05f, y + 0.05f) - 0.48f) / 0.04f) * Clamp01((-x + y) * 2f - 0.4f);
        c = Mix(c, Rgb(0xF4E8FF), hl * (polished ? 0.45f : 0.3f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Energia da esfera: redemoinho de fumaça de luz violeta, recortado no círculo.</summary>
    static float[] OrbEnergy(float seed) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.60f) return default;
        float ang = MathF.Atan2(y, x), sw = ang + r * 4.5f;
        float n = Fbm(MathF.Cos(sw) * (1.4f + r * 3f) + seed, MathF.Sin(sw) * (1.4f + r * 3f) - seed);
        float k = Clamp01((n - 0.40f) * 2.6f);
        float edge = Clamp01((0.60f - r) / 0.10f);
        var c = Mix(Rgb(0x5A14C8), Rgb(0xD898FF), Clamp01(k * 1.3f - 0.2f));
        c = Mix(c, Rgb(0xFFF0FF), Clamp01(k - 0.85f) * 3f);
        return (c.r, c.g, c.b, Clamp01(k * 1.1f) * edge);
    });

    /// <summary>Fenda no espaço: rasgo escuro em lente, com borda violeta acesa e serrilhada.</summary>
    static float[] RiftTex() => Paint(64, (x, y) =>
    {
        float jag = 0.06f * MathF.Sin(x * 23f) + 0.04f * MathF.Sin(x * 41f + 1f);
        float h = 0.42f * Clamp01(1f - x * x) + jag * (1f - x * x);
        float d = MathF.Abs(y) - h;
        float core = Cover(d * 0.6f, 64);
        float rim = Clamp01(1f - MathF.Abs(d + 0.02f) / 0.10f);
        float glow = MathF.Exp(-Math.Max(d, 0f) / 0.22f) * 0.8f * Clamp01(1f - MathF.Abs(x));
        if (core <= 0f && rim <= 0f && glow <= 0.01f) return default;
        var c = Mix(Rgb(0x10001E), Rgb(0xC060FF), Math.Max(rim, glow * (1f - core)));
        c = Mix(c, Rgb(0xFFE8FF), Clamp01(rim - 0.6f) * 2f);
        return (c.r, c.g, c.b, Clamp01(Math.Max(core, Math.Max(rim, glow))));
    });

    /// <summary>Núcleo de plasma: brilho violeta com filamentos, mais claro no centro.</summary>
    static float[] PlasmaTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.62f) return default;
        float n = Fbm(x * 3.5f + 2f, y * 3.5f - 1f);
        float fil = Clamp01(1f - MathF.Abs(n - 0.5f) / 0.06f);
        float core = Clamp01(1f - r / 0.55f);
        float a = Clamp01(core * core * 0.9f + fil * 0.6f * core) * Clamp01((0.62f - r) / 0.08f);
        var c = Mix(Rgb(0x7A28E0), Rgb(0xE8B8FF), Clamp01(core * core + fil * 0.4f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Redemoinho de energia dentro da esfera (braços em espiral).</summary>
    static float[] OrbSwirlTex(float seed, float twist) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.60f) return default;
        float ang = MathF.Atan2(y, x);
        float sp = ang + r * twist * 3f;
        float arms = MathF.Pow(Math.Max(0f, MathF.Sin(sp * 2f + seed)), 4f);
        float n = Fbm(x * 4f + seed, y * 4f);
        float a = arms * (0.5f + 0.5f * n) * Clamp01((0.60f - r) / 0.08f) * Clamp01(r / 0.15f);
        var c = Mix(Rgb(0x8A3AF0), Rgb(0xF0D8FF), arms);
        return (c.r, c.g, c.b, Clamp01(a * 1.3f));
    });

    /// <summary>Runa do caos original: haste com gancho em cima, dois cortes cruzados e três pontos.</summary>
    static List<(float, float, float, float)> ChaosRune()
    {
        var l = new List<(float, float, float, float)>
        {
            (0f, -0.36f, 0f, 0.28f),
            (-0.20f, 0.10f, 0.18f, -0.14f), (0.20f, 0.10f, -0.18f, -0.14f),
            (-0.10f, -0.36f, 0.10f, -0.36f),
        };
        Arc(l, 0.10f, 0.28f, 0.10f, 180f, 20f, 8);           // gancho no alto
        foreach (var (px, py) in new[] { (-0.24f, 0.24f), (0.24f, 0.24f), (0f, 0.46f) })
            l.Add((px - 0.01f, py, px + 0.01f, py));
        return l;
    }

    /// <summary>Elo de corrente de magia (luz violeta: núcleo claro e brilho em volta).</summary>
    static float[] MagicLink(bool face) => Paint(64, (x, y) =>
    {
        float d = face
            ? MathF.Abs(Len(x / 1.0f, y / 0.68f) * 0.68f - 0.46f) - 0.12f
            : Box(x, y, 0.80f, 0.30f) - 0.12f;
        float core = Cover(d * 0.5f, 64);
        float glow = MathF.Exp(-Math.Max(d, 0f) / 0.10f) * 0.6f;
        var c = Mix(Rgb(0x9A40FF), Rgb(0xF8E8FF), Clamp01(-d / 0.08f));
        return (c.r, c.g, c.b, Clamp01(Math.Max(core, glow)));
    });

    // ───────── Caos: gárgula demoníaca (criatura original) ─────────

    static float GarHead(float x, float y)
    {
        float ax = MathF.Abs(x);
        float skull = Len(x / 0.44f, (y - 0.10f) / 0.40f) - 1f; skull *= 0.40f;          // cabeça arredondada
        float cheek = Box(x, y + 0.18f, 0.44f, 0.12f) - 0.08f;                         // maçãs salientes
        float ears = Poly(ax, y, 0.40f, 0.26f, 0.44f, -0.02f, 0.82f, 0.42f);           // orelhas pontudas para fora
        return MathF.Min(SMin(skull, cheek, 0.10f), ears);
    }

    static float GarJaw(float x, float y)
        => SMin(Box(x, y + 0.48f, 0.30f, 0.10f) - 0.08f, Len(x / 1.6f, y + 0.62f) - 0.10f, 0.08f);

    /// <summary>Forma completa (cabeça + mandíbula fechada) para buraco, sombra e brilho.</summary>
    static float GargoyleShape(float x, float y) => SMin(MathF.Max(GarHead(x, y), -(y + 0.36f)), GarJaw(x, y), 0.06f);

    /// <summary>Pedra roxa escura entalhada: volume, granulado, trincas e arestas de cinzel.</summary>
    static (float r, float g, float b) GarStone(float x, float y, float d, bool polished)
    {
        float k = Clamp01(-d / 0.20f);
        float lit = Clamp01(0.40f + 0.35f * (-x * 0.6f + y * 0.5f) + 0.30f * k);
        var c = Mix(Rgb(0x12081C), Rgb(polished ? 0x6A4A8E : 0x54386F), lit);
        float grain = Noise(x * 40f, y * 40f);
        c = Mix(c, Rgb(0x0A0410), (grain - 0.5f) * 0.25f + 0.1f);
        float crack = Clamp01(1f - MathF.Abs(Fbm(x * 4f + 9f, y * 4f - 3f) - 0.5f) / 0.012f) * Clamp01(-d / 0.05f);
        c = Mix(c, Rgb(0x06020A), crack * 0.7f);
        return c;
    }

    /// <summary>Rosto da gárgula (até a linha da boca): testa franzida em V, órbitas fundas, focinho, presas de cima.</summary>
    static float[] GargoyleTex(bool polished) => Paint(S, (x, y) =>
    {
        float ax = MathF.Abs(x);
        float d = MathF.Max(GarHead(x, y), -(y + 0.36f));
        // Presas de cima (saem da linha da boca para baixo).
        float fang = MathF.Min(Poly(ax, y, 0.14f, -0.34f, 0.22f, -0.34f, 0.17f, -0.54f), Poly(ax, y, 0.02f, -0.34f, 0.08f, -0.34f, 0.05f, -0.42f));
        float aF = Clamp01(0.5f - fang / 0.008f);
        float a = Cover(d, S);
        if (a <= 0f && aF <= 0f) return default;
        var c = GarStone(x, y, d, polished);
        // Sobrancelha pesada em V (testa franzida): faixa clara em cima, sombra embaixo.
        float browY = 0.20f - 0.22f * MathF.Max(0f, 0.30f - ax) / 0.30f * 0.6f - 0.10f * Clamp01(ax / 0.3f);
        float brow = y - (0.17f + 0.20f * Clamp01((0.30f - ax) / 0.30f) * -0.4f - 0.05f * ax);
        float browBand = Clamp01(1f - MathF.Abs(brow - 0.03f) / 0.035f) * Clamp01((0.40f - ax) / 0.08f);
        c = Mix(c, Rgb(0x8A6AB0), browBand * 0.45f);
        // Órbitas: amêndoas inclinadas para baixo no meio (cara de bravo), bem escuras.
        float ex = ax - 0.17f, ey = y - 0.06f - (ax - 0.17f) * 0.35f;
        float eye = Len(ex / 0.12f, ey / 0.055f) - 1f;
        if (eye < 0.15f) c = Mix(c, Rgb(0x04000A), Clamp01(0.5f - eye / 0.15f) * 0.95f);
        // Focinho largo com narinas.
        float snout = Box(x, y + 0.14f, 0.12f, 0.08f) - 0.05f;
        c = Mix(c, Rgb(0x6A4A8C), Clamp01(1f - MathF.Abs(snout + 0.02f) / 0.02f) * 0.4f);
        float nost = Len((ax - 0.055f) / 0.035f, (y + 0.17f) / 0.022f) - 1f;
        if (nost < 0f) c = Mix(c, Rgb(0x040008), 0.95f);
        // Rugas do rosnado ao lado do focinho.
        float wr = Clamp01(1f - MathF.Abs(Len(ax - 0.30f, y + 0.08f) - 0.14f) / 0.012f) * Clamp01((0.30f - ax) / 0.1f) * Clamp01((y + 0.30f) / 0.1f);
        c = Mix(c, Rgb(0x08040E), wr * 0.6f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.008f) / 0.010f);
        c = Mix(c, Rgb(0x05010A), rim * 0.9f);
        var tc = Mix(Rgb(0xEDE4D0), Rgb(0x9C8C70), Clamp01((-0.34f - y) / 0.2f));
        if (aF > 0f) c = a > 0f ? Mix(c, tc, aF) : tc;
        return (c.r, c.g, c.b, Math.Max(a, aF));
    });

    /// <summary>Mandíbula da gárgula (desce quando ela rosna/ruge), com presas para cima.</summary>
    static float[] GargoyleJawTex() => Paint(S, (x, y) =>
    {
        float ax = MathF.Abs(x);
        float d = MathF.Max(GarJaw(x, y), y + 0.36f);
        float fang = MathF.Min(Poly(ax, y, 0.20f, -0.40f, 0.27f, -0.40f, 0.24f, -0.27f), Poly(ax, y, 0.06f, -0.40f, 0.12f, -0.40f, 0.09f, -0.33f));
        float aF = Clamp01(0.5f - fang / 0.008f);
        float a = Cover(d, S);
        if (a <= 0f && aF <= 0f) return default;
        var c = GarStone(x, y + 0.3f, d, false);
        float rim = Clamp01(1f - MathF.Abs(d + 0.008f) / 0.010f);
        c = Mix(c, Rgb(0x05010A), rim * 0.9f);
        var tc = Mix(Rgb(0xEDE4D0), Rgb(0x9C8C70), Clamp01((y + 0.40f) / 0.13f));
        if (aF > 0f) c = a > 0f ? Mix(c, tc, aF) : tc;
        return (c.r, c.g, c.b, Math.Max(a, aF));
    });

    /// <summary>Chifres de pedra saindo do alto da cabeça e varrendo para trás e para fora.</summary>
    static float[] GargoyleHornsTex() => Paint(S, (x, y) =>
    {
        float ax = MathF.Abs(x);
        float best = 9f, bu = 0f;
        for (int i = 0; i < 14; i++)
        {
            float u0 = i / 14f, u1 = (i + 1) / 14f;
            float x0 = 0.18f + 0.40f * MathF.Sin(u0 * 1.9f), y0 = 0.36f + 0.50f * u0 * u0 + 0.10f * MathF.Sin(u0 * 3f);
            float x1 = 0.18f + 0.40f * MathF.Sin(u1 * 1.9f), y1 = 0.36f + 0.50f * u1 * u1 + 0.10f * MathF.Sin(u1 * 3f);
            float dd = Seg(ax, y, x0, y0, x1, y1) - (0.10f * (1f - u0) + 0.012f);
            if (dd < best) { best = dd; bu = u0; }
        }
        float a = Cover(best * 0.6f, S);
        if (a <= 0f) return default;
        var c = Mix(Rgb(0x2A1A36), Rgb(0xB8A0D0), Clamp01(0.2f + bu * 0.8f));
        float ring = 0.5f + 0.5f * MathF.Sin(bu * 60f);
        c = Mix(c, Rgb(0x0A0410), ring * 0.3f * (1f - bu));
        float rim = Clamp01(1f - MathF.Abs(best + 0.008f) / 0.010f);
        c = Mix(c, Rgb(0x05010A), rim * 0.85f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Olhos de fogo roxo da gárgula (amêndoas bravas, núcleo claro).</summary>
    static float[] GargoyleEyesTex() => Paint(S, (x, y) =>
    {
        float ax = MathF.Abs(x);
        float ex = ax - 0.17f, ey = y - 0.06f + 0.035f - (ax - 0.17f) * 0.35f;
        float eye = Len(ex / 0.10f, ey / 0.040f) - 1f;
        float glow = Clamp01(1f - Len(ex / 0.28f, ey / 0.16f));
        float core = Cover(eye * 0.05f, S);
        var c = Mix(Rgb(0xB050FF), Rgb(0xFFF0FF), Clamp01(core * (1f - Len(ex / 0.06f, ey / 0.025f) * 0.5f)));
        return (c.r, c.g, c.b, Math.Max(core, glow * glow));
    });

    /// <summary>Interior da boca: escuro com brasa roxa no fundo.</summary>
    static float[] MouthInTex() => Paint(64, (x, y) =>
    {
        float r = Len(x, y * 1.2f);
        float a = Cover((r - 0.92f) * 0.5f, 64);
        if (a <= 0f) return default;
        var c = Mix(Rgb(0xC060FF), Rgb(0x0A0210), Clamp01(r));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Bola de fogo roxa (labaredas saindo em todas as direções).</summary>
    static float[] FireBallTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float n = Fbm(MathF.Cos(ang) * 2.5f + 3f, MathF.Sin(ang) * 2.5f - 2f);
        float edge = 0.55f + 0.35f * n;
        float k = Clamp01((edge - r) / edge);
        if (k <= 0f) return default;
        var c = Mix(Rgb(0x6A1AC0), Rgb(0xFFE0FF), k * k);
        return (c.r, c.g, c.b, Clamp01(k * 2.2f) * (0.7f + 0.3f * n));
    });

    /// <summary>Faíscas violeta estalando na superfície da bola.</summary>
    static float[] ChaosSparksTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.92f) return default;
        float n = Fbm(x * 3.0f + 5f, y * 3.0f - 2f);
        float v = Clamp01(1f - MathF.Abs(n - 0.5f) / 0.018f) * Clamp01((r - 0.30f) / 0.3f);
        float glow = Clamp01(1f - MathF.Abs(n - 0.5f) / 0.07f) * 0.3f * Clamp01((r - 0.30f) / 0.3f);
        float a = Math.Max(v, glow);
        if (a <= 0f) return default;
        var c = Mix(Rgb(0xB050FF), Rgb(0xFFF0FF), v);
        return (c.r, c.g, c.b, a * Clamp01((0.92f - r) / 0.03f));
    });

    /// <summary>Fumaça em bolinha (sai das narinas).</summary>
    static float[] PuffTex() => Paint(64, (x, y) =>
    {
        float r = Len(x, y);
        float n = Fbm(x * 3f + 1f, y * 3f);
        float a = Clamp01(1f - r) * (0.5f + 0.5f * n);
        return (0.55f, 0.45f, 0.65f, a * a * 0.9f);
    });

    /// <summary>Chama cristalizada: base arredondada, língua alta no meio e duas laterais.</summary>
    static float FlameCrystal(float x, float y)
    {
        float body = Len(x / 0.95f, y + 0.26f) - 0.56f;
        float mid = Poly(x, y, -0.32f, 0.0f, 0.32f, 0.0f, 0.04f, 0.94f);
        float l = Poly(x, y, -0.58f, -0.15f, -0.08f, 0.10f, -0.48f, 0.66f);
        float r = Poly(x, y, 0.58f, -0.15f, 0.08f, 0.10f, 0.50f, 0.60f);
        return SMin(SMin(body, mid, 0.14f), MathF.Min(l, r), 0.10f);
    }

    /// <summary>Vulto da cabeça de demônio: chifres longos curvados, juba desgrenhada, focinho.</summary>
    static float DemonHead(float x0, float y0)
    {
        const float k = 1.15f;
        float x = x0 / k, y = y0 / k;
        float ax = MathF.Abs(x);
        float head = Len(x / 0.17f, (y + 0.06f) / 0.20f) - 1f;
        float snout = Box(x, y + 0.24f, 0.10f, 0.07f) - 0.04f;
        float ang = MathF.Atan2(y + 0.10f, x), rr = Len(x, y + 0.10f);
        float mane = rr - (0.30f + 0.06f * MathF.Abs(MathF.Sin(ang * 9f)) + 0.03f * MathF.Sin(ang * 23f));
        mane = MathF.Max(mane, y - 0.08f);                                   // juba só dos lados e embaixo
        // Chifres: saem da cabeça para fora, descem um pouco e sobem curvando.
        float horn = 9f;
        for (int i = 0; i < 12; i++)
        {
            float u0 = i / 12f, u1 = (i + 1) / 12f;
            float hx0 = 0.12f + 0.22f * MathF.Sin(u0 * 2.2f), hy0 = 0.06f - 0.04f * MathF.Sin(u0 * 3f) + 0.40f * u0 * u0;
            float hx1 = 0.12f + 0.22f * MathF.Sin(u1 * 2.2f), hy1 = 0.06f - 0.04f * MathF.Sin(u1 * 3f) + 0.40f * u1 * u1;
            horn = MathF.Min(horn, Seg(ax, y, hx0, hy0, hx1, hy1) - (0.05f * (1f - u0) + 0.010f));
        }
        float d = MathF.Min(MathF.Min(head * 0.17f, snout), MathF.Min(mane, horn));
        return d * k;
    }

    /// <summary>Cristal de chama roxo, facetado e translúcido, com o vulto do demônio dentro.</summary>
    static float[] ChaosCrystalTex(bool polished)
    {
        var fx = new[] { -0.22f, 0.20f, 0.02f, -0.40f, 0.40f, -0.12f, 0.18f, 0.00f, -0.30f, 0.32f };
        var fy = new[] { 0.40f, 0.42f, 0.70f, 0.05f, 0.02f, -0.30f, -0.34f, -0.65f, -0.58f, -0.55f };
        var fl = new[] { 0.95f, 0.55f, 0.85f, 0.80f, 0.35f, 0.70f, 0.40f, 0.30f, 0.65f, 0.25f };
        return Paint(S, (x, y) =>
        {
            float d = FlameCrystal(x, y);
            float a = Cover(d, S);
            if (a <= 0f) return default;
            int best = 0; float b1 = 9f, b2 = 9f;
            for (int k = 0; k < fx.Length; k++)
            {
                float q = Len(x - fx[k], y - fy[k]);
                if (q < b1) { b2 = b1; b1 = q; best = k; } else if (q < b2) b2 = q;
            }
            float lit = fl[best];
            var c = Mix(Rgb(0x3A0E6A), Rgb(polished ? 0xC890F8 : 0xA868E8), lit);
            float alpha = 0.50f + 0.25f * lit;
            // Vulto do demônio, borrado pelo cristal.
            float vs = DemonHead(x, y + 0.02f);
            float sh = Clamp01(0.5f - vs / 0.02f);
            c = Mix(c, Rgb(0x080110), sh * 0.92f);
            alpha = Math.Max(alpha, sh * 0.95f);
            float edge = Clamp01(1f - (b2 - b1) / 0.018f);
            c = Mix(c, Rgb(0xF2DCFF), edge * 0.5f);
            alpha = Math.Max(alpha, edge * 0.85f);
            float rim = Clamp01(1f - MathF.Abs(d + 0.010f) / 0.014f);
            c = Mix(c, Rgb(0xF0D0FF), rim * 0.85f);
            alpha = Math.Max(alpha, rim);
            float sx = x + 0.20f, sy = y - 0.40f;
            float star = Clamp01(1f - Len(sx, sy) / 0.035f) + Clamp01(1f - MathF.Abs(sx) / 0.009f) * Clamp01(1f - MathF.Abs(sy) / 0.10f)
                       + Clamp01(1f - MathF.Abs(sy) / 0.009f) * Clamp01(1f - MathF.Abs(sx) / 0.10f);
            c = Mix(c, (1f, 1f, 1f), Clamp01(star) * (polished ? 0.9f : 0.6f));
            return (c.r, c.g, c.b, a * Clamp01(alpha));
        });
    }

    /// <summary>Fogo roxo dentro do cristal (labaredas subindo, mais forte embaixo e atrás do vulto).</summary>
    static float[] FlameInside(float seed) => Paint(S, (x, y) =>
    {
        float d = FlameCrystal(x, y) + 0.04f;
        if (d > 0f) return default;
        float n = Fbm(x * 3.2f + seed, y * 2.2f - seed * 0.5f);
        float tongues = Clamp01((n - 0.35f) * 2.2f) * Clamp01(0.9f - (y + 0.6f) * 0.6f);
        float a = Clamp01(tongues * 0.9f) * Clamp01(-d / 0.06f) * Clamp01(DemonHead(x, y + 0.02f) / 0.03f + 0.3f);
        var c = Mix(Rgb(0x8A2AE0), Rgb(0xFFD0FF), Clamp01(tongues * 1.2f - 0.2f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Olhos acesos do vulto (fenda brilhante com brilho em volta).</summary>
    static float[] DemonEyesTex() => Paint(S, (x, y) =>
    {
        float ax = MathF.Abs(x), yy = y + 0.02f;
        float ex = ax - 0.065f, ey = yy + 0.02f - ex * 0.45f;
        float eye = Len(ex / 0.045f, ey / 0.016f) - 1f;
        float glow = Clamp01(1f - Len(ex / 0.14f, ey / 0.07f));
        float core = Cover(eye * 0.04f, S);
        var c = Mix(Rgb(0xC050FF), Rgb(0xFFF0FF), core);
        return (c.r, c.g, c.b, Math.Max(core, glow * glow * 0.85f));
    });

    /// <summary>Asa de penas em silhueta (preta sólida, pontas das penas recortadas, contorno roxo).</summary>
    static float[] ShadowWingTex(float side)
    {
        // Contorno: borda de ataque (ombro, cotovelo, pulso, ponta) e borda de trás com as pontas das penas.
        var v = new List<float> { -0.92f, -0.12f, -0.55f, 0.45f, -0.05f, 0.84f, 0.92f, 0.95f };
        const int n = 11;
        for (int i = 0; i <= n; i++)
        {
            float u = i / (float)n;
            // Curva de trás: da ponta até perto do corpo, caindo em arco.
            float bx = 0.92f + (-0.80f - 0.92f) * u, by = 0.95f + (-0.62f - 0.95f) * u - 0.28f * MathF.Sin(u * MathF.PI);
            if (i > 0) { v.Add(bx); v.Add(by); }                            // ponta da pena (a primeira é a própria ponta da asa)
            if (i < n)
            {
                float u2 = (i + 0.5f) / n;
                float nx = 0.92f + (-0.80f - 0.92f) * u2, ny = 0.95f + (-0.62f - 0.95f) * u2 - 0.28f * MathF.Sin(u2 * MathF.PI);
                float px = nx + (-0.05f - nx) * 0.24f, py = ny + (0.80f - ny) * 0.24f;   // reentrância entre penas (puxa para o pulso)
                v.Add(px); v.Add(py);
            }
        }
        var pv = v.ToArray();
        return Paint(256, (x0, y) =>
        {
            float x = x0 * side;
            float body = PolyN(x, y, pv);
            float a = Cover(body * 0.6f, 256);
            if (a <= 0f) return default;
            var c = Mix(Rgb(0x040208), Rgb(0x140C20), Clamp01(0.5f + y * 0.6f));
            float rim = Clamp01(1f - MathF.Abs(body + 0.012f) / 0.018f);
            c = Mix(c, Rgb(0xA060F0), rim * 0.9f);
            // Linhas finas das penas, irradiando do pulso.
            float ang = MathF.Atan2(y - 0.80f, x + 0.05f);
            float kk = ang / MathF.PI * 22f;
            float line = Clamp01(1f - MathF.Abs(kk - MathF.Round(kk)) / 0.08f) * Clamp01((Len(x + 0.05f, y - 0.80f) - 0.35f) * 3f) * (1f - rim);
            c = Mix(c, Rgb(0x2E1E44), line * 0.7f);
            return (c.r, c.g, c.b, a);
        });
    }

    /// <summary>Caveira de demônio (crânio + maxilar com presas; a mandíbula de baixo é separada).</summary>
    static float DemonSkullShape(float x, float y)
    {
        float jaw = Box(x, y + 0.50f, 0.24f, 0.07f) - 0.05f;           // mandíbula fechada (para buraco e sombra)
        return SMin(DemonCranium(x, y), jaw, 0.08f);
    }

    static float DemonCranium(float x, float y)
    {
        float cran = Len(x / 0.98f, (y - 0.16f) / 0.92f) - 0.50f;
        float cheek = Box(x, y + 0.14f, 0.36f, 0.10f) - 0.06f;
        float maxilla = Box(x, y + 0.30f, 0.25f, 0.08f) - 0.05f;
        return SMin(SMin(cran, cheek, 0.10f), maxilla, 0.08f);
    }

    /// <summary>Obsidiana roxa polida: sombreado de volume, brilho especular e trincas com fogo roxo.</summary>
    static (float r, float g, float b) Obsidian(float x, float y, float d, bool polished)
    {
        float k = Clamp01(-d / 0.22f);
        float nz = MathF.Sqrt(Math.Max(0f, 1f - Sq(1f - k)));
        float lit = Clamp01(0.45f + 0.55f * (-x * 0.5f + y * 0.6f) * (1f - nz) + 0.35f * nz);
        var c = lit < 0.5f ? Mix(Rgb(0x06020A), Rgb(0x1E0C30), lit * 2f) : Mix(Rgb(0x1E0C30), Rgb(polished ? 0x5E3A8A : 0x4A2A70), (lit - 0.5f) * 2f);
        float spec = MathF.Pow(Clamp01(1f - Len(x + 0.22f, y - 0.42f) / 0.20f), 2f);
        c = Mix(c, Rgb(0xE8D8FF), spec * (polished ? 0.6f : 0.4f));
        float crack = Clamp01(1f - MathF.Abs(Fbm(x * 4.5f + 6f, y * 4.5f - 1f) - 0.5f) / 0.018f) * Clamp01(-d / 0.05f);
        c = Mix(c, Rgb(0xD070FF), crack * 0.8f);
        return c;
    }

    static float[] DemonSkullTex(bool polished) => Paint(S, (x, y) =>
    {
        float d = DemonCranium(x, y);
        float a = Cover(d, S);
        // Dentes de cima e presas (creme), na base do maxilar.
        float teeth = 9f;
        for (int k = -2; k <= 2; k++)
        {
            float tx = k * 0.075f;
            bool fang = k == -2 || k == 2;
            float len = fang ? 0.20f : 0.06f, hw = fang ? 0.034f : 0.024f;
            teeth = MathF.Min(teeth, Poly(x, y, tx - hw, -0.32f, tx + hw, -0.32f, tx * (fang ? 0.9f : 1f), -0.32f - len));
        }
        float aT = Clamp01(0.5f - teeth / 0.008f);
        if (a <= 0f && aT <= 0f) return default;
        var c = Obsidian(x, y, d, polished);
        float ax = MathF.Abs(x);
        // Órbitas angulosas (bravas), fundas, com brasa no fundo.
        float eye = Poly(ax, y, 0.04f, 0.04f, 0.27f, 0.20f, 0.20f, -0.05f);
        eye = SMin(eye, Len(ax - 0.17f, y - 0.06f) - 0.09f, 0.04f);
        if (eye < 0.012f)
        {
            float depth = Clamp01(-eye / 0.06f);
            var hole = Mix(Rgb(0x1A0624), Rgb(0x020004), depth);
            hole = Mix(hole, Rgb(0x9A30E0), Clamp01(1f - Len(ax - 0.15f, y - 0.01f) / 0.07f) * 0.6f);
            c = Mix(c, hole, Clamp01(0.5f - eye / 0.012f));
        }
        float nose = Poly(ax, y, 0f, -0.06f, 0.06f, -0.17f, 0f, -0.20f);
        if (nose < 0.01f) c = Mix(c, Rgb(0x050008), Clamp01(0.5f - nose / 0.01f));
        float brow = Clamp01(1f - MathF.Abs(y - 0.23f + (ax - 0.15f) * 0.55f) / 0.025f) * Clamp01(1f - MathF.Abs(ax - 0.17f) / 0.14f);
        c = Mix(c, Rgb(0xB890E8), brow * 0.35f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.008f) / 0.010f);
        c = Mix(c, Rgb(0x06000C), rim * 0.85f);
        var tc = Mix(Rgb(0xF4ECD8), Rgb(0xA89878), Clamp01((-0.33f - y) / 0.16f));
        if (aT > 0f) c = a > 0f ? Mix(c, tc, aT) : tc;
        return (c.r, c.g, c.b, Math.Max(a, aT));
    });

    static float[] DemonJawTex() => Paint(S, (x, y) =>
    {
        float d = Poly(x, y, -0.27f, -0.42f, 0.27f, -0.42f, 0f, -0.66f) - 0.04f;
        d = SMin(d, Box(x, y + 0.47f, 0.22f, 0.05f) - 0.03f, 0.05f);
        float a = Cover(d, S);
        float teeth = 9f;
        for (int k = -2; k <= 2; k++)
        {
            float tx = k * 0.07f;
            bool fang = k == -2 || k == 2;
            float len = fang ? 0.15f : 0.05f, hw = fang ? 0.030f : 0.022f;
            teeth = MathF.Min(teeth, Poly(x, y, tx - hw, -0.42f, tx + hw, -0.42f, tx, -0.42f + len));
        }
        float aT = Clamp01(0.5f - teeth / 0.008f);
        if (a <= 0f && aT <= 0f) return default;
        var c = Obsidian(x, y + 0.3f, d, false);
        float rim = Clamp01(1f - MathF.Abs(d + 0.008f) / 0.010f);
        c = Mix(c, Rgb(0x06000C), rim * 0.85f);
        var tc = Mix(Rgb(0xF4ECD8), Rgb(0xA89878), Clamp01((y + 0.42f) / 0.13f));
        if (aT > 0f) c = a > 0f ? Mix(c, tc, aT) : tc;
        return (c.r, c.g, c.b, Math.Max(a, aT));
    });

    /// <summary>Chifres creme, longos e curvados (saem das têmporas, abrem para fora e sobem).</summary>
    static float[] DemonHornsTex()
    {
        var pts = new List<(float x, float y)>();
        for (int i = 0; i <= 24; i++)
        {
            float u = i / 24f;
            // Sai da têmpora, vai para fora e para baixo, curva e sobe com a ponta para dentro.
            float x = 0.34f + 0.42f * MathF.Sin(u * 2.2f) - 0.10f * u * u;
            float y = 0.30f - 0.10f * MathF.Sin(u * 3.0f) + 0.62f * u * u;
            pts.Add((x, y));
        }
        return Paint(S, (x0, y) =>
        {
            float x = MathF.Abs(x0);
            float best = 9f, bu = 0f;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float u = i / (float)(pts.Count - 1);
                float w = 0.085f * (1f - u) + 0.012f;
                float dd = Seg(x, y, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y) - w;
                if (dd < best) { best = dd; bu = u; }
            }
            float a = Cover(best * 0.6f, S);
            if (a <= 0f) return default;
            var c = Mix(Rgb(0x6A5A44), Rgb(0xF2E8D2), Clamp01(0.3f + bu * 0.9f));
            float ring = 0.5f + 0.5f * MathF.Sin(bu * 70f);
            c = Mix(c, Rgb(0x8A7A60), ring * 0.25f * (1f - bu));
            float rim = Clamp01(1f - MathF.Abs(best + 0.008f) / 0.010f);
            c = Mix(c, Rgb(0x2A2014), rim * 0.8f);
            return (c.r, c.g, c.b, a);
        });
    }

    /// <summary>Labareda roxa saindo da órbita (base embaixo).</summary>
    static float[] EyeFlameTex(float seed) => Paint(64, (x, y) =>
    {
        float u = Clamp01((y + 0.9f) / 1.8f);
        float wob = 0.18f * MathF.Sin(u * 5f + seed) * u;
        float w = 0.55f * MathF.Pow(1f - u, 0.8f) * (0.8f + 0.4f * Fbm(u * 3f + seed, seed));
        float k = MathF.Abs(x - wob) / Math.Max(w, 0.01f);
        if (k > 1f) return default;
        float core = Clamp01(1f - k) * Clamp01(1f - u * 1.4f);
        var c = Mix(Rgb(0x7A1ED0), Rgb(0xE8A0FF), core);
        c = Mix(c, (1f, 0.95f, 1f), Clamp01(core - 0.5f) * 1.2f);
        return (c.r, c.g, c.b, Clamp01((1f - k) * 1.6f) * Clamp01(1f - u * 0.9f));
    });

    /// <summary>Rocha vulcânica: fissuras de lava roxa na superfície da bola.</summary>
    static float[] LavaCracksTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.92f) return default;
        float n = Fbm(x * 2.4f + 3f, y * 2.4f - 5f), n2 = Fbm(x * 5f - 1f, y * 5f + 2f);
        float v = MathF.Max(Clamp01(1f - MathF.Abs(n - 0.5f) / 0.035f), 0.6f * Clamp01(1f - MathF.Abs(n2 - 0.5f) / 0.022f));
        float glow = MathF.Max(v, Clamp01(1f - MathF.Abs(n - 0.5f) / 0.12f) * 0.35f);
        if (glow <= 0f) return default;
        var c = Mix(Rgb(0x8A20D0), Rgb(0xFFC8FF), v * v);
        return (c.r, c.g, c.b, glow * Clamp01((0.92f - r) / 0.03f));
    });

    /// <summary>Onda de choque roxa (anel grosso que se desfaz).</summary>
    static float[] ShockRingTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float a = Clamp01(1f - MathF.Abs(r - 0.86f) / 0.10f);
        a *= 0.6f + 0.4f * Fbm(x * 6f, y * 6f);
        var c = Mix(Rgb(0x8A30E0), Rgb(0xF0C8FF), Clamp01(1f - MathF.Abs(r - 0.88f) / 0.03f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Asa de penas pretas (side = -1 esquerda, 1 direita), com brilho roxo nas bordas das penas.</summary>
    static float[] FeatherWingTex(float side) => Paint(256, (x0, y) =>
    {
        float x = x0 * side;
        float shx = -0.88f, shy = -0.20f, wrx = -0.10f, wry = 0.25f;
        float best = 9f, along = 0f;
        for (int k = 0; k < 12; k++)             // primárias, sobrepostas
        {
            float ang = (62f - k * 7.5f) * MathF.PI / 180f;
            float len = 0.70f + 0.28f * MathF.Sin((k + 2) / 14f * MathF.PI);
            float ex = wrx + MathF.Cos(ang) * len, ey = wry + MathF.Sin(ang) * len;
            float sg = Seg(x, y, wrx, wry, ex, ey);
            float u = Clamp01(((x - wrx) * (ex - wrx) + (y - wry) * (ey - wry)) / (len * len));
            float w = 0.072f * MathF.Sin(MathF.PI * MathF.Min(1f, u * 1.05f)) * (1f - 0.3f * u) + 0.006f;
            float dd = sg - w;
            if (dd < best) { best = dd; along = u; }
        }
        for (int k = 0; k < 7; k++)              // secundárias, ao longo do braço
        {
            float f = k / 6f;
            float bx = shx + (wrx - shx) * f, by = shy + (wry - shy) * f;
            float ang = (-62f - 10f * f) * MathF.PI / 180f;
            float len = 0.42f + 0.10f * f;
            float ex = bx + MathF.Cos(ang) * len, ey = by + MathF.Sin(ang) * len;
            float sg = Seg(x, y, bx, by, ex, ey);
            float u = Clamp01(((x - bx) * (ex - bx) + (y - by) * (ey - by)) / (len * len));
            float dd = sg - (0.07f * MathF.Sin(MathF.PI * MathF.Min(1f, u * 1.1f)) + 0.006f);
            if (dd < best) { best = dd; along = u; }
        }
        float arm = MathF.Min(Seg(x, y, shx, shy, wrx, wry) - 0.14f, Len(x - wrx + 0.05f, y - wry + 0.03f) - 0.17f);
        float d = MathF.Min(best, arm);
        float a = Cover(d * 0.5f, 256);
        if (a <= 0f) return default;
        var c = Mix(Rgb(0x0E0A14), Rgb(0x2E2440), Clamp01(along));
        float edge = Clamp01(1f - MathF.Abs(best + 0.012f) / 0.02f);
        c = Mix(c, Rgb(0x9A6AE0), edge * 0.85f);
        float shaft = Clamp01(1f - MathF.Abs(best + 0.05f) / 0.006f) * (1f - along);
        c = Mix(c, Rgb(0x3A2A4A), shaft * 0.5f);
        if (arm < 0f && arm < best) c = Mix(Rgb(0x0A0610), Rgb(0x241A30), Clamp01(Fbm(x * 12f, y * 12f)));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Juba desgrenhada em volta da bola (tufos bordô e roxos com pontas em brasa).</summary>
    static float[] ManeTex(float seed) => Paint(256, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float tuft = Fbm(MathF.Cos(ang) * 3f + seed, MathF.Sin(ang) * 3f - seed);
        float spikes = MathF.Pow(MathF.Abs(MathF.Sin(ang * 17f + tuft * 6f)), 0.6f);
        float outer = 0.62f + 0.30f * spikes * (0.6f + 0.6f * tuft);
        if (r > outer || r < 0.40f) return default;
        float k = Clamp01((r - 0.45f) / Math.Max(outer - 0.45f, 0.01f));
        var c = Mix(Rgb(0x4A1430), Rgb(0xA83A66), k);
        c = Mix(c, Rgb(0xD070FF), Clamp01((k - 0.7f) * 3.5f) * 0.9f);
        float strand = 0.5f + 0.5f * MathF.Sin(ang * 90f + r * 20f);
        c = Mix(c, Rgb(0x14040E), strand * 0.3f);
        return (c.r, c.g, c.b, Clamp01((outer - r) / 0.03f) * 0.95f);
    });

    /// <summary>Pena preta solta queimando na ponta (fogo roxo).</summary>
    static float[] FeatherTex() => Paint(64, (x, y) =>
    {
        float u = Clamp01((y + 0.9f) / 1.8f);
        float w = 0.40f * MathF.Sin(MathF.PI * MathF.Min(1f, u * 1.1f)) + 0.02f;
        float d = MathF.Abs(x) - w;
        float a = Cover(d * 0.5f, 64);
        if (a <= 0f) return default;
        var c = Mix(Rgb(0x050308), Rgb(0x2A1E36), Clamp01(1f - MathF.Abs(x) / 0.4f));
        c = Mix(c, Rgb(0xD070FF), Clamp01((u - 0.72f) * 4f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Presa de gelo (base em cima, ponta curva para baixo).</summary>
    static float[] IceFangTex() => Paint(64, (x, y) =>
    {
        float u = Clamp01((0.95f - y) / 1.9f);                   // 0 base .. 1 ponta
        float cx = 0.18f * u * u;
        float w = 0.55f * (1f - u) + 0.02f;
        float d = (MathF.Abs(x - cx) - w) * 0.5f;
        if (y > 0.95f || y < -0.95f) d = Math.Max(d, 0.02f);
        float a = Cover(d, 64);
        if (a <= 0f) return default;
        float k = (x - cx) / Math.Max(w, 0.01f);
        var c = Mix(Rgb(0xE8F6FF), Rgb(0x6AA6E0), Clamp01(0.5f + k * 0.6f));
        c = Mix(c, Rgb(0xFFFFFF), Clamp01(1f - MathF.Abs(k + 0.4f) / 0.15f) * 0.7f);
        return (c.r, c.g, c.b, a * 0.95f);
    });

    /// <summary>Bloco de gelo cristalino irregular (em pé).</summary>
    static float IceBlockShape(float x, float y) => PolyN(x, y, new[] { -0.08f, 0.90f, 0.22f, 0.86f, 0.46f, 0.62f, 0.58f, 0.22f, 0.60f, -0.30f, 0.50f, -0.80f, -0.48f, -0.82f, -0.60f, -0.28f, -0.56f, 0.30f, -0.36f, 0.72f });

    /// <summary>Vampiro (busto) de olhos fechados e braços cruzados no peito. Retorna cor e cobertura.</summary>
    static ((float r, float g, float b) c, float a) Vampire(float x, float y)
    {
        float ax = MathF.Abs(x);
        var skin = Mix(Rgb(0xA8BED2), Rgb(0xEAF2F8), Clamp01(0.55f - x * 1.6f + (y - 0.28f) * 0.8f));
        // Gola alta da capa atrás da cabeça (pontas para cima e para fora), forro vermelho.
        float collarO = PolyN(ax, y, new[] { 0.05f, 0.02f, 0.44f, 0.00f, 0.46f, 0.62f, 0.30f, 0.40f, 0.20f, 0.20f });
        float collarI = PolyN(ax, y, new[] { 0.10f, 0.05f, 0.38f, 0.04f, 0.40f, 0.50f, 0.28f, 0.33f, 0.20f, 0.16f });
        float head = Len(x / 0.195f, (y - 0.27f) / 0.235f) - 1f;
        float jaw = Poly(x, y, -0.18f, 0.22f, 0.18f, 0.22f, 0f, 0.01f);
        head = MathF.Min(head, jaw * 6f);
        float hairTop = Len(x / 0.215f, (y - 0.34f) / 0.22f) - 1f;
        float peak = (y - 0.37f) - ax * 0.9f + 0.02f;            // bico na testa (V)
        float hair = MathF.Max(hairTop, -peak * 8f);
        hair = MathF.Max(hair, -(y - 0.25f) * 8f);
        float cape = PolyN(x, y, new[] { -0.44f, 0.02f, 0.44f, 0.02f, 0.46f, -0.80f, -0.46f, -0.80f });
        float shirt = Poly(x, y, -0.11f, 0.05f, 0.11f, 0.05f, 0f, -0.30f);
        float neck = Box(x, y - 0.07f, 0.06f, 0.05f);
        float armA = Seg(x, y, -0.40f, -0.12f, 0.20f, -0.42f) - 0.085f;
        float armB = Seg(x, y, 0.40f, -0.12f, -0.20f, -0.42f) - 0.085f;
        float handA = Len((x - 0.24f) / 0.07f, (y + 0.40f) / 0.055f) - 1f, handB = Len((x + 0.24f) / 0.07f, (y + 0.40f) / 0.055f) - 1f;
        float gem = Len(x, y - 0.02f) - 0.035f;

        (float r, float g, float b) c = default; float a = 0f;
        void Lay(float d, (float r, float g, float b) col) { float k = Clamp01(0.5f - d * 120f); if (k > 0f) { c = a <= 0f ? col : Mix(c, col, k); a = Math.Max(a, k); } }
        Lay(collarO, Mix(Rgb(0x0A0A16), Rgb(0x34344E), Clamp01(y * 1.5f)));
        Lay(collarI, Mix(Rgb(0x6A0018), Rgb(0xC0142E), Clamp01(y * 2f)));
        Lay(cape, Mix(Rgb(0x08081A), Rgb(0x2C2C48), Clamp01(0.5f - x * 1.0f) * 0.9f));
        Lay(shirt, Mix(Rgb(0xC8D4E0), Rgb(0xF4F8FC), Clamp01(y + 0.3f)));
        Lay(neck, skin);
        // Braços cruzados: manga escura com borda clara para ler bem pequeno.
        float edgeA = Clamp01(1f - MathF.Abs(armA + 0.012f) / 0.014f), edgeB = Clamp01(1f - MathF.Abs(armB + 0.012f) / 0.014f);
        Lay(armB, Mix(Rgb(0x1A1A30), Rgb(0x6A6A90), edgeB));
        Lay(armA, Mix(Rgb(0x22223C), Rgb(0x7A7AA0), edgeA));
        Lay(handA, skin); Lay(handB, skin);
        Lay(gem, Rgb(0xD0142E));
        Lay(head, skin);
        Lay(hair, Mix(Rgb(0x06060C), Rgb(0x30304A), Clamp01((y - 0.36f) * 4f)));
        if (head < 0f && hair > 0f)
        {
            // Olhos fechados (arcos para baixo), sobrancelhas arqueadas, boca e presas.
            float eyes = MathF.Abs(Len(ax - 0.072f, y - 0.300f) - 0.040f) - 0.008f;
            if (eyes < 0f && y < 0.300f) c = Mix(c, Rgb(0x22222E), 0.9f);
            float brow = Seg(ax, y, 0.030f, 0.335f, 0.120f, 0.365f) - 0.010f;
            if (brow < 0f) c = Mix(c, Rgb(0x10101A), 0.95f);
            float nose = Seg(x, y, 0f, 0.27f, 0.012f, 0.215f) - 0.005f;
            if (nose < 0f) c = Mix(c, Rgb(0x8AA0B8), 0.6f);
            float mouth = Seg(x, y, -0.050f, 0.165f, 0.050f, 0.165f) - 0.006f;
            if (mouth < 0f) c = Mix(c, Rgb(0x4A2030), 0.85f);
            float fang = Poly(ax, y, 0.018f, 0.163f, 0.040f, 0.163f, 0.029f, 0.118f);
            if (fang < 0f) c = Rgb(0xFFFFFF);
            float shade = Clamp01(1f - Len(ax - 0.11f, y - 0.24f) / 0.07f);
            c = Mix(c, Rgb(0x7A92AE), shade * 0.35f);
        }
        return (c, a);
    }

    /// <summary>Vulto do vampiro em pé na pose clássica (braços cruzados no peito, gola alta). Distância com sinal.</summary>
    static float VampSilhouette(float x, float y)
    {
        float ax = MathF.Abs(x);
        float head = Len(x / 0.13f, (y - 0.38f) / 0.155f) - 1f;
        float peak = Poly(ax, y, 0f, 0.50f, 0.075f, 0.53f, 0.0f, 0.585f);                  // bico do cabelo na testa
        float ears = Poly(ax, y, 0.10f, 0.42f, 0.125f, 0.35f, 0.18f, 0.47f);               // orelhas pontudas
        float collar = PolyN(ax, y, new[] { 0.03f, 0.22f, 0.26f, 0.20f, 0.34f, 0.64f, 0.20f, 0.40f, 0.10f, 0.30f });
        float cape = PolyN(x, y, new[] { -0.22f, 0.24f, 0.22f, 0.24f, 0.30f, -0.20f, 0.38f, -0.72f, 0.20f, -0.66f, 0f, -0.74f, -0.20f, -0.66f, -0.38f, -0.72f, -0.30f, -0.20f });
        float d = MathF.Min(MathF.Min(head * 0.12f, peak), MathF.Min(ears, collar));
        return MathF.Min(d, cape);
    }

    /// <summary>Braços cruzados em X dentro do vulto (um tom mais claro).</summary>
    static float VampArms(float x, float y)
        => MathF.Min(Seg(x, y, -0.20f, 0.18f, 0.14f, -0.06f), Seg(x, y, 0.20f, 0.18f, -0.14f, -0.06f)) - 0.035f;

    /// <summary>Bloco de gelo facetado e translúcido (faces planas), com o vulto do vampiro lá dentro.</summary>
    static float[] FrozenVampire(bool polished)
    {
        // Sementes das faces planas do cristal.
        var fx = new[] { -0.30f, 0.25f, 0.05f, -0.45f, 0.48f, -0.15f, 0.30f, -0.40f, 0.42f, 0.00f };
        var fy = new[] { 0.60f, 0.62f, 0.20f, 0.05f, 0.00f, -0.40f, -0.45f, -0.62f, -0.62f, 0.85f };
        var fl = new[] { 0.95f, 0.55f, 0.75f, 0.85f, 0.35f, 0.65f, 0.40f, 0.70f, 0.30f, 0.90f };
        return Paint(S, (x, y) =>
        {
            float d = IceBlockShape(x, y);
            float a = Cover(d, S);
            if (a <= 0f) return default;
            int best = 0, second = 0; float b1 = 9f, b2 = 9f;
            for (int k = 0; k < fx.Length; k++)
            {
                float q = Len(x - fx[k], y - fy[k]);
                if (q < b1) { b2 = b1; second = best; b1 = q; best = k; } else if (q < b2) { b2 = q; second = k; }
            }
            float lit = fl[best];
            var c = Mix(Rgb(0x5C8ED8), Rgb(polished ? 0xD8E8FF : 0xBCD6FA), lit);
            c = Mix(c, Rgb(0xB0B4F0), 0.15f);                                   // leve toque lilás do gelo mágico
            float alpha = 0.38f + 0.30f * lit;
            // Vulto do vampiro, borrado pelo gelo, mais nítido no meio.
            float vs = VampSilhouette(x, y + 0.05f);
            float shadow = Clamp01(0.5f - vs / 0.03f);
            c = Mix(c, Rgb(0x0E1830), shadow * 0.85f);
            float arm = Clamp01(0.5f - VampArms(x, y + 0.05f) / 0.02f) * shadow;
            c = Mix(c, Rgb(0x2A3C60), arm * 0.8f);
            alpha = Math.Max(alpha, shadow * 0.92f);
            // Arestas entre as faces e contorno.
            float edge = Clamp01(1f - (b2 - b1) / 0.018f);
            c = Mix(c, (1f, 1f, 1f), edge * 0.55f);
            alpha = Math.Max(alpha, edge * 0.85f);
            float rim = Clamp01(1f - MathF.Abs(d + 0.010f) / 0.014f);
            c = Mix(c, Rgb(0xF4FBFF), rim * 0.8f);
            alpha = Math.Max(alpha, rim);
            float frost = Clamp01((-y - 0.45f) / 0.35f) * (0.4f + 0.6f * Noise(x * 22f, y * 22f));
            c = Mix(c, Rgb(0xF0FAFF), frost * 0.5f);
            float sx = x + 0.28f, sy = y - 0.62f;
            float star = Clamp01(1f - Len(sx, sy) / 0.035f) + Clamp01(1f - MathF.Abs(sx) / 0.009f) * Clamp01(1f - MathF.Abs(sy) / 0.10f)
                       + Clamp01(1f - MathF.Abs(sy) / 0.009f) * Clamp01(1f - MathF.Abs(sx) / 0.10f);
            c = Mix(c, (1f, 1f, 1f), Clamp01(star) * (polished ? 0.9f : 0.6f));
            return (c.r, c.g, c.b, a * Clamp01(alpha));
        });
    }

    /// <summary>Olhos do vampiro se abrindo: dois riscos acesos em azul-gelo.</summary>
    static float[] VampEyesTex() => Paint(S, (x, y) =>
    {
        float ax = MathF.Abs(x), yy = y;
        float eye = Len((ax - 0.050f) / 0.030f, (yy - 0.335f) / 0.011f) - 1f;
        float glow = Clamp01(1f - Len((ax - 0.050f) / 0.10f, (yy - 0.335f) / 0.05f));
        float core = Cover(eye * 0.03f, S);
        var c = Mix(Rgb(0x40D8FF), Rgb(0xF0FFFF), core);
        return (c.r, c.g, c.b, Math.Max(core, glow * glow * 0.8f));
    });

    /// <summary>Trincas no bloco do vampiro (saem da borda).</summary>
    static float[] IceBlockCracks(int stage)
    {
        var rng = new Random(53);
        var lines = new List<(float x, float y)[]>();
        for (int i = 0; i < 7; i++)
        {
            float a0 = i * MathF.Tau / 7f + (float)rng.NextDouble() * 0.5f;
            float r0 = 0.02f; float dx = MathF.Cos(a0), dy = MathF.Sin(a0);
            while (r0 < 1.2f && IceBlockShape(dx * r0, dy * r0) < -0.03f) r0 += 0.01f;
            float cx = dx * r0, cy = dy * r0, a = a0 + MathF.PI;
            var pts = new List<(float, float)> { (cx, cy) };
            for (int k = 0; k < 2 + 2 * stage; k++)
            {
                a += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                cx += MathF.Cos(a) * 0.07f; cy += MathF.Sin(a) * 0.07f;
                pts.Add((cx, cy));
            }
            lines.Add(pts.ToArray());
        }
        return CrackLines(lines, 0.006f, 1.4f, 0xD8F4FF);
    }

    /// <summary>Caixão de vampiro (estreito em cima e embaixo, ombros largos).</summary>
    static float Coffin(float x, float y) => PolyN(x, y, new[] { -0.24f, 0.86f, 0.24f, 0.86f, 0.46f, 0.44f, 0.28f, -0.86f, -0.28f, -0.86f, -0.46f, 0.44f });

    static List<(float, float, float, float)> Arc(List<(float, float, float, float)> l, float cx, float cy, float r, float a0, float a1, int n = 10)
    {
        for (int i = 0; i < n; i++)
        {
            float u0 = (a0 + (a1 - a0) * i / n) * MathF.PI / 180f, u1 = (a0 + (a1 - a0) * (i + 1) / n) * MathF.PI / 180f;
            l.Add((cx + MathF.Cos(u0) * r, cy + MathF.Sin(u0) * r, cx + MathF.Cos(u1) * r, cy + MathF.Sin(u1) * r));
        }
        return l;
    }

    /// <summary>Runa vampírica principal (original): lua crescente, asas de morcego, gota e presas.</summary>
    static List<(float, float, float, float)> MainRune()
    {
        var l = new List<(float, float, float, float)>
        {
            (0f, -0.46f, 0f, 0.40f),                                   // haste
            (0f, 0.14f, -0.20f, 0.26f), (-0.20f, 0.26f, -0.32f, 0.20f), (-0.32f, 0.20f, -0.26f, 0.08f), (-0.26f, 0.08f, -0.17f, 0.10f), (-0.17f, 0.10f, -0.10f, 0.03f),
            (0f, 0.14f, 0.20f, 0.26f), (0.20f, 0.26f, 0.32f, 0.20f), (0.32f, 0.20f, 0.26f, 0.08f), (0.26f, 0.08f, 0.17f, 0.10f), (0.17f, 0.10f, 0.10f, 0.03f),
            (-0.16f, -0.38f, 0.16f, -0.38f),                           // barra das presas
            (-0.09f, -0.38f, -0.05f, -0.58f), (0.09f, -0.38f, 0.05f, -0.58f),   // presas
            (0f, -0.05f, -0.06f, -0.15f), (-0.06f, -0.15f, 0f, -0.24f), (0f, -0.24f, 0.06f, -0.15f), (0.06f, -0.15f, 0f, -0.05f),   // gota/losango
        };
        Arc(l, 0f, 0.58f, 0.18f, 200f, 340f, 12);                      // lua com as pontas para cima
        return l;
    }

    /// <summary>Runas menores do círculo: combinações de traços do mesmo alfabeto vampírico.</summary>
    static List<(float, float, float, float)> SmallRune(int i)
    {
        var l = new List<(float, float, float, float)> { (0f, -0.6f, 0f, 0.6f) };
        switch (i % 3)   // topo
        {
            case 0: Arc(l, 0f, 0.62f, 0.26f, 200f, 340f, 8); break;
            case 1: l.Add((-0.28f, 0.70f, 0f, 0.42f)); l.Add((0f, 0.42f, 0.28f, 0.70f)); break;
            case 2: l.Add((0f, 0.78f, -0.14f, 0.62f)); l.Add((-0.14f, 0.62f, 0f, 0.46f)); l.Add((0f, 0.46f, 0.14f, 0.62f)); l.Add((0.14f, 0.62f, 0f, 0.78f)); break;
        }
        switch ((i / 3) % 2)   // meio
        {
            case 0: l.Add((0f, 0.05f, -0.40f, 0.25f)); l.Add((-0.40f, 0.25f, -0.36f, -0.05f)); l.Add((0f, 0.05f, 0.40f, 0.25f)); l.Add((0.40f, 0.25f, 0.36f, -0.05f)); break;
            case 1: l.Add((-0.32f, 0.08f, 0.32f, 0.08f)); break;
        }
        switch ((i / 6) % 2 + (i % 2) * 2)   // base
        {
            case 0: l.Add((-0.22f, -0.40f, -0.14f, -0.72f)); l.Add((0.22f, -0.40f, 0.14f, -0.72f)); l.Add((-0.26f, -0.40f, 0.26f, -0.40f)); break;
            case 1: l.Add((0f, -0.30f, -0.28f, -0.62f)); l.Add((0f, -0.30f, 0.28f, -0.62f)); break;
            case 2: Arc(l, 0f, -0.42f, 0.22f, 20f, 160f, 8); break;
            default: l.Add((-0.24f, -0.25f, 0.24f, -0.25f)); l.Add((-0.16f, -0.50f, 0.16f, -0.50f)); break;
        }
        return l;
    }

    static float SegDist(List<(float ax, float ay, float bx, float by)> segs, float x, float y)
    {
        float d = 9f;
        foreach (var q in segs) d = MathF.Min(d, Seg(x, y, q.ax, q.ay, q.bx, q.by));
        return d;
    }

    /// <summary>Traço da runa aceso: núcleo branco-ciano com brilho em volta.</summary>
    static float[] GlyphTex(List<(float, float, float, float)> segs, int size, float width, bool glow, float haloK = 0.75f, int col0 = 0x22C8FF, int col1 = 0xE8FCFF) => Paint(size, (x, y) =>
    {
        float d = SegDist(segs, x, y) - width;
        float core = Cover(d, size);
        float halo = MathF.Exp(-Math.Max(d, 0f) / (width * 3.5f)) * haloK;
        var c = Mix(Rgb(col0), Rgb(col1), core * 0.7f);
        return (c.r, c.g, c.b, Clamp01(Math.Max(core, glow ? halo : 0f)));
    });

    /// <summary>Placa de gelo claro (chanfro facetado, geada no centro) com a runa entalhada.</summary>
    static float[] RuneTablet(bool polished)
    {
        var rune = MainRune();
        return Paint(S, (x, y) =>
        {
            float d = Coffin(x, y);
            float a = Cover(d, S);
            if (a <= 0f) return default;
            // Chanfro: faixa perto da borda, iluminada conforme o lado (luz de cima à esquerda).
            const float h = 0.01f;
            float nx = Coffin(x + h, y) - Coffin(x - h, y), ny = Coffin(x, y + h) - Coffin(x, y - h);
            float nl = MathF.Sqrt(nx * nx + ny * ny) + 1e-5f; nx /= nl; ny /= nl;
            float bevel = Clamp01(1f + d / 0.10f);
            float lit = Clamp01(0.5f + 0.5f * (-nx * 0.6f + ny * 0.8f));
            float n = Fbm(x * 6f + 2f, y * 6f - 3f);
            var face = Mix(Rgb(0x4E96D2), Rgb(0xB8E0F8), Clamp01(0.35f + 0.5f * n + 0.2f * y));
            var bev = Mix(Rgb(0x3E86C8), Rgb(polished ? 0xFFFFFF : 0xEAF8FF), lit);
            var c = bevel > 0.01f ? Mix(face, bev, MathF.Min(1f, bevel * 1.6f)) : face;
            float inner = Clamp01(1f - MathF.Abs(d + 0.10f) / 0.008f);
            c = Mix(c, Rgb(0xFFFFFF), inner * 0.5f);
            // Entalhe da runa: sulco azul profundo com borda clara.
            float rd = SegDist(rune, x, y);
            float groove = Clamp01(1f - (rd - 0.040f) / 0.008f);
            float lip = Clamp01(1f - MathF.Abs(rd - 0.052f) / 0.012f);
            float shadowSide = Clamp01(1f - MathF.Abs(SegDist(rune, x + 0.012f, y - 0.012f) - 0.040f) / 0.010f);
            c = Mix(c, Mix(Rgb(0x061A40), Rgb(0x0E3A78), Clamp01(rd / 0.04f)), groove);   // sulco azul-marinho
            c = Mix(c, Rgb(0x2A5C96), shadowSide * 0.5f * (1f - groove));                  // sombra do entalhe
            c = Mix(c, Rgb(0xFFFFFF), lip * 0.6f * (1f - groove));
            float rim = Clamp01(1f - MathF.Abs(d + 0.008f) / 0.010f);
            c = Mix(c, Rgb(0x1E5A94), rim * 0.8f);
            float sx = x + 0.22f, sy = y - 0.52f;
            float star = Clamp01(1f - Len(sx, sy) / 0.035f) + Clamp01(1f - MathF.Abs(sx) / 0.009f) * Clamp01(1f - MathF.Abs(sy) / 0.10f)
                       + Clamp01(1f - MathF.Abs(sy) / 0.009f) * Clamp01(1f - MathF.Abs(sx) / 0.10f);
            c = Mix(c, (1f, 1f, 1f), Clamp01(star) * (polished ? 0.9f : 0.6f));
            return (c.r, c.g, c.b, a * 0.96f);
        });
    }

    /// <summary>Círculo de runas: dois anéis finos com marcas.</summary>
    static float[] RuneCircleTex(int c0 = 0x4ADCFF, int c1 = 0xE8FCFF) => Paint(256, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float a = Math.Max(Clamp01(1f - MathF.Abs(r - 0.92f) / 0.010f), Clamp01(1f - MathF.Abs(r - 0.70f) / 0.008f) * 0.8f);
        float ticks = Clamp01(1f - MathF.Abs(MathF.Sin(ang * 24f)) / 0.06f) * Clamp01(1f - MathF.Abs(r - 0.81f) / 0.05f);
        float dots = Clamp01(1f - Len(r - 0.81f, MathF.Sin(ang * 8f) * 0.8f) / 0.02f);
        a = Math.Max(a, Math.Max(ticks * 0.7f, dots));
        var c = Mix(Rgb(c0), Rgb(c1), a);
        float glow = Clamp01(1f - MathF.Abs(r - 0.92f) / 0.05f) * 0.3f;
        return (c.r, c.g, c.b, Math.Max(a, glow));
    });

    /// <summary>Coração (dois lóbulos e ponta para baixo).</summary>
    static float HeartShape(float x, float y)
    {
        // Coração clássico: (x² + y² − 1)³ − x²·y³ ≤ 0, convertido em distância aproximada.
        static float F(float X, float Y) { float q = X * X + Y * Y - 1f; return q * q * q - X * X * Y * Y * Y; }
        const float k = 0.66f;
        float X = x / (k * 0.92f), Y = (y + 0.06f) / (k * 1.08f);
        float f = F(X, Y);
        const float h = 0.004f;
        float gx = (F(X + h, Y) - F(X - h, Y)) / (2f * h), gy = (F(X, Y + h) - F(X, Y - h)) / (2f * h);
        return f / MathF.Max(MathF.Sqrt(gx * gx + gy * gy), 0.3f) * k;
    }

    /// <summary>Coração de gelo escuro: quatro faces (lóbulos e metades de baixo), arestas ciano.</summary>
    static float[] HeartIceTex(bool polished) => Paint(S, (x, y) =>
    {
        float d = HeartShape(x, y);
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float split = 0.18f - 0.30f * MathF.Abs(x);           // divisa entre lóbulos e parte de baixo
        bool upper = y > split, left = x < 0f;
        float lit = upper ? (left ? 0.85f : 0.45f) : (left ? 0.55f : 0.15f);
        var dark = Rgb(0x02060F); var mid = Rgb(0x0A1A3A); var light = Rgb(polished ? 0x2E5C9A : 0x234A80);
        var c = lit > 0.5f ? Mix(mid, light, (lit - 0.5f) * 2f) : Mix(dark, mid, lit * 2f);
        c = Mix(c, Rgb(0x010309), Clamp01(1f - MathF.Abs(Fbm(x * 5f + 3f, y * 5f - 2f) - 0.5f) / 0.02f) * 0.6f);
        float ridge = Math.Max(Clamp01(1f - MathF.Abs(x) / 0.010f) * Clamp01((0.30f - y) * 3f), Clamp01(1f - MathF.Abs(y - split) / 0.010f));
        c = Mix(c, Rgb(0x7ADFFF), ridge * 0.7f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.014f) / 0.014f);
        c = Mix(c, Rgb(0x8AE8FF), rim * 0.85f);
        float outer = Clamp01(1f - MathF.Abs(d + 0.003f) / 0.006f);
        c = Mix(c, Rgb(0x020814), outer * 0.8f);
        float sx = x + 0.30f, sy = y - 0.32f;
        float star = Clamp01(1f - Len(sx, sy) / 0.04f) + Clamp01(1f - MathF.Abs(sx) / 0.01f) * Clamp01(1f - MathF.Abs(sy) / 0.12f)
                   + Clamp01(1f - MathF.Abs(sy) / 0.01f) * Clamp01(1f - MathF.Abs(sx) / 0.12f);
        c = Mix(c, Rgb(0xE8FAFF), Clamp01(star) * (polished ? 0.9f : 0.6f));
        return (c.r, c.g, c.b, a);
    });

    static float[] CrackLines(List<(float x, float y)[]> lines, float width, float clipR, int color) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > clipR) return default;
        float d = 9f;
        foreach (var pts in lines)
            for (int j = 0; j + 1 < pts.Length; j++)
                d = MathF.Min(d, Seg(x, y, pts[j].x, pts[j].y, pts[j + 1].x, pts[j + 1].y) - width * (1f - j / (float)pts.Length * 0.7f));
        float core = Clamp01(0.5f - d * S * 0.5f);
        float glow = MathF.Exp(-Math.Max(d, 0f) / 0.03f) * 0.6f;
        var c = Mix(Rgb(color), (1f, 1f, 1f), core * 0.6f);
        return (c.r, c.g, c.b, Clamp01(Math.Max(core, glow)) * Clamp01((clipR - r) / 0.03f));
    });

    /// <summary>Lago congelado: rachaduras saindo do centro, maiores a cada estágio.</summary>
    static float[] LakeCrackTex(int stage)
    {
        var rng = new Random(77);
        var lines = new List<(float x, float y)[]>();
        int n = 7;
        float len = 0.30f + 0.20f * stage;
        for (int i = 0; i < n; i++)
        {
            float a = i * MathF.Tau / n + (float)rng.NextDouble() * 0.5f;
            float cx = MathF.Cos(a) * 0.30f, cy = MathF.Sin(a) * 0.30f;
            var pts = new List<(float, float)> { (cx, cy) };
            int steps = 3 + 2 * stage;
            for (int k = 0; k < steps; k++)
            {
                a += ((float)rng.NextDouble() - 0.5f) * 0.7f;
                cx += MathF.Cos(a) * len / 7f; cy += MathF.Sin(a) * len / 7f;
                pts.Add((cx, cy));
                if (k == 2 && stage >= 2) lines.Add(new[] { (cx, cy), (cx + MathF.Cos(a + 0.8f) * 0.12f, cy + MathF.Sin(a + 0.8f) * 0.12f) });
            }
            lines.Add(pts.ToArray());
        }
        return CrackLines(lines, 0.008f, 0.92f, 0x8AE8FF);
    }

    /// <summary>Armadura de gelo escuro: placas em volta da bola e ombreiras pontudas.</summary>
    static float[] ArmorTex() => Paint(256, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(x, y);      // 0 = cima
        float ball = 0.47f / 0.875f;                          // raio da bola nesta textura
        float aa = MathF.Abs(ang);
        float seg = ang / (MathF.Tau / 10f); float f = seg - MathF.Floor(seg);
        float gap = MathF.Min(f, 1f - f) * MathF.Tau / 10f * r;  // distância até a divisa entre placas
        float inner = ball + 0.04f, outer = ball + 0.20f + 0.04f * MathF.Cos(ang * 2f);
        // Ombreiras: pontas subindo a ~50° de cada lado.
        float spikeA = MathF.Abs(aa - 0.85f);
        outer += Clamp01(1f - spikeA / 0.22f) * 0.30f;
        float d = MathF.Max(inner - r, r - outer);
        if (aa > 2.6f) d = MathF.Max(d, (aa - 2.6f) * 0.5f);   // aberta embaixo
        float a = Cover(d * 0.7f, 256);
        if (a <= 0f) return default;
        // Faces: metade interna clara, externa escura (chanfro), com runas finas acesas.
        float mid = (inner + outer) * 0.5f;
        bool lit = (r < mid) == (x < 0f);
        var c = lit ? Mix(Rgb(0x0E2450), Rgb(0x3A6AA8), Clamp01(y + 0.6f)) : Mix(Rgb(0x02060F), Rgb(0x14325E), Clamp01(y + 0.6f));
        float ridge = Clamp01(1f - MathF.Abs(r - mid) / 0.006f);
        c = Mix(c, Rgb(0x7ADFFF), ridge * 0.6f);
        float rune = Clamp01(1f - MathF.Abs(MathF.Sin(ang * 14f)) / 0.12f) * Clamp01(1f - MathF.Abs(r - mid - 0.04f) / 0.02f);
        c = Mix(c, Rgb(0xBFF4FF), rune * 0.7f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.01f) / 0.012f);
        c = Mix(c, Rgb(0x9AF0FF), rim * 0.9f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Nuvens de neve em espiral (olho do furacão no meio).</summary>
    static float[] StormTex(float seed, float twist) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.93f) return default;
        float ang = MathF.Atan2(y, x);
        float sp = ang + MathF.Log(Math.Max(r, 0.02f)) * twist;
        float n = Fbm(MathF.Cos(sp) * 1.8f + seed, MathF.Sin(sp) * 1.8f + r * 2.5f);
        float arms = 0.5f + 0.5f * MathF.Sin(sp * 3f);
        float dens = Clamp01((n * 0.6f + arms * 0.5f - 0.45f) * 2.2f);
        float eye = Clamp01((r - 0.10f) / 0.12f);
        float a = dens * eye * Clamp01((0.93f - r) / 0.05f) * 0.9f;
        var c = Mix(Rgb(0x8AB8E0), Rgb(0xF4FAFF), dens);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Faixas de vento curvas em volta.</summary>
    static float[] WindTex() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float a = 0f;
        for (int k = 0; k < 5; k++)
        {
            float rr = 0.55f + 0.08f * k;
            float a0 = k * 1.3f;
            float da = ang - a0; da -= MathF.Floor(da / MathF.Tau) * MathF.Tau;
            float arc = Clamp01(1f - da / 1.6f) * Clamp01(da / 0.2f);
            a = Math.Max(a, Clamp01(1f - MathF.Abs(r - rr) / 0.012f) * arc);
        }
        return (0.9f, 0.97f, 1f, a * 0.7f);
    });

    /// <summary>Lâmina de neve (meia-lua fina).</summary>
    static float[] BladeTex() => Paint(64, (x, y) =>
    {
        float d = MathF.Max(Len(x, y + 0.6f) - 1.2f, -(Len(x, y + 0.9f) - 1.25f));
        d = MathF.Max(d, MathF.Abs(x) - 0.95f);
        float a = Cover(d * 0.5f, 64);
        var c = Mix(Rgb(0x8AD8FF), (1f, 1f, 1f), Clamp01(1f - MathF.Abs(x)));
        return (c.r, c.g, c.b, a * Clamp01(1f - MathF.Abs(x) * 0.8f));
    });

    /// <summary>Redemoinho de neve vertical (mais largo em cima); phase desloca as faixas.</summary>
    static float[] TornadoTex(float phase) => Paint(S, (x, y) =>
    {
        float u = (y + 1f) * 0.5f;                                  // 0 base .. 1 topo
        float cx = 0.10f * MathF.Sin(u * 5f + phase * MathF.Tau);
        float w = 0.10f + 0.55f * u * u;
        float k = (x - cx) / w;
        if (MathF.Abs(k) > 1f) return default;
        float bands = 0.5f + 0.5f * MathF.Sin((y * 9f - phase * MathF.Tau) + k * 2.2f);
        float n = Fbm(k * 2f + phase * 4f, y * 4f - phase * 3f);
        float edge = 1f - k * k;
        float a = edge * (0.35f + 0.65f * bands * n) * Clamp01(u / 0.1f) * Clamp01((1f - u) / 0.15f + 0.3f) * 0.85f;
        var c = Mix(Rgb(0x8AB8E0), Rgb(0xF4FAFF), bands);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Anel de vento elíptico com uma abertura (girando = trocar a fase).</summary>
    static float[] SwirlRing(float phase) => Paint(64, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x) - phase;
        ang -= MathF.Floor(ang / MathF.Tau) * MathF.Tau;
        float ring = Clamp01(1f - MathF.Abs(r - 0.80f) / 0.12f);
        float tail = Clamp01(ang / 4.6f);                    // vai clareando ao longo do giro, com uma abertura
        float front = y < 0f ? 1f : 0.55f;                   // parte da frente mais forte
        float a = ring * tail * tail * front;
        return (0.92f, 0.97f, 1f, a);
    });

    /// <summary>Bloco de gelo transparente quadrado (bordas chanfradas, bolhas presas).</summary>
    static float[] BlockTex(float half) => Paint(S, (x, y) =>
    {
        float d = Box(x, y, half, half) - 0.06f;
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float bevel = Clamp01(1f - (-d) / 0.08f);
        var c = Mix(Rgb(0xA8D8F8), (1f, 1f, 1f), bevel * 0.6f);
        float alpha = 0.16f + 0.55f * bevel * bevel;
        // Brilho diagonal no vidro e bolhas.
        float diag = Clamp01(1f - MathF.Abs(x + y - 0.5f) / 0.06f) * 0.35f + Clamp01(1f - MathF.Abs(x + y - 0.75f) / 0.025f) * 0.3f;
        alpha = Math.Max(alpha, diag);
        float bub = 0f;
        for (int k = 0; k < 9; k++)
        {
            float bx = (GemStyle.Hash(k * 7 + 3) - 0.5f) * 1.5f, by = (GemStyle.Hash(k * 11 + 5) - 0.5f) * 1.5f, br = 0.015f + 0.02f * GemStyle.Hash(k);
            bub = Math.Max(bub, Clamp01(1f - MathF.Abs(Len(x - bx, y - by) - br) / 0.008f));
        }
        alpha = Math.Max(alpha, bub * 0.7f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.006f) / 0.008f);
        c = Mix(c, Rgb(0x2A6AA8), rim * 0.8f);
        alpha = Math.Max(alpha, rim);
        return (c.r, c.g, c.b, a * alpha);
    });

    /// <summary>Geada nos cantos e bordas do bloco.</summary>
    static float[] BlockFrostTex(float half) => Paint(S, (x, y) =>
    {
        float d = Box(x, y, half, half) - 0.06f;
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float edge = Clamp01(1f - (-d) / 0.35f);
        float corner = Clamp01((MathF.Abs(x) + MathF.Abs(y) - 1.0f) / 0.5f);
        float n = Fbm(x * 8f, y * 8f);
        float alpha = Clamp01((edge * 0.6f + corner * 0.6f) * (0.4f + 0.8f * n) - 0.15f);
        var c = Mix(Rgb(0xD8ECFA), (1f, 1f, 1f), n);
        return (c.r, c.g, c.b, a * alpha * 0.75f);
    });

    /// <summary>Trincas no bloco (saem das bordas para dentro, maiores a cada estágio).</summary>
    static float[] BlockCrackTex(int stage, float half)
    {
        var rng = new Random(31);
        var lines = new List<(float x, float y)[]>();
        for (int i = 0; i < 6; i++)
        {
            float side = i % 4; float t0 = (float)rng.NextDouble() * 1.4f - 0.7f;
            float sx = side == 0 ? -half : side == 1 ? half : t0, sy = side < 2 ? t0 : (side == 2 ? -half : half);
            float a = MathF.Atan2(-sy, -sx) + ((float)rng.NextDouble() - 0.5f) * 0.8f;
            var pts = new List<(float, float)> { (sx, sy) };
            int steps = 2 + 2 * stage;
            for (int k = 0; k < steps; k++)
            {
                a += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                sx += MathF.Cos(a) * 0.07f; sy += MathF.Sin(a) * 0.07f;
                pts.Add((sx, sy));
            }
            lines.Add(pts.ToArray());
        }
        return CrackLines(lines, 0.007f, 1.4f, 0xD8F4FF);
    }

    static float IceShard(float x, float y) => PolyN(x, y, new[] { 0f, 0.95f, 0.26f, 0.5f, 0.22f, -0.6f, 0f, -0.95f, -0.26f, -0.5f, -0.22f, 0.6f });

    /// <summary>Cristal de gelo escuro: cada ponta dividida em face clara e face escura.</summary>
    static float[] StarTex(bool polished) => Paint(S, (x, y) =>
    {
        float d = IceStar(x, y);
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float ang = MathF.Atan2(y, x);
        float step = MathF.PI / 4f;
        float k = MathF.Round((ang - MathF.PI / 2f) / step);
        float pa = MathF.PI / 2f + k * step;                 // eixo da ponta mais próxima
        float side = MathF.Sin(ang - pa);                     // de que lado do eixo estamos
        // Normal da face: perpendicular ao eixo, inclinada para o lado; luz de cima à esquerda.
        float nx = -MathF.Sin(pa) * MathF.Sign(side), ny = MathF.Cos(pa) * MathF.Sign(side);
        float lit = Clamp01(0.5f + 0.5f * (nx * -0.6f + ny * 0.8f));
        var dark = Rgb(0x0B2246); var mid = Rgb(0x2E68A8); var light = Rgb(0x9AD4FF);
        var c = lit > 0.5f ? Mix(mid, light, (lit - 0.5f) * 2f) : Mix(dark, mid, lit * 2f);
        // Aresta central de cada ponta (linha clara) e contorno escuro.
        float r = Len(x, y);
        float ridge = Clamp01(1f - MathF.Abs(side) * r / 0.010f) * Clamp01(r / 0.15f);
        c = Mix(c, Rgb(0xD8F0FF), ridge * 0.8f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.012f) / 0.016f);
        c = Mix(c, Rgb(0x05132A), rim * 0.9f);
        // Miolo brilhante e estrela de reflexo.
        c = Mix(c, Rgb(0xE6F6FF), Clamp01(1f - r / 0.10f) * 0.7f);
        float sx = x + 0.12f, sy = y - 0.30f;
        float star = Clamp01(1f - Len(sx, sy) / 0.04f) + Clamp01(1f - MathF.Abs(sx) / 0.01f) * Clamp01(1f - MathF.Abs(sy) / 0.12f)
                   + Clamp01(1f - MathF.Abs(sy) / 0.01f) * Clamp01(1f - MathF.Abs(sx) / 0.12f);
        c = Mix(c, (1f, 1f, 1f), Clamp01(star) * (polished ? 0.9f : 0.6f));
        if (polished) c = Mix(c, Rgb(0xBFE6FF), Clamp01(lit - 0.6f) * 0.5f);   // faces claras mais vivas
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Faixa de luz diagonal recortada no formato da joia (um quadro do brilho em loop).</summary>
    static float[] ShineTex(Sd shape, float pos) => Paint(S, (x, y) =>
    {
        float d = shape(x, y);
        float a = Cover(d + 0.01f, S);
        if (a <= 0f) return default;
        float u = (x + y) * 0.7071f - pos;
        float band = MathF.Exp(-u * u / 0.018f) + 0.5f * MathF.Exp(-(u + 0.20f) * (u + 0.20f) / 0.003f);
        return (0.92f, 0.98f, 1f, a * Clamp01(band));
    });

    /// <summary>Cintilação de 4 pontas.</summary>
    static float[] GlintTex() => Paint(64, (x, y) =>
    {
        float c1 = Clamp01(1f - MathF.Abs(x) / 0.05f) * Clamp01(1f - MathF.Abs(y));
        float c2 = Clamp01(1f - MathF.Abs(y) / 0.05f) * Clamp01(1f - MathF.Abs(x));
        float core = Clamp01(1f - Len(x, y) / 0.25f);
        float a = Clamp01(MathF.Max(c1, c2) + core * core);
        return (0.92f, 0.98f, 1f, a);
    });

    static float Eye(float x, float y)
    {
        // Amêndoa (interseção de dois círculos) com pontas finas nos cantos.
        float v = MathF.Max(Len(x, y + 0.45f) - 0.85f, Len(x, y - 0.45f) - 0.85f);
        return v;
    }

    static (float, float, float)? EyeColor(float x, float y, float d)
    {
        float r = Len(x, y);
        // Pupila em fenda vertical.
        float slit = MathF.Max(Len(x + 0.28f, y) - 0.33f, Len(x - 0.28f, y) - 0.33f);
        if (slit < 0f) return (0.02f, 0.0f, 0.03f);
        if (r < 0.29f)
        {
            // Íris verde ácida, brilhando mais no centro.
            float k = Clamp01(1f - r / 0.29f);
            var c = Mix(Rgb(0x2E9A1A), Rgb(0xB8FF6A), k * k);
            float ring = Clamp01(1f - MathF.Abs(r - 0.27f) / 0.025f);
            return Mix(c, Rgb(0x0E3A06), ring * 0.8f);
        }
        return null; // resto: esclera roxa escura, sombreada como joia
    }

    static float Bolt(float x, float y)
    {
        // Raio em zigue-zague.
        return PolyN(x, y, new[]
        {
            0.08f, 0.86f, 0.46f, 0.86f, 0.14f, 0.16f, 0.42f, 0.16f,
            -0.28f, -0.86f, -0.04f, -0.08f, -0.32f, -0.08f,
        });
    }

    static float SkullSilhouette(float x, float y)
        => SMin(Len(x / 1.06f, y - 0.12f) - 0.56f, Box(x, y + 0.36f, 0.29f, 0.17f) - 0.07f, 0.12f);

    static float Skull(float x, float y)
    {
        // Crânio largo + mandíbula mais estreita, unidos suavemente.
        float head = Len(x / 1.06f, y - 0.12f) - 0.56f;
        float jaw = Box(x, y + 0.36f, 0.29f, 0.17f) - 0.07f;
        float d = SMin(head, jaw, 0.12f);
        // Só dois cortes curtos entre os dentes, saindo da base da mandíbula.
        float teeth = Box(MathF.Abs(x) - 0.10f, y + 0.56f, 0.024f, 0.075f);
        return MathF.Max(d, -teeth);
    }

    // Olhos grandes e angulosos, mais baixos perto do nariz (cara de brava).
    static float SkullEye(float x, float y)
        => PolyN(MathF.Abs(x), y, new[] { 0.10f, 0.02f, 0.40f, 0.08f, 0.41f, 0.24f, 0.28f, 0.31f, 0.12f, 0.21f }) - 0.045f;

    static float SkullNose(float x, float y)
        => Poly(x, y, -0.075f, -0.07f, 0.075f, -0.07f, 0f, -0.21f) - 0.015f;

    // Olhos e nariz escuros dentro do cristal, com um brilho roxo no fundo.
    static (float, float, float)? SkullColor(float x, float y, float d)
    {
        float e = MathF.Min(SkullEye(x, y), SkullNose(x, y));
        if (e > 0.012f) return null;
        float inner = Clamp01(-e / 0.08f);
        var c = Mix(Rgb(0x0C0216), Rgb(0x6A1FB0), inner * 0.55f);
        float lip = Clamp01(1f - MathF.Abs(e) / 0.012f);
        return Mix(c, Rgb(0x1A0430), lip);
    }

    // ───────────────────────── camadas básicas ─────────────────────────

    static float[] BallTex(int dark, int light, int rim) => Paint(S, (x, y) =>
    {
        float d = Len(x, y) - 0.94f;
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float nz = MathF.Sqrt(MathF.Max(0f, 1f - (x * x + y * y) / (0.94f * 0.94f)));
        float l = Clamp01(0.55f + 0.45f * (-0.45f * x + 0.55f * y + 0.70f * nz));
        var c = Mix(Rgb(dark), Rgb(light), l);
        float spec = MathF.Pow(Clamp01(1f - Len(x + 0.38f, y - 0.42f) / 0.38f), 2f) * 0.5f;
        c = Mix(c, (1f, 1f, 0.95f), spec);
        float rimK = Clamp01(1f - MathF.Abs(d + 0.025f) / 0.03f);
        c = Mix(c, Rgb(rim), rimK * 0.8f);
        return (c.r, c.g, c.b, a);
    });

    static float[] GemTex(int size, Sd shape, int dark, int light, int edge, GemColorFn? custom) => Paint(size, (x, y) =>
    {
        float d = shape(x, y);
        float a = Cover(d, size);
        if (a <= 0f) return default;
        var c = Mix(Rgb(dark), Rgb(light), Clamp01(0.5f + 0.45f * y));
        var cc = custom?.Invoke(x, y, d);
        if (cc.HasValue) c = cc.Value;
        // Bisel: borda de cima clara, contorno escuro, faceta brilhante.
        float up = shape(x, y - 0.035f);
        if (up > -0.005f) c = Mix(c, (1f, 0.97f, 0.9f), Clamp01(1f - MathF.Abs(up) / 0.03f) * 0.55f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.018f) / 0.022f);
        c = Mix(c, Rgb(edge), rim * 0.85f);
        float glint = MathF.Pow(Clamp01(1f - Len(x + 0.18f, y - 0.30f) / 0.20f), 2f) * 0.45f;
        c = Mix(c, (1f, 1f, 1f), glint);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>
    /// Joia de cristal lapidado: borda chanfrada dividida em facetas planas (cada uma
    /// iluminada de um jeito), mesa plana no meio, linhas de corte claras, brilho
    /// especular forte e um pouco de transparência.
    /// </summary>
    /// <summary>
    /// Joia de cristal lapidada em facetas: cada parte da forma se divide pela linha do meio
    /// (onde as bordas se encontram) em faces planas claras e escuras, com arestas brilhantes,
    /// contorno escuro e reflexo em estrela. Mesmo princípio da estrela de gelo.
    /// </summary>
    static float[] CrystalTex(Sd shape, int dark, int light, int edge, GemColorFn? custom, float facets = 8f, bool polished = false, Sd? facetShape = null, float white = 1f) => Paint(S, (x, y) =>
    {
        float d = shape(x, y);
        float a = Cover(d, S);
        if (a <= 0f) return default;

        var fs = facetShape ?? shape;   // forma usada para as facetas (pode ser a silhueta lisa)
        int Sector(float px, float py)
        {
            const float h = 0.008f;
            float gx = fs(px + h, py) - fs(px - h, py);
            float gy = fs(px, py + h) - fs(px, py - h);
            float ang = MathF.Atan2(gy, gx);
            return (int)MathF.Round(ang / (MathF.Tau / facets));
        }

        // Normal da face = direção (arredondada) até a borda mais próxima, inclinada para fora.
        int sec = Sector(x, y);
        float q = sec * (MathF.Tau / facets);
        const float slope = 0.75f;
        float nx = MathF.Cos(q) * MathF.Sin(slope), ny = MathF.Sin(q) * MathF.Sin(slope), nz = MathF.Cos(slope);
        const float lx = -0.48f, ly = 0.58f, lz = 0.66f;
        float lit = Clamp01(nx * lx + ny * ly + nz * lz);
        float hz = lz + 1f, hl = Len3(lx, ly, hz);
        float spec = MathF.Pow(Clamp01((nx * lx + ny * ly + nz * hz) / hl), 30f);

        var dk = Rgb(dark); var lt = Rgb(light);
        var mid = Mix(dk, lt, 0.5f);
        float k = Clamp01((lit - 0.25f) / 0.6f);
        var c = k < 0.5f ? Mix(Mix(dk, (0f, 0f, 0f), 0.25f), mid, k * 2f) : Mix(mid, lt, (k - 0.5f) * 2f);

        var cc = custom?.Invoke(x, y, d);
        if (cc.HasValue) c = Mix(cc.Value, c, 0.15f);   // detalhes próprios (íris, olhos da caveira) quase intactos

        // Arestas: onde a faceta muda (linha do meio de cada parte e divisas entre facetas).
        const float e = 0.012f;
        bool seam = Sector(x + e, y) != sec || Sector(x - e, y) != sec || Sector(x, y + e) != sec || Sector(x, y - e) != sec;
        if (seam && !cc.HasValue && d < -0.02f) c = Mix(c, Mix(lt, (1f, 1f, 1f), 0.55f * white), 0.65f);

        // Contorno, reflexo e brilho em estrela.
        float rim = Clamp01(1f - MathF.Abs(d + 0.012f) / 0.016f);
        c = Mix(c, Rgb(edge), rim * 0.9f);
        c = Mix(c, (1f, 1f, 1f), Clamp01(spec * 0.8f * white));
        float sx = x + 0.18f, sy = y - 0.28f;
        float star = Clamp01(1f - Len(sx, sy) / 0.05f) + Clamp01(1f - MathF.Abs(sx) / 0.011f) * Clamp01(1f - MathF.Abs(sy) / 0.14f)
                   + Clamp01(1f - MathF.Abs(sy) / 0.011f) * Clamp01(1f - MathF.Abs(sx) / 0.14f);
        star *= shape(x, y) < -0.01f ? 1f : 0f;
        c = Mix(c, (1f, 1f, 1f), Clamp01(star) * (polished ? 0.9f : 0.65f) * Math.Max(white, 0.5f));
        if (polished)
        {
            c = Mix(c, Mix(lt, (1f, 1f, 1f), 0.35f), Clamp01(k - 0.6f) * 0.6f);  // faces claras mais vivas
            float s2x = x - 0.22f, s2y = y + 0.16f;
            float star2 = Clamp01(1f - Len(s2x, s2y) / 0.035f) + Clamp01(1f - MathF.Abs(s2x) / 0.009f) * Clamp01(1f - MathF.Abs(s2y) / 0.09f)
                        + Clamp01(1f - MathF.Abs(s2y) / 0.009f) * Clamp01(1f - MathF.Abs(s2x) / 0.09f);
            star2 *= shape(x, y) < -0.01f ? 1f : 0f;
            c = Mix(c, (1f, 1f, 1f), Clamp01(star2) * 0.7f);
        }
        return (Clamp01(c.Item1), Clamp01(c.Item2), Clamp01(c.Item3), a * 0.97f);
    });

    /// <summary>Acha as pontas da forma (máximos locais do raio da borda) para a cintilação.</summary>
    static void FindTips(Sd shape, out float[] tx, out float[] ty)
    {
        const int n = 72;
        var r = new float[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * MathF.Tau / n, dx = MathF.Cos(a), dy = MathF.Sin(a);
            float rr = 0.02f;
            while (rr < 0.98f && shape(dx * rr, dy * rr) < 0f) rr += 0.01f;
            r[i] = rr;
        }
        var xs = new List<float>(); var ys = new List<float>();
        for (int i = 0; i < n; i++)
        {
            bool max = r[i] > 0.4f;
            for (int k = 1; k <= 3 && max; k++)
                if (r[(i + k) % n] > r[i] || r[(i - k + n) % n] >= r[i]) max = false;
            if (!max) continue;
            float a = i * MathF.Tau / n;
            xs.Add(MathF.Cos(a) * (r[i] - 0.06f)); ys.Add(MathF.Sin(a) * (r[i] - 0.06f));
        }
        if (xs.Count == 0)   // forma arredondada: usa alguns pontos da borda
            for (int i = 0; i < 4; i++)
            {
                int idx = (i * n / 4 + n / 8) % n;
                float a = idx * MathF.Tau / n;
                xs.Add(MathF.Cos(a) * r[idx] * 0.85f); ys.Add(MathF.Sin(a) * r[idx] * 0.85f);
            }
        tx = xs.ToArray(); ty = ys.ToArray();
    }

    static float[] CrystalSmall(int size, Sd shape, int dark, int light, int edge) => Paint(size, (x, y) =>
    {
        float d = shape(x, y);
        float a = Cover(d, size);
        if (a <= 0f) return default;
        // Duas metades (esquerda clara, direita escura) = cristal facetado em miniatura.
        var c = x < 0f ? Mix(Rgb(light), (1f, 1f, 1f), 0.3f) : Rgb(dark);
        c = Mix(c, Rgb(light), Clamp01(0.4f + y * 0.4f) * 0.4f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.03f) / 0.04f);
        c = Mix(c, Rgb(edge), rim * 0.6f);
        return (c.r, c.g, c.b, a * 0.95f);
    });

    static float ShardShape(float x, float y) => PolyN(x, y, new[] { 0f, 0.95f, 0.42f, 0.1f, 0f, -0.95f, -0.42f, 0.1f });

    /// <summary>Aro em volta da bola: prata lisa ou ouro com pequenas gemas.</summary>
    /// <param name="metal">0 bronze, 1 prata, 2 ouro (com gemas).</param>
    static float[] Ring(int metal) => Paint(S, (x, y) =>
    {
        bool gold = metal == 2;
        float r = Len(x, y);
        float inner = gold ? 0.86f : 0.89f, outer = 0.99f;
        float d = MathF.Max(inner - r, r - outer);
        float a = Cover(d, S);
        float ang = MathF.Atan2(y, x);
        // Metal: claro em cima à esquerda, escuro embaixo à direita, com faixa brilhante no meio.
        float l = Clamp01(0.5f + 0.5f * (-0.6f * x + 0.8f * y) / Math.Max(r, 0.01f));
        float mid = 1f - MathF.Abs((r - (inner + outer) / 2f) / ((outer - inner) / 2f));
        var dark = metal == 2 ? Rgb(0x7A4E0A) : metal == 1 ? Rgb(0x5E6670) : Rgb(0x4A2810);
        var light = metal == 2 ? Rgb(0xFFE38A) : metal == 1 ? Rgb(0xF2F6FA) : Rgb(0xE0A06A);
        var c = Mix(dark, light, Clamp01(l * 0.7f + mid * 0.4f));
        float alpha = a;
        if (gold)
        {
            // 8 pequenas gemas vermelhas encravadas no aro.
            float step = MathF.Tau / 8f;
            float qa = MathF.Round(ang / step) * step;
            float gd = Len(x - MathF.Cos(qa) * 0.925f, y - MathF.Sin(qa) * 0.925f) - 0.045f;
            float ga = Cover(gd, S);
            if (ga > 0f)
            {
                var gc = Mix(Rgb(0x8A0018), Rgb(0xFF6A7A), Clamp01(0.5f - gd * 8f + (y - MathF.Sin(qa) * 0.925f) * 6f));
                c = Mix(c, gc, ga);
                alpha = Math.Max(alpha, ga);
            }
        }
        if (alpha <= 0f) return default;
        return (c.r, c.g, c.b, alpha);
    });

    /// <summary>Brilho suave no formato da joia (luz interna).</summary>
    static float[] GlowTex(Sd shape, int color) => Paint(S, (x, y) =>
    {
        float d = shape(x / 0.9f, y / 0.9f) * 0.9f;
        float a = Clamp01(1f - (d + 0.05f) / 0.18f);
        var c = Mix(Rgb(color), (1f, 1f, 1f), 0.5f);
        return (c.r, c.g, c.b, a * a * 0.8f);
    });

    /// <summary>Anel fino do progresso até o próximo nível (preenchido pelo Unity).</summary>
    static float[] ProgressRing() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float d = MathF.Abs(r - 0.955f) - 0.03f;
        return (1f, 1f, 1f, Cover(d, S));
    });

    // ───────── motivos do nível Lendário ─────────

    /// <summary>Sangue: aglomerado de pontas de cristal (uma alta no meio, duas de cada lado, abrindo em leque).</summary>
    static float BloodCrystal(float x, float y)
    {
        float d = 9f;
        // (ângulo em graus a partir da vertical, comprimento, meia-largura)
        var shards = new (float ang, float len, float w)[] { (0, 1.05f, 0.16f), (-15, 0.88f, 0.14f), (16, 0.90f, 0.14f), (-31, 0.66f, 0.12f), (30, 0.64f, 0.12f) };
        foreach (var (angDeg, len, w) in shards)
        {
            Rot(x, y + 0.55f, angDeg * MathF.PI / 180f, out float px, out float py);
            // ponta de cristal: corpo hexagonal alongado terminando em bico
            d = MathF.Min(d, PolyN(px, py, new[] { -w, 0f, w, 0f, w, len - w * 1.6f, 0f, len + 0.02f, -w, len - w * 1.6f }));
        }
        return MathF.Max(d, -(y + 0.62f));   // base reta
    }

    /// <summary>Sangue: poder obscuro dentro da bola (redemoinho vermelho, mais forte no meio).</summary>
    static float[] InnerPower(float seed, int c0 = 0x8A0018, int c1 = 0xFF3A50, int c2 = 0xFFB0B8) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.90f) return default;
        float ang = MathF.Atan2(y, x);
        // Redemoinho: o ruído gira mais perto do centro.
        float sw = ang + r * 3.2f;
        float n = Fbm(MathF.Cos(sw) * (1.2f + r * 2f) + seed, MathF.Sin(sw) * (1.2f + r * 2f) - seed);
        float core = Clamp01(1f - r / 0.75f);
        float k = Clamp01((n - 0.42f) * 2.6f) * (0.35f + 0.65f * core);
        var c = Mix(Rgb(c0), Rgb(c1), Clamp01(k * 1.2f));
        c = Mix(c, Rgb(c2), Clamp01((k - 0.75f) * 3f) * 0.5f);
        float edge = Clamp01((0.90f - r) / 0.15f);
        return (c.r, c.g, c.b, Clamp01(k * 1.5f) * edge);
    });

    /// <summary>Sangue: corpo de vidro rubi da gota (escuro por dentro, borda acesa).</summary>
    static float[] GlassDrop(bool polished) => Paint(S, (x, y) =>
    {
        float d = Drop(x, y);
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float depth = Clamp01(-d / 0.30f);
        var c = Mix(Rgb(0x6A0A1E), Rgb(0x1A0006), depth);
        float inner = Clamp01(1f - MathF.Abs(d + 0.035f) / 0.025f);              // brilho interno da borda
        c = Mix(c, Rgb(polished ? 0xFF6A7A : 0xE0404E), inner * Clamp01(0.3f + x * 0.9f + 0.4f) * 0.6f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.010f) / 0.014f);
        c = Mix(c, Rgb(0x14000A), rim * 0.9f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Sangue líquido dentro da gota; tilt = inclinação da superfície (graus).</summary>
    static float[] LiquidTex(float tilt) => Paint(S, (x, y) =>
    {
        float d = Drop(x, y) + 0.035f;               // um pouco para dentro do vidro
        if (d > 0.01f) return default;
        float ph = tilt * MathF.PI / 180f;
        const float level = -0.02f;
        float wave = 0.010f * MathF.Sin(x * 14f + tilt * 0.3f);
        float sd = (y - level) * MathF.Cos(ph) - x * MathF.Sin(ph) - wave;   // >0 acima da superfície
        float a = Cover(MathF.Max(d, sd), S);
        if (a <= 0f) return default;
        float depth = Clamp01(-sd / 0.45f);
        var c = Mix(Rgb(0xC8142E), Rgb(0x4A0010), depth);
        c = Mix(c, Rgb(0xFF2A44), Clamp01(1f - Len(x + 0.05f, y + 0.30f) / 0.30f) * 0.35f);   // brilho no fundo
        float men = Clamp01(1f - MathF.Abs(sd + 0.012f) / 0.014f);             // menisco brilhante
        c = Mix(c, Rgb(0xFF7A88), men * 0.85f);
        return (c.r, c.g, c.b, a * 0.96f);
    });

    /// <summary>Reflexos do vidro por cima do líquido: faixa curva, borda clara e estrela.</summary>
    static float[] GlassShine() => Paint(S, (x, y) =>
    {
        float d = Drop(x, y);
        if (d > 0f) return default;
        float band = MathF.Abs(Len(x + 0.06f, y + 0.20f) - 0.36f);
        float arc = Clamp01(1f - band / 0.035f) * Clamp01((-x - 0.05f) * 4f) * Clamp01((y + 0.15f) * 4f);
        float tipLine = Clamp01(1f - MathF.Abs(x + 0.06f - (0.84f - y) * 0.18f) / 0.022f) * Clamp01((y - 0.12f) * 4f) * Clamp01((0.70f - y) * 6f);
        float rimR = Clamp01(1f - MathF.Abs(d + 0.03f) / 0.02f) * Clamp01((x - 0.10f) * 3f) * Clamp01((-y + 0.05f) * 2f);
        float sx = x + 0.18f, sy = y + 0.02f;
        float star = Clamp01(1f - Len(sx, sy) / 0.04f) + Clamp01(1f - MathF.Abs(sx) / 0.009f) * Clamp01(1f - MathF.Abs(sy) / 0.12f)
                   + Clamp01(1f - MathF.Abs(sy) / 0.009f) * Clamp01(1f - MathF.Abs(sx) / 0.12f);
        float a = Clamp01(arc * 0.65f + tipLine * 0.55f + rimR * 0.45f + Clamp01(star) * 0.9f);
        return (1f, 0.93f, 0.95f, a);
    });

    /// <summary>Elo em brasa (por cima do dourado quando a corrente está no limite).</summary>
    static float[] ChainHot(bool face) => Paint(64, (x, y) =>
    {
        float d = face
            ? MathF.Abs(Len(x / 1.0f, y / 0.68f) * 0.68f - 0.44f) - 0.19f
            : Box(x, y, 0.82f, 0.34f) - 0.14f;
        float glow = Clamp01(1f - (d + 0.02f) / 0.14f);
        if (glow <= 0f) return default;
        float core = Clamp01(-d / 0.10f);
        var c = Mix(Rgb(0xE01828), Rgb(0xFFB040), core);
        return (c.r, c.g, c.b, glow * (0.5f + 0.5f * core));
    });

    /// <summary>Faísca alongada (laranja quente).</summary>
    static float[] SparkTex() => Paint(32, (x, y) =>
    {
        float r = Len(x, y * 1.6f);
        float a = Clamp01(1f - r / 0.9f);
        if (a <= 0f) return default;
        var c = Mix(Rgb(0xFF5020), Rgb(0xFFF0B0), Clamp01(1f - r / 0.4f));
        return (c.r, c.g, c.b, a * a);
    });

    /// <summary>Veias no fundo do buraco: rede orgânica (bright = camada que pulsa).</summary>
    static float[] HoleVeins(Sd shape, bool bright, int d0 = 0x3A0008, int d1 = 0x9A0A22, int b0 = 0xFF2A44, int b1 = 0xFFB0B8) => Paint(S, (x, y) =>
    {
        float inside = shape(x, y) - 0.02f;
        if (inside > 0f) return default;
        float n1 = Fbm(x * 3.1f + 4.2f, y * 3.1f - 1.3f), n2 = Fbm(x * 5.3f - 2.1f, y * 5.3f + 7.7f);
        float v = MathF.Max(Clamp01(1f - MathF.Abs(n1 - 0.5f) / 0.035f), 0.7f * Clamp01(1f - MathF.Abs(n2 - 0.5f) / 0.028f));
        float fade = Clamp01(-inside / 0.06f);
        if (v <= 0f) return default;
        var c = bright ? Mix(Rgb(b0), Rgb(b1), v * 0.5f) : Mix(Rgb(d0), Rgb(d1), v);
        return (c.r, c.g, c.b, v * fade * (bright ? 0.9f : 0.8f));
    });

    /// <summary>Par de olhos vermelhos em fenda, brilhando (escuridão da Lendária).</summary>
    static float[] EyesTex() => Paint(128, (x, y) =>
    {
        float ax = MathF.Abs(x);
        // Olho amendoado inclinado (canto de fora mais alto).
        float ex = ax - 0.42f, ey = (y - (ax - 0.42f) * 0.35f) * 3.0f;
        float eye = Len(ex / 0.36f, ey / 0.9f) - 1f;
        float glow = Clamp01(1f - Len(ex / 0.6f, ey / 2.2f));
        float inEye = Cover(eye * 0.3f, 128);
        float pupil = Clamp01(1f - MathF.Abs(ex) / 0.035f) * inEye;
        var c = Mix(Rgb(0xFF2030), Rgb(0xFFD0A0), Clamp01(1f - Len(ex / 0.2f, ey / 0.5f)) * 0.6f);
        c = Mix(c, Rgb(0x100000), pupil);
        float a = Math.Max(inEye, glow * glow * 0.6f);
        if (inEye <= 0f) c = Rgb(0xFF1828);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Elo de corrente: de frente (oval vazado) ou de lado (barra), ferro escuro com reflexo vermelho.</summary>
    static float[] ChainLink(bool face) => Paint(64, (x, y) =>
    {
        float d = face
            ? MathF.Abs(Len(x / 1.0f, y / 0.68f) * 0.68f - 0.44f) - 0.19f
            : Box(x, y, 0.82f, 0.34f) - 0.14f;
        float a = Cover(d * 0.5f, 64);
        if (a <= 0f) return default;
        // Ouro: escuro embaixo, claro em cima, brilho no meio do elo e contorno escuro para destacar.
        var c = Mix(Rgb(0x7A5210), Rgb(0xF6D266), Clamp01(0.45f + y * 0.9f));
        float shine = Clamp01(1f - MathF.Abs(d + 0.10f) / 0.06f);
        c = Mix(c, Rgb(0xFFF6C8), shine * 0.6f);
        float rim = Clamp01(1f - MathF.Abs(d + 0.015f) / 0.03f);
        c = Mix(c, Rgb(0x2A1A04), rim * 0.8f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Sangue: gola alta de capa de vampiro (pontas para cima e para fora), preta com forro vermelho.</summary>
    static float[] VampireCollar() => Paint(256, (x, y) =>
    {
        float ax = MathF.Abs(x);
        // Asa da gola: sobe pela lateral da bola e abre em ponta.
        float outer = PolyN(ax, y, new[] { 0.30f, -0.55f, 0.62f, -0.30f, 0.80f, 0.20f, 0.98f, 0.92f, 0.70f, 0.55f, 0.48f, 0.42f, 0.26f, 0.05f });
        float inner = PolyN(ax, y, new[] { 0.36f, -0.42f, 0.58f, -0.22f, 0.72f, 0.20f, 0.86f, 0.70f, 0.66f, 0.47f, 0.46f, 0.33f, 0.31f, 0.02f });
        float a = Cover(outer * 0.6f, 256);
        if (a <= 0f) return default;
        (float r, float g, float b) c;
        if (inner < 0f)
        {
            // Forro vermelho: dobras (faixas) e brilho de cetim.
            float fold = 0.5f + 0.5f * MathF.Sin(ax * 34f + y * 6f);
            c = Mix(Rgb(0x5A0010), Rgb(0xD01A34), fold * 0.7f + Clamp01(y) * 0.3f);
        }
        else
        {
            c = Mix(Rgb(0x06030A), Rgb(0x2A2030), Clamp01(0.4f + y * 0.6f) * 0.6f);
            float rim = Clamp01(1f - MathF.Abs(outer + 0.012f) / 0.015f);
            c = Mix(c, Rgb(0x9A8AA0), rim * 0.5f);
        }
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Morcego com as asas em uma pose (lift: 0.55 para cima, 0 no meio, -0.5 para baixo).</summary>
    static float[] BatPose(float lift) => Paint(64, (x, y) =>
    {
        float ax = MathF.Abs(x);
        // Asa: as pontas sobem/descem conforme a pose (dobra a partir do ombro).
        float wy(float px, float py) => py - lift * Clamp01((px - 0.12f) / 0.85f) * 0.9f;
        float wing = PolyN(ax, wy(ax, y), new[] { 0.06f, 0.12f, 0.34f, 0.30f, 0.62f, 0.26f, 0.97f, 0.10f, 0.80f, -0.02f,
                                                  0.70f, 0.04f, 0.58f, -0.10f, 0.46f, -0.04f, 0.32f, -0.18f, 0.18f, -0.04f, 0.06f, -0.10f });
        float body = Len(x / 0.6f, (y + 0.02f)) * 0.6f - 0.11f;
        float head = Len(x, y - 0.18f) - 0.09f;
        float ears = Poly(ax, y, 0.02f, 0.22f, 0.09f, 0.22f, 0.07f, 0.36f);
        float d = MathF.Min(MathF.Min(wing, body), MathF.Min(head, ears));
        float a = Cover(d, 64);
        if (a <= 0f) return default;
        var c = Rgb(0x0E0208);
        float rim = Clamp01(1f - MathF.Abs(d + 0.025f) / 0.03f);
        c = Mix(c, Rgb(0xC81E32), rim * 0.8f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Sangue: veios vermelhos brilhando na superfície da bola, como o chão rachado da arena.</summary>
    static float[] BallVeins() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.92f) return default;
        // Rachaduras: linhas onde o ruído cruza um nível (dá veios ramificados).
        float n1 = Fbm(x * 3.2f + 5.1f, y * 3.2f - 2.3f), n2 = Fbm(x * 6.5f - 1.7f, y * 6.5f + 4.4f);
        float v1 = Clamp01(1f - MathF.Abs(n1 - 0.5f) / 0.022f);
        float v2 = Clamp01(1f - MathF.Abs(n2 - 0.5f) / 0.018f) * 0.7f;
        float vein = Math.Max(v1, v2);
        float glow = MathF.Exp(-MathF.Abs(n1 - 0.5f) / 0.05f) * 0.45f;
        float edge = Clamp01((0.92f - r) / 0.08f);                  // some na borda (curvatura)
        var c = Mix(Rgb(0xC8102A), Rgb(0xFF6A7A), vein * 0.6f);
        return (c.r, c.g, c.b, Clamp01(Math.Max(vein, glow)) * edge);
    });

    /// <summary>Sangue: escuridão negro-avermelhada em volta (névoa densa nas bordas).</summary>
    static float[] Darkness() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float n = Fbm(x * 2.6f + 1.3f, y * 2.6f - 3.7f);
        float body = Clamp01(1f - (r - 0.25f) / 0.75f);
        float wisps = Clamp01((n - 0.35f) * 2.2f);
        float a = body * (0.55f + 0.45f * wisps) * 0.9f;
        var c = Mix(Rgb(0x080004), Rgb(0x3A0010), Clamp01(n * 1.2f - 0.2f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Sangue: anel neon vermelho (se expande na aura).</summary>
    static float[] RedRing() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float ring = MathF.Exp(-((r - 0.88f) / 0.025f) * ((r - 0.88f) / 0.025f));
        float glow = MathF.Exp(-MathF.Abs(r - 0.88f) / 0.06f) * 0.5f;
        var c = Mix(Rgb(0xFF1838), Rgb(0xFFB0BC), ring * 0.5f);
        return (c.r, c.g, c.b, Clamp01(Math.Max(ring, glow)));
    });

    /// <summary>Sangue: orbe de energia com um morcego dentro (o "enxame" vindo em direção à bola).</summary>
    static float[] BatOrb() => Paint(64, (x, y) =>
    {
        float r = Len(x, y);
        float glow = MathF.Pow(Clamp01(1f - r), 1.6f);
        float ax = MathF.Abs(x);
        float bat = PolyN(ax / 0.55f, y / 0.55f, new[] { 0.06f, 0.12f, 0.34f, 0.40f, 0.62f, 0.34f, 0.97f, 0.08f, 0.80f, -0.04f,
                                                         0.70f, 0.02f, 0.58f, -0.14f, 0.46f, -0.06f, 0.32f, -0.22f, 0.18f, -0.06f, 0.06f, -0.12f });
        bat = MathF.Min(bat, Len(x, y + 0.01f) / 0.55f - 0.12f);
        var c = Mix(Rgb(0xC8102A), Rgb(0xFF8A96), Clamp01(1f - r * 1.5f));
        if (bat < 0f) c = Rgb(0x12000A);
        return (c.r, c.g, c.b, bat < 0f ? 1f : glow);
    });

    /// <summary>Sangue: par de chifres de obsidiana com veios vermelhos, curvados para trás e para fora.</summary>
    static float[] Horns() => Paint(256, (x, y) =>
    {
        float ax = MathF.Abs(x);
        float horn = 9f, along = 0f;
        for (int i = 0; i < 48; i++)
        {
            float u = i / 47f, v = 1f - u;
            // Sai do alto da bola, sobe, curva para fora e para trás, ponta virada para cima.
            float hx = v * v * v * 0.30f + 3 * v * v * u * 0.52f + 3 * v * u * u * 0.86f + u * u * u * 0.74f;
            float hy = v * v * v * 0.22f + 3 * v * v * u * 0.50f + 3 * v * u * u * 0.52f + u * u * u * 0.92f;
            float dd = Len(ax - hx, y - hy) - (0.17f * (1f - u * 0.93f) + 0.006f);
            if (dd < horn) { horn = dd; along = u; }
        }
        float a = Cover(horn, 256);
        if (a <= 0f) return default;
        // Obsidiana: quase preta, reflexo frio no alto, veios vermelhos pulsando ao longo do chifre.
        var c = Mix(Rgb(0x1A0E1A), Rgb(0x5A4A5A), Clamp01(0.5f + (y - 0.3f) * 1.2f) * 0.7f);
        float rim = Clamp01(1f - MathF.Abs(horn + 0.01f) / 0.012f);
        c = Mix(c, Rgb(0xE8D8F0), rim * 0.6f);
        float vein = Clamp01(1f - MathF.Abs(MathF.Sin(along * 22f + ax * 9f)) / 0.25f) * Clamp01(-horn / 0.02f);
        c = Mix(c, Rgb(0xFF2A44), vein);
        // Anéis (sulcos) ao longo do chifre.
        float groove = Clamp01(1f - MathF.Abs(MathF.Sin(along * 30f)) / 0.15f) * 0.35f;
        c = Mix(c, Rgb(0x000000), groove);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Sangue: coroa de espinhos de sangue no alto da aura.</summary>
    static float[] Crown() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float d = 9f;
        for (int k = 0; k < 7; k++)
        {
            float a = MathF.PI / 2f + (k - 3) * 0.22f;
            float len = k == 3 ? 0.30f : k % 2 == 1 ? 0.18f : 0.24f;
            Rot(x, y, MathF.PI / 2f - a, out float px, out float py);
            d = MathF.Min(d, Poly(px, py, -0.055f, 0.58f, 0.055f, 0.58f, 0f, 0.58f + len));
        }
        // Faixa da coroa (arco) ligando os espinhos.
        float band = MathF.Max(MathF.Abs(r - 0.60f) - 0.035f, MathF.Abs(ang - MathF.PI / 2f) - 0.80f);
        d = MathF.Min(d, band);
        float a2 = Cover(d, S);
        if (a2 <= 0f) return default;
        var c = Mix(Rgb(0x4A0008), Rgb(0xFF3A50), Clamp01(0.3f + (r - 0.55f) * 2f));
        float rim = Clamp01(1f - MathF.Abs(d + 0.012f) / 0.015f);
        c = Mix(c, Rgb(0xFFB0B8), rim * 0.6f);
        return (c.r, c.g, c.b, a2);
    });

    /// <summary>Sangue: morcego.</summary>
    static float[] Bat() => Paint(64, (x, y) =>
    {
        float ax = MathF.Abs(x);
        float wing = PolyN(ax, y, new[] { 0.06f, 0.12f, 0.34f, 0.40f, 0.62f, 0.34f, 0.97f, 0.08f, 0.80f, -0.04f,
                                          0.70f, 0.02f, 0.58f, -0.14f, 0.46f, -0.06f, 0.32f, -0.22f, 0.18f, -0.06f, 0.06f, -0.12f });
        float body = Len(x / 0.6f, (y + 0.02f) / 1.0f) * 0.6f - 0.11f;
        float head = Len(x, y - 0.20f) - 0.10f;
        float ears = Poly(ax, y, 0.02f, 0.24f, 0.10f, 0.24f, 0.08f, 0.40f);
        float d = MathF.Min(MathF.Min(wing, body), MathF.Min(head, ears));
        float a = Cover(d, 64);
        if (a <= 0f) return default;
        var c = Rgb(0x14020A);
        float rim = Clamp01(1f - MathF.Abs(d + 0.025f) / 0.03f);
        c = Mix(c, Rgb(0xC81E32), rim * 0.8f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Corrompida: gomo do corpo da serpente.</summary>
    static float[] SnakeBody() => Paint(P, (x, y) =>
    {
        // Gomo de borda suave e cor contínua: sobrepostos, formam um corpo liso (sem "contas").
        float r = Len(x, y);
        float a = Clamp01((0.95f - r) / 0.25f);
        if (a <= 0f) return default;
        // Dorso escuro em cima, barriga clara embaixo (y da textura acompanha a curva do corpo).
        var c = Mix(Rgb(0x8A3ED0), Rgb(0x2A0848), Clamp01(0.5f + 0.6f * y));
        float scale = Clamp01(MathF.Abs(MathF.Sin(x * 7f + y * 4f)) - 0.6f) * 1.5f;
        c = Mix(c, Rgb(0x14022A), scale * 0.2f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Corrompida: ouroboros, serpente em anel mordendo a própria cauda.</summary>
    static float[] Ouroboros() => Paint(256, (x, y) =>
    {
        const float R = 0.80f;
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        const float headAng = 1.75f;                       // cabeça um pouco à esquerda do topo
        // s = quanto do corpo já percorremos a partir da cabeça, no sentido horário.
        float sAng = headAng - ang; while (sAng < 0f) sAng += MathF.Tau; while (sAng >= MathF.Tau) sAng -= MathF.Tau;
        float u = sAng / MathF.Tau;                         // 0 cabeça .. 1 ponta da cauda
        float width = 0.095f - 0.065f * u;
        float body = u > 0.035f && u < 0.985f ? MathF.Abs(r - R) - width : 9f;   // a cauda entra na boca
        // Cabeça: losango arredondado tangente ao anel, mordendo a cauda.
        float hx = MathF.Cos(headAng) * R, hy = MathF.Sin(headAng) * R;
        // Eixo da cabeça: aponta no sentido anti-horário (para a cauda); qy = para fora do anel.
        float tx = -MathF.Sin(headAng), ty = MathF.Cos(headAng);
        float qx = (x - hx) * tx + (y - hy) * ty, qy = (x - hx) * MathF.Cos(headAng) + (y - hy) * MathF.Sin(headAng);
        qx += 0.06f;   // recua a cabeça para a boca ficar sobre a ponta da cauda
        float head = SMin(Len(qx / 1.5f, qy) * 1.0f - 0.10f, Poly(qx, qy, 0.0f, -0.10f, 0.0f, 0.10f, 0.24f, 0f), 0.04f);
        float d = MathF.Min(body, head);
        float a = Cover(d, 256);
        if (a <= 0f) return default;
        (float r, float g, float b) c;
        if (head <= body)
        {
            c = Mix(Rgb(0x2A0848), Rgb(0xA050F0), Clamp01(0.5f + qy * 3f));
            float eye = Len(qx - 0.06f, qy - 0.045f) - 0.026f;
            if (eye < 0f) c = MathF.Abs(qx - 0.06f) < 0.007f ? (0.02f, 0f, 0.03f) : Rgb(0x9CFF4A);
        }
        else
        {
            // Escamas em losango, dorso escuro (fora) e barriga clara (dentro do anel).
            float across = (r - R) / Math.Max(width, 0.01f);           // -1 dentro .. 1 fora
            c = Mix(Rgb(0xC890FF), Rgb(0x3A0E66), Clamp01(0.5f + across * 0.6f));
            float sc = MathF.Abs(MathF.Sin(sAng * 46f + across * 1.5f)) * MathF.Abs(MathF.Sin(sAng * 46f - across * 1.5f));
            c = Mix(c, Rgb(0x1A0430), Clamp01(sc * 1.2f - 0.4f) * 0.6f);
            float hl = Clamp01(1f - MathF.Abs(across + 0.35f) / 0.25f) * 0.35f;
            c = Mix(c, (1f, 0.9f, 1f), hl);
        }
        float rim = Clamp01(1f - MathF.Abs(d + 0.006f) / 0.01f);
        c = Mix(c, Rgb(0x0E0218), rim * 0.85f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>
    /// Corrompida: um tendril de simbionte (base embaixo, ponta afiada em cima), escuro e brilhante
    /// como algo molhado, com luz roxa na borda e um filete de brilho ao longo do corpo.
    /// </summary>
    static float[] Tendril(float phase, float bend) => Paint(S, (x, y) =>
    {
        // Linha central curva: começa reta na base e serpenteia cada vez mais até a ponta.
        float u = Clamp01((y + 0.95f) / 1.9f);
        float cx = bend * 0.62f * MathF.Sin(u * 4.2f + phase) * MathF.Pow(u, 0.8f);
        float width = 0.40f * MathF.Pow(1f - u, 0.9f) + 0.012f;   // grosso na base, agulha na ponta
        float off = (x - cx) / Math.Max(width, 0.001f);             // -1..1 através do corpo
        float d = (MathF.Abs(x - cx) - width) * 0.5f;               // a peça é desenhada estreita: corrige a borda
        if (y < -0.95f || y > 0.97f) d = Math.Max(d, 0.02f);
        float a = Cover(d, S) * Clamp01((y + 0.98f) / 0.08f);       // base some suave atrás da bola
        if (a <= 0f) return default;
        // Corpo quase preto-arroxeado; luz roxa na borda de um lado; filete brilhante "molhado".
        var c = Rgb(0x12031F);
        c = Mix(c, Rgb(0x7A30C8), Clamp01((off - 0.35f) / 0.6f) * 0.9f);
        c = Mix(c, Rgb(0x3A0C66), Clamp01((-off - 0.6f) / 0.4f) * 0.6f);
        float streak = Clamp01(1f - MathF.Abs(off + 0.25f) / 0.12f) * (0.4f + 0.6f * MathF.Abs(MathF.Sin(u * 9f + phase)));
        c = Mix(c, Rgb(0xF0DCFF), streak * 0.75f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Corrompida: tentáculos de corrupção líquida saindo da bola, com gota na ponta.</summary>
    static float[] Tendrils(float seed) => Paint(S, (x, y) =>
    {
        float d = 9f, along = 0f;
        for (int k = 0; k < 7; k++)
        {
            float a0 = MathF.PI / 2f + (k - 3) * 0.55f;          // leque, sem a parte de baixo
            float wav = 0.35f + 0.15f * GemStyle.Hash(k * 7 + (int)seed);
            for (int i = 0; i < 48; i++)
            {
                float u = i / 47f;
                float rr = 0.42f + u * (0.42f + 0.10f * GemStyle.Hash(k * 3 + 1));
                float aa = a0 + MathF.Sin(u * 5f + seed + k) * wav * u;
                float px = MathF.Cos(aa) * rr, py = MathF.Sin(aa) * rr;
                float dd = Len(x - px, y - py) - (0.070f - 0.050f * u);
                if (dd < d) { d = dd; along = u; }
                if (i == 47) { float drop = Len(x - px, y - py) - 0.045f; if (drop < d) { d = drop; along = 1f; } }
            }
        }
        float a = Cover(d, S);
        if (a <= 0f) return default;
        var c = Mix(Rgb(0x2A0848), Rgb(0x8A3ED0), Clamp01(0.4f + along * 0.6f));
        float hl = Clamp01(1f - MathF.Abs(d + 0.022f) / 0.015f) * 0.6f;
        c = Mix(c, Rgb(0xE6C8FF), hl);
        return (c.r, c.g, c.b, a * 0.95f);
    });

    /// <summary>Corrompida: serpente erguida em "S" com cabeça de víbora (aponta para cima).</summary>
    static float[] SnakeRise() => Paint(S, (x, y) =>
    {
        // Curva de Bézier do corpo, afinando da base até o pescoço.
        float body = 9f, along = 0f;
        float px = x / 0.75f;   // a textura é desenhada um pouco mais estreita que alta
        for (int i = 0; i < 48; i++)
        {
            float u = i / 47f, v = 1f - u;
            float bx = 3 * v * v * u * 0.45f + 3 * v * u * u * -0.45f;
            float by = v * v * v * -0.95f + 3 * v * v * u * -0.35f + 3 * v * u * u * 0.20f + u * u * u * 0.52f;
            float d = Len(px - bx, y - by) - (0.22f - 0.09f * u);
            if (d < body) { body = d; along = u; }
        }
        // Cabeça de víbora no alto.
        float head = SMin(Len(px, (y - 0.64f) / 1.1f) - 0.22f, Poly(px, y, -0.17f, 0.70f, 0.17f, 0.70f, 0f, 0.97f), 0.07f);
        float d0 = MathF.Min(body, head);
        float a = Cover(d0 * 0.55f, S);
        if (a <= 0f) return default;
        (float r, float g, float b) c;
        if (head <= body)
        {
            c = Mix(Rgb(0x2A0848), Rgb(0xA050F0), Clamp01(0.4f + (y - 0.6f) * 2f));
            float eye = Len(MathF.Abs(px) - 0.10f, y - 0.70f) - 0.06f;
            if (eye < 0f) c = MathF.Abs(MathF.Abs(px) - 0.10f) < 0.015f ? (0.02f, 0f, 0.03f) : Rgb(0x9CFF4A);
        }
        else
        {
            // Dorso com losangos escuros e borda clara (barriga) de um lado.
            c = Mix(Rgb(0x3A0E66), Rgb(0x9A4AE0), Clamp01(0.5f - body * 4f));
            float diamond = MathF.Abs(MathF.Sin(along * 40f)) * Clamp01(-body / 0.05f);
            c = Mix(c, Rgb(0x16031F), Clamp01(diamond - 0.55f) * 1.6f);
        }
        float rim = Clamp01(1f - MathF.Abs(d0 + 0.012f) / 0.02f);
        c = Mix(c, Rgb(0x0E0218), rim * 0.8f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Corrompida: cabeça de víbora (aponta para cima na textura).</summary>
    static float[] SnakeHead() => Paint(64, (x, y) =>
    {
        float head = SMin(Len(x / 0.85f, y + 0.12f) * 0.85f - 0.42f, Poly(x, y, -0.36f, 0.05f, 0.36f, 0.05f, 0f, 0.86f), 0.15f);
        float a = Cover(head, 64);
        if (a <= 0f) return default;
        var c = Mix(Rgb(0x2A0848), Rgb(0xA050F0), Clamp01(0.5f + 0.5f * (-0.5f * x + 0.6f * y)));
        float eye = Len(MathF.Abs(x) - 0.22f, y - 0.18f) - 0.10f;
        if (eye < 0f)
        {
            c = Rgb(0x9CFF4A);
            if (MathF.Abs(MathF.Abs(x) - 0.22f) < 0.025f) c = (0.02f, 0f, 0.03f); // pupila em fenda
        }
        float rim = Clamp01(1f - MathF.Abs(head + 0.02f) / 0.025f);
        c = Mix(c, Rgb(0x16031F), rim * 0.8f);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Tempestade: anel mecânico com dentes de engrenagem e runas acesas.</summary>
    static float[] GearRing(int teeth, int runes) => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float tooth = MathF.Cos(ang * teeth) > 0.2f ? 1f : 0f;
        float outer = 0.90f + 0.06f * tooth;
        float d = MathF.Max(0.76f - r, r - outer);
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float l = Clamp01(0.5f + 0.5f * (-0.6f * x + 0.8f * y) / Math.Max(r, 0.01f));
        var c = Mix(Rgb(0x1E2A40), Rgb(0x8A9AB8), l);
        // Runas: tracinhos acesos dentro do anel.
        float step = MathF.Tau / runes;
        float k = MathF.Round(ang / step);
        float local = (ang - k * step) * r;
        int id = ((int)k % runes + runes) % runes;
        float len = 0.03f + 0.05f * GemStyle.Hash(id * 13 + teeth);
        float rune = MathF.Max(MathF.Abs(local) - 0.012f, MathF.Abs(r - 0.83f) - len);
        if (rune < 0f) c = Mix(Rgb(0x7FE0FF), (1f, 1f, 1f), 0.4f);
        float glow = Clamp01(1f - rune / 0.03f) * 0.5f;
        c = Mix(c, Rgb(0x5AC8FF), glow);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Caos: asas de demônio e chifres, feitas de chama roxa.</summary>
    static float[] DemonWings(float seed) => Paint(S, (x, y) =>
    {
        float ax = MathF.Abs(x);
        // Membrana de asa de dragão com borda de baixo recortada.
        float wing = PolyN(ax, y, new[] { 0.30f, 0.12f, 0.60f, 0.58f, 0.97f, 0.66f, 0.93f, 0.32f, 0.80f, 0.20f,
                                          0.79f, -0.04f, 0.64f, 0.04f, 0.58f, -0.20f, 0.45f, -0.06f, 0.33f, -0.10f });
        // Bordas de chama: só um pouco irregulares.
        float n = Fbm(x * 6f + seed, y * 5f - seed);
        wing += (n - 0.5f) * 0.05f;
        // Ossos da asa saindo do "pulso".
        float bones = MathF.Min(MathF.Min(Seg(ax, y, 0.60f, 0.58f, 0.93f, 0.32f), Seg(ax, y, 0.60f, 0.58f, 0.79f, -0.04f)),
                                MathF.Min(Seg(ax, y, 0.60f, 0.58f, 0.58f, -0.20f), Seg(ax, y, 0.30f, 0.12f, 0.60f, 0.58f)));
        // Chifres grossos curvando para fora e para cima (curva de Bézier afinando).
        float horn = 9f;
        for (int i = 0; i < 40; i++)
        {
            float u = i / 39f, v = 1f - u;
            float hx = v * v * 0.24f + 2 * v * u * 0.44f + u * u * 0.50f;
            float hy = v * v * 0.30f + 2 * v * u * 0.40f + u * u * 0.80f;
            horn = MathF.Min(horn, Len(ax - hx, y - hy) - 0.085f * (1f - 0.80f * u));
        }
        float d = MathF.Min(wing, horn);
        float a = Cover(d, S);
        if (a <= 0f) return default;
        (float r, float g, float b) c;
        if (horn <= wing)
        {
            c = Mix(Rgb(0x1A0430), Rgb(0x8A4AD0), Clamp01(0.3f + y));
            c = Mix(c, Rgb(0xE0B8FF), Clamp01(1f - MathF.Abs(horn + 0.012f) / 0.014f) * 0.7f);
        }
        else
        {
            float k = Clamp01(-wing / 0.18f);
            c = Mix(Rgb(0x5A1A9A), Rgb(0x2A0850), k * 0.6f);          // membrana mais escura por dentro
            c = Mix(c, Rgb(0xD890FF), Clamp01(1f - MathF.Abs(wing + 0.02f) / 0.03f) * (0.6f + 0.4f * n)); // borda em chama
            c = Mix(c, Rgb(0x18022E), Clamp01(1f - bones / 0.018f) * 0.8f);
        }
        return (c.r, c.g, c.b, a * 0.95f);
    });

    /// <summary>Gelo: véu espectral fluindo (a Mulher da Neve).</summary>
    static float[] Veil(float seed) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float hood = Clamp01(0.45f + 0.7f * y / Math.Max(r, 0.01f));
        float band = Clamp01((r - 0.46f) / 0.08f) * Clamp01(1f - (r - 0.66f) / 0.28f);
        float n = Fbm(x * 3.0f + seed, y * 1.0f + r * 2.0f - seed);
        float strands = Clamp01((n - 0.52f) * 5f);           // fiapos finos, não névoa
        float a = band * strands * (0.3f + 0.7f * hood) * 0.6f;
        var c = Mix(Rgb(0xB8E4F8), (1f, 1f, 1f), Clamp01((n - 0.5f) * 3f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Luz: rosácea de vitral de igreja.</summary>
    static float[] Rosette() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        if (r > 0.97f) return default;
        int ringId = r < 0.30f ? 0 : r < 0.62f ? 1 : 2;
        int sectors = ringId == 0 ? 6 : 12;
        float step = MathF.Tau / sectors;
        float sec = (ang + MathF.PI) / step;
        float edgeA = MathF.Abs(sec - MathF.Round(sec)) * step * r;  // distância à divisa radial
        float edgeR = MathF.Min(MathF.Abs(r - 0.30f), MathF.Min(MathF.Abs(r - 0.62f), MathF.Abs(r - 0.95f)));
        float lead = MathF.Min(edgeA, edgeR);
        int id = (int)MathF.Floor(sec) + ringId * 7;
        int[] pal = { 0xFFFFFF, 0xCFE6FF, 0xFFD9EC, 0xFFF1C8, 0xE2D4FF, 0xD4FFF0 };   // vitral prismático em tons pastel
        var c = Rgb(pal[((id % pal.Length) + pal.Length) % pal.Length]);
        float aa = 0.80f;
        if (lead < 0.022f) { c = Rgb(0xB89A58); aa = 0.95f; }
        return (c.r, c.g, c.b, aa * Cover(r - 0.95f, S));
    });

    /// <summary>Luz: prisma triangular; um feixe branco entra pela esquerda e sai em leque de arco-íris.</summary>
    static float[] PrismScene() => Paint(256, (x, y) =>
    {
        // Coordenadas da peça: x -1..1 (largura 2x a altura), y -1..1.
        float px = x * 2f;
        float tri = PolyN(px, y, new[] { -0.55f, -0.62f, 0.55f, -0.62f, 0f, 0.70f });
        (float r, float g, float b) c = (0, 0, 0); float a = 0f;
        // Feixe branco entrando (da esquerda até a face esquerda do prisma).
        float beamIn = Seg(px, y, -2.0f, -0.25f, -0.28f, 0.02f);
        float bi = MathF.Exp(-beamIn * beamIn / 0.0035f) * Clamp01((px + 2f) / 0.6f);
        if (bi > 0.01f) { c = (1f, 1f, 1f); a = bi; }
        // Leque de arco-íris saindo pela face direita.
        if (px > 0.25f)
        {
            float dx = px - 0.25f, dy = y - 0.0f;
            float ang = MathF.Atan2(dy, dx);
            float k = (ang + 0.05f) / 0.55f;                 // 0..1 através do leque (para baixo)
            if (k > -1f && k < 0f)
            {
                var rb = Hue(-k * 0.8f);
                float fade = Clamp01(1f - dx / 1.8f) * Clamp01(dx / 0.15f);
                float alpha = 0.85f * fade;
                c = Mix(c, rb, alpha / Math.Max(a + alpha, 0.001f)); a = Math.Max(a, alpha);
            }
        }
        // Prisma de cristal facetado por cima.
        float ta = Cover(tri * 0.5f, 256);
        if (ta > 0f)
        {
            float face = px < 0f ? 0.95f : 0.75f;
            var pc = Mix((0.80f, 0.86f, 0.95f), (1f, 1f, 1f), face * Clamp01(0.5f + y));
            float edge = Clamp01(1f - MathF.Abs(tri + 0.02f) / 0.03f);
            pc = Mix(pc, (0.55f, 0.60f, 0.75f), edge * 0.8f);
            float ridge = Clamp01(1f - MathF.Abs(px) / 0.02f) * Clamp01((0.7f - y) / 1.3f);
            pc = Mix(pc, (1f, 1f, 1f), ridge * 0.6f);
            c = Mix(c, pc, ta); a = Math.Max(a, ta * 0.95f);
        }
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Luz: mandala de linhas de luz (dois triângulos + círculos + pontos de brilho).</summary>
    static float[] Mandala() => Paint(256, (x, y) =>
    {
        float d = 9f;
        d = MathF.Min(d, MathF.Abs(Len(x, y) - 0.92f));
        d = MathF.Min(d, MathF.Abs(Len(x, y) - 0.62f));
        for (int k = 0; k < 6; k++)
        {
            float a1 = MathF.PI / 2f + k * MathF.PI / 3f, a2 = a1 + 2f * MathF.PI / 3f;
            d = MathF.Min(d, Seg(x, y, MathF.Cos(a1) * 0.92f, MathF.Sin(a1) * 0.92f, MathF.Cos(a2) * 0.92f, MathF.Sin(a2) * 0.92f));
            d = MathF.Min(d, MathF.Abs(Len(x - MathF.Cos(a1) * 0.92f, y - MathF.Sin(a1) * 0.92f) - 0.07f));
        }
        float line = Clamp01(1f - d / 0.008f);
        float glow = MathF.Exp(-d / 0.03f) * 0.55f;
        float dots = 0f;
        for (int k = 0; k < 6; k++)
        {
            float a1 = MathF.PI / 2f + k * MathF.PI / 3f + MathF.PI / 6f;
            dots = MathF.Max(dots, Clamp01(1f - Len(x - MathF.Cos(a1) * 0.46f, y - MathF.Sin(a1) * 0.46f) / 0.04f));
        }
        var c = Mix((1f, 0.95f, 0.82f), (1f, 1f, 1f), line);
        return (c.r, c.g, c.b, Clamp01(Math.Max(Math.Max(line, glow), dots)) * Clamp01((1f - Len(x, y)) / 0.04f));
    });

    static float[] LightOrb() => Paint(64, (x, y) =>
    {
        float r = Len(x, y);
        float core = Clamp01(1f - r / 0.35f);
        float a = Clamp01(MathF.Pow(Clamp01(1f - r), 2.2f) + core);
        var c = Mix((1f, 0.95f, 0.8f), (1f, 1f, 1f), core);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Rastro de cometa: branco no centro, bordas em arco-íris, sumindo para trás (para a esquerda).</summary>
    static float[] CometTail() => Paint(S, (x, y) =>
    {
        float along = Clamp01((x + 1f) / 2f);            // 0 atrás .. 1 junto da esfera
        float wid = 0.15f + 0.55f * along;
        float off = y / wid;
        if (MathF.Abs(off) > 1f) return default;
        var c = Mix(Hue(0.5f + off * 0.45f), (1f, 1f, 1f), 1f - MathF.Abs(off));
        float a = along * along * (1f - off * off);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Luz: coroa solar de raios afiados de comprimentos variados.</summary>
    static float[] Corona() => Paint(256, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        const float n = 28f;
        float sec = ang / MathF.Tau * n;
        int id = (int)MathF.Floor(sec);
        float f = sec - id - 0.5f;
        float len = 0.62f + 0.36f * GemStyle.Hash(id * 7 + 1);
        float width = 0.42f * Clamp01(1f - (r - 0.4f) / (len - 0.4f));     // afina até a ponta
        float spike = Clamp01(1f - MathF.Abs(f) / Math.Max(width, 0.001f)) * (r > 0.4f && r < len ? 1f : 0f);
        float core = Clamp01(1f - (r - 0.40f) / 0.22f) * Clamp01((r - 0.3f) / 0.1f);
        float a = Clamp01(spike * 0.95f + core * 0.7f);
        var c = Mix((1f, 0.9f, 0.65f), (1f, 1f, 1f), Clamp01(1f - (r - 0.4f) / 0.5f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Luz: auréola dourada (anel com brilho; achatada pelo tamanho do desenho).</summary>
    static float[] Aureola() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float ring = Clamp01(1f - MathF.Abs(r - 0.68f) / 0.15f);
        float glow = MathF.Exp(-MathF.Abs(r - 0.68f) / 0.16f) * 0.8f;
        // Mais claro na frente (embaixo), como metal polido iluminado; núcleo quase branco.
        float l = Clamp01(0.55f + 0.45f * (-y));
        var c = Mix(Rgb(0xE0A020), Rgb(0xFFF8D0), Clamp01(ring * 0.7f + l * 0.5f));
        float a = Clamp01(Math.Max(ring, glow));
        if (r > 0.98f) a = 0f;
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Luz: par de asas de penas de luz, abertas para cima e para fora.</summary>
    static float[] LightWings() => Paint(S, (x, y) =>
    {
        float ax = MathF.Abs(x);
        const float bx = 0.30f, by = 0.02f;
        float d = Len((ax - 0.40f) / 1.4f, y - 0.10f) * 1.4f - 0.13f; // cobertura junto ao ombro
        for (int k = 0; k < 7; k++)
        {
            float a = -0.30f + k * 0.24f;
            float len = 0.42f + 0.22f * MathF.Sin((k + 1) / 8f * MathF.PI);
            float ex = bx + MathF.Cos(a) * len, ey = by + MathF.Sin(a) * len;
            // Pena afinando até a ponta.
            float pax = ax - bx, pay = y - by, bax = ex - bx, bay = ey - by;
            float h = Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float w = 0.065f * (1f - 0.75f * h);
            d = MathF.Min(d, Len(pax - bax * h, pay - bay * h) - w);
        }
        float a2 = Cover(d, S);
        if (a2 <= 0f) return default;
        var c = Mix(Rgb(0xFFC860), (1f, 1f, 0.96f), Clamp01(-d / 0.04f));
        return (c.r, c.g, c.b, a2 * 0.92f);
    });

    static float Len3(float x, float y, float z) => MathF.Sqrt(x * x + y * y + z * z);

    /// <summary>Estilo da energia no buraco: 1 luz derretida, 2 brilho na borda, 3 vórtice.</summary>
    internal static int EnergyMode = 2;

    static float[] EnergyTex(Sd shape, int color, float seed) => Paint(S, (x, y) =>
    {
        float d = shape(x, y) - 0.025f;
        float a = Cover(d, S);
        if (a <= 0f) return default;
        var col = Rgb(color);
        var deep = (col.r * 0.45f, col.g * 0.45f, col.b * 0.45f);
        float depth = Clamp01(-d / 0.10f);                    // 0 na borda, 1 por dentro
        bool topEdge = shape(x, y - 0.07f) - 0.025f > 0f;    // borda de cima projeta sombra
        switch (EnergyMode)
        {
            case 2: // brilho na borda: fundo escuro tingido, luz vazando pela borda
            {
                float rim = MathF.Exp(-(-d) / 0.035f);
                var c = Mix(deep, col, rim);
                c = Mix(c, Mix(col, (1f, 1f, 1f), 0.25f), rim * rim * 0.6f);
                float alpha = 0.55f + 0.45f * rim;
                if (topEdge) alpha *= 0.8f;
                return (c.r, c.g, c.b, a * alpha);
            }
            case 3: // vórtice: espiral saturada
            {
                float r = Len(x, y), ang = MathF.Atan2(y, x);
                float sp = MathF.Sin(ang * 3f + r * 14f + seed) * 0.5f + 0.5f;
                float n = Fbm(x * 3f + seed, y * 3f);
                float k = Clamp01(sp * 0.8f + n * 0.4f);
                var c = Mix(deep, col, k);
                c = Mix(c, Mix(col, (1f, 1f, 1f), 0.2f), Clamp01((k - 0.8f) * 4f) * 0.5f);
                return (c.r, c.g, c.b, a * (topEdge ? 0.8f : 1f));
            }
            default: // 1: luz derretida, cor viva e uniforme, núcleo mais quente
            {
                float n = Fbm(x * 2.2f + seed, y * 2.2f - seed);
                float k = Clamp01(0.45f + 0.55f * depth + (n - 0.5f) * 0.35f);
                var c = Mix(deep, col, k);
                c = Mix(c, Mix(col, (1f, 1f, 1f), 0.3f), Clamp01((k - 0.85f) * 5f) * 0.5f);
                return (c.r, c.g, c.b, a * (topEdge ? 0.85f : 1f));
            }
        }
    });

    /// <summary>Rachaduras no fundo do buraco: saem da borda do encaixe em direção ao centro, brilhando na cor da joia.</summary>
    static float[] CracksTex(Sd shape, int color, int seed)
    {
        var rng = new Random(seed);
        var lines = new List<(float x, float y)[]>();
        const int count = 9;
        for (int i = 0; i < count; i++)
        {
            float ang = (i + (float)rng.NextDouble() * 0.6f) / count * MathF.Tau;
            float dx = MathF.Cos(ang), dy = MathF.Sin(ang);
            // Acha a borda do buraco nessa direção (do centro para fora).
            float r0 = 0.02f;
            while (r0 < 0.95f && shape(dx * r0, dy * r0) - 0.025f < 0f) r0 += 0.01f;
            if (r0 < 0.08f || r0 >= 0.93f) continue;
            // Começa um pouco para dentro da borda e anda em direção ao centro.
            float cx = dx * (r0 - 0.02f), cy = dy * (r0 - 0.02f);
            float a = ang + MathF.PI;
            float len = r0 * (0.45f + (float)rng.NextDouble() * 0.4f);
            var pts = new List<(float, float)> { (cx, cy) };
            const int steps = 6;
            for (int k = 0; k < steps; k++)
            {
                a += ((float)rng.NextDouble() - 0.5f) * 1.0f;
                float st = len / steps;
                cx += MathF.Cos(a) * st; cy += MathF.Sin(a) * st;
                pts.Add((cx, cy));
                if (k == 2 && rng.NextDouble() < 0.6)
                {
                    float ba = a + (rng.NextDouble() < 0.5 ? -0.9f : 0.9f);
                    lines.Add(new[] { (cx, cy), (cx + MathF.Cos(ba) * len * 0.3f, cy + MathF.Sin(ba) * len * 0.3f) });
                }
            }
            lines.Add(pts.ToArray());
        }
        return Paint(S, (x, y) =>
        {
            // Só dentro do buraco.
            float inside = shape(x, y) - 0.025f;
            if (inside > 0f) return default;
            float clip = Clamp01(-inside / 0.015f);
            float d = 9f;
            foreach (var pts in lines)
                for (int j = 0; j + 1 < pts.Length; j++)
                {
                    float sd = Seg(x, y, pts[j].x, pts[j].y, pts[j + 1].x, pts[j + 1].y) - 0.013f * (1f - j / (float)pts.Length * 0.6f);
                    d = MathF.Min(d, sd);
                }
            float core = Clamp01(0.5f - d * S * 0.5f);
            float glow = MathF.Exp(-Math.Max(d, 0f) / 0.035f) * 0.8f;
            var c = Mix(Rgb(color), (1f, 1f, 1f), core * 0.45f);
            return (c.r, c.g, c.b, Clamp01(Math.Max(core, glow)) * clip);
        });
    }

    static float[] HoleTex(Sd shape, int dark, int lipColor) => Paint(S, (x, y) =>
    {
        float d = shape(x, y) - 0.025f;
        float a = Cover(d, S);
        if (a <= 0f) return default;
        var c = Mix(Mix(Rgb(dark), (0f, 0f, 0f), 0.4f), Rgb(dark), Clamp01(-d / 0.12f));
        if (shape(x, y - 0.07f) - 0.025f > 0f) c = Mix(c, (0f, 0f, 0f), 0.7f);
        if (shape(x, y + 0.04f) - 0.025f > 0f)
            c = Mix(c, Rgb(lipColor), Clamp01(1f - MathF.Abs(d + 0.012f) / 0.018f) * 0.8f);
        return (c.r, c.g, c.b, a);
    });

    static float[] ShadowTex(Sd shape) => Paint(S, (x, y) =>
        (0f, 0f, 0f, Clamp01(1f - (shape(x, y) + 0.02f) / 0.14f) * 0.75f));

    static float[] Small(int size, Sd shape, int dark, int light, int edge) => GemTex(size, shape, dark, light, edge, null);

    // ───────────────────────── auras ─────────────────────────

    static float[] Halo(int inner, int outer) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float f = Clamp01(1f - (r - 0.30f) / 0.70f);
        var c = Mix(Rgb(outer), Rgb(inner), Clamp01(1f - (r - 0.3f) / 0.5f));
        return (c.r, c.g, c.b, f * f * (3f - 2f * f) * 0.85f);
    });

    /// <summary>Cor do arco-íris para h em 0..1 (vermelho → violeta).</summary>
    static (float r, float g, float b) Hue(float h)
    {
        h = (h - MathF.Floor(h)) * 6f;
        float x = 1f - MathF.Abs(h % 2f - 1f);
        return h < 1 ? (1, x, 0) : h < 2 ? (x, 1, 0) : h < 3 ? (0, 1, x) : h < 4 ? (0, x, 1) : h < 5 ? (x, 0, 1) : (1, 0, x);
    }

    /// <summary>Raios de luz branca que se abrem em arco-íris nas bordas (dispersão de prisma).</summary>
    static float[] PrismBeams() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        const float rays = 9f;
        float sec = ang / MathF.Tau * rays;
        float f = sec - MathF.Floor(sec);                      // 0..1 dentro de cada setor
        float w = 0.07f + 0.05f * GemStyle.Hash((int)MathF.Floor(sec) + 3);   // largura varia por raio
        float off = (f - 0.5f) / w;                             // posição através do raio
        float core = MathF.Exp(-off * off * 2.2f);
        float edge = MathF.Exp(-(MathF.Abs(off) - 1.3f) * (MathF.Abs(off) - 1.3f) * 3f) * 0.75f;
        float fall = Clamp01(1f - (r - 0.3f) / 0.7f) * Clamp01((r - 0.22f) / 0.12f);
        var rainbow = Hue(0.5f + off * 0.18f + r * 0.2f);
        var c = Mix(Mix((1f, 1f, 1f), rainbow, 0.85f), (1f, 1f, 1f), core);
        float a = Clamp01(core + edge) * fall * fall;
        return (c.r, c.g, c.b, a * 0.95f);
    });

    /// <summary>Anel prismático fino em volta (as cores giram pelo ângulo).</summary>
    static float[] PrismRing() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float ring = MathF.Exp(-((r - 0.78f) / 0.035f) * ((r - 0.78f) / 0.035f));
        var c = Mix(Hue(ang / MathF.Tau + 0.5f), (1f, 1f, 1f), 0.35f);
        return (c.r, c.g, c.b, ring * 0.7f);
    });

    static float[] Beams() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float b = MathF.Pow(MathF.Abs(MathF.Cos(ang * 6f)), 14f) + 0.5f * MathF.Pow(MathF.Abs(MathF.Cos(ang * 6f + MathF.PI / 2f)), 30f);
        float fall = Clamp01(1f - (r - 0.35f) / 0.65f) * Clamp01((r - 0.25f) / 0.15f);
        return (1f, 0.92f, 0.62f, Clamp01(b) * fall * fall * 0.9f);
    });

    /// <summary>Névoa de sangue em fiapos (gira na tela).</summary>
    static float[] Mist(float seed, int dark, int light) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float ring = Clamp01((r - 0.32f) / 0.18f) * Clamp01(1f - (r - 0.55f) / 0.45f);
        float ang = MathF.Atan2(y, x);
        // Fiapos alongados ao redor (ruído em coordenadas polares).
        float n = Fbm(MathF.Cos(ang) * 2.2f + seed, MathF.Sin(ang) * 2.2f + r * 3.5f - seed);
        float w = Clamp01((n - 0.42f) * 3f);
        var c = Mix(Rgb(dark), Rgb(light), Clamp01((n - 0.4f) * 2.5f));
        return (c.r, c.g, c.b, ring * w * 0.9f);
    });

    /// <summary>Anel de cristais de gelo e brilhos.</summary>
    static float[] Frost() => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float band = Clamp01((r - 0.44f) / 0.06f) * Clamp01(1f - (r - 0.50f) / 0.32f);
        float spikes = MathF.Pow(MathF.Abs(MathF.Cos(ang * 9f)), 24f) * band;
        float spikes2 = MathF.Pow(MathF.Abs(MathF.Cos(ang * 9f + MathF.PI / 2f)), 40f) * band * Clamp01(1f - (r - 0.48f) / 0.15f);
        float n = Noise(x * 14f + 3f, y * 14f + 7f);
        float sparkle = n > 0.86f ? Clamp01((n - 0.86f) / 0.06f) * Clamp01(1f - (r - 0.5f) / 0.45f) * Clamp01((r - 0.45f) / 0.05f) : 0f;
        float a = Clamp01(spikes + spikes2 * 0.8f + sparkle);
        return (0.88f, 0.97f, 1f, a);
    });

    /// <summary>Corrupção líquida: borda de bolha irregular e brilhante.</summary>
    static float[] Liquid(float seed, int dark, int light) => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float n = Fbm(MathF.Cos(ang) * 1.6f + seed, MathF.Sin(ang) * 1.6f - seed);
        float edge = 0.62f + (n - 0.5f) * 0.42f;
        float d = r - edge;
        float a = Clamp01(0.5f - d * S * 0.5f) * Clamp01((r - 0.3f) / 0.05f);
        if (a <= 0f) return default;
        var c = Mix(Rgb(dark), Rgb(light), Clamp01(1f + d / 0.12f) * 0.6f);
        // Reflexo na borda de cima, como líquido brilhante.
        float hl = Clamp01(1f - MathF.Abs(d + 0.035f) / 0.02f) * Clamp01(y / Math.Max(r, 0.001f) + 0.2f);
        c = Mix(c, (1f, 0.92f, 1f), hl * 0.7f);
        return (c.r, c.g, c.b, a * 0.92f);
    });

    /// <summary>Fogo roxo em volta, com labaredas mais altas em cima.</summary>
    static float[] Fire(float seed) => Paint(S, (x, y) =>
    {
        float r = Len(x, y), ang = MathF.Atan2(y, x);
        float up = Clamp01(y / Math.Max(r, 0.001f));
        // Labaredas: altura varia pelo ângulo (ruído), bem mais altas em cima.
        float tongue = Fbm(MathF.Cos(ang) * 3.4f + seed, MathF.Sin(ang) * 3.4f + seed * 1.7f);
        tongue = Clamp01((tongue - 0.30f) * 1.9f);
        float h = 0.05f + 0.50f * tongue * tongue * (0.30f + 0.70f * up * up);
        float edge = 0.46f + h;
        float k = Clamp01((edge - r) / Math.Max(h + 0.06f, 0.06f));
        if (k <= 0f) return default;
        float turb = Fbm(x * 6f + seed, y * 4f - seed);
        var c = Mix(Rgb(0x4A0E86), Rgb(0xB24CFF), Clamp01(k * 1.5f));
        c = Mix(c, Rgb(0xF6DEFF), Clamp01((k - 0.55f) * 2.4f) * (0.6f + 0.4f * turb));
        return (c.r, c.g, c.b, Clamp01(k * 3f) * 0.97f);
    });

    /// <summary>Arcos elétricos em zigue-zague perto da bola.</summary>
    static float[] Arcs(int seed)
    {
        var rng = new Random(seed);
        var lines = new List<(float x, float y)[]>();
        for (int i = 0; i < 2; i++)
        {
            float a0 = (float)(rng.NextDouble() * MathF.Tau), span = 0.9f + (float)rng.NextDouble() * 0.6f;
            var pts = new (float, float)[10];
            for (int j = 0; j < pts.Length; j++)
            {
                float a = a0 + span * j / (pts.Length - 1);
                float rr = 0.56f + (float)(rng.NextDouble() - 0.5) * 0.16f + (j == 0 || j == pts.Length - 1 ? -0.07f : 0f);
                pts[j] = (MathF.Cos(a) * rr, MathF.Sin(a) * rr);
            }
            lines.Add(pts);
        }
        return Paint(S, (x, y) =>
        {
            float d = 9f;
            foreach (var pts in lines)
                for (int j = 0; j + 1 < pts.Length; j++)
                    d = MathF.Min(d, Seg(x, y, pts[j].x, pts[j].y, pts[j + 1].x, pts[j + 1].y));
            float core = Clamp01(1f - d / 0.014f);
            float glow = MathF.Exp(-d / 0.035f) * 0.85f;
            var c = Mix(Rgb(0x3A8BFF), (1f, 1f, 1f), core);
            return (c.r, c.g, c.b, Clamp01(MathF.Max(core, glow)));
        });
    }

    /// <summary>Moldura de obsidiana em volta do olho (amêndoa vazada).</summary>
    static float EyeFrame(float x, float y) => MathF.Max(Eye(x, y), -(Eye(x / 0.80f, y / 0.80f) * 0.80f));

    /// <summary>Meia-altura da abertura do olho na coluna x (amêndoa interna).</summary>
    static float EyeH(float x)
    {
        float q = 0.7225f - Sq(x / 0.80f);
        return q <= 0f ? 0f : Math.Max(0f, 0.80f * (-0.45f + MathF.Sqrt(q)));
    }

    /// <summary>Fundo do olho: roxo escuro brilhante, mais claro no meio, com veiazinhas.</summary>
    static float[] ScleraTex() => Paint(S, (x, y) =>
    {
        float d = Eye(x / 0.82f, y / 0.82f) * 0.82f;
        float a = Cover(d, S);
        if (a <= 0f) return default;
        float h = Math.Max(EyeH(x), 0.001f);
        float k = Clamp01(1f - MathF.Abs(y) / h);
        var c = Mix(Rgb(0x14031E), Rgb(0x5A1E7A), k * k);
        float n = Fbm(x * 7f + 2f, y * 7f - 1f);
        c = Mix(c, Rgb(0xA0307A), Clamp01(1f - MathF.Abs(n - 0.5f) / 0.03f) * 0.5f * k);
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Íris verde tóxica em espiral (gira devagar), com aro escuro.</summary>
    static float[] IrisTex() => Paint(128, (x, y) =>
    {
        float r = Len(x, y);
        float a = Cover(r - 0.96f, 128);
        if (a <= 0f) return default;
        float ang = MathF.Atan2(y, x);
        float rays = 0.5f + 0.5f * MathF.Sin(ang * 22f + r * 9f + Fbm(x * 4f, y * 4f) * 6f);
        var c = Mix(Rgb(0x1E6A10), Rgb(0x9CF050), Clamp01(1f - r) * 0.7f + rays * 0.3f);
        c = Mix(c, Rgb(0xE8FFB0), Clamp01(1f - r / 0.35f) * 0.45f);
        c = Mix(c, Rgb(0x082A04), Clamp01((r - 0.80f) / 0.16f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Pupila em fenda (desenhada esticada na vertical).</summary>
    static float[] PupilTex() => Paint(64, (x, y) =>
    {
        float d = Len(x, y * 0.55f) - 0.55f;
        float a = Cover(d * 0.6f, 64);
        return a <= 0f ? default : (0.02f, 0f, 0.03f, a);
    });

    /// <summary>Pálpebras de escamas: c = 0 aberta .. 1 fechada.</summary>
    static float[] LidTex(float c) => Paint(S, (x, y) =>
    {
        float d = Eye(x / 0.84f, y / 0.84f) * 0.84f;
        if (d > 0.01f || c <= 0f) return default;
        float h = EyeH(x) * 1.05f + 0.01f;
        float top = h * (1f - c * 1.10f), bot = -h * (1f - c * 0.90f);
        float lidD = MathF.Min(top - y, y - bot);                    // < 0 coberto
        float a = Cover(MathF.Max(d, lidD), S);
        if (a <= 0f) return default;
        bool upper = y > (top + bot) * 0.5f;
        // Escamas: linhas em arco, roxo escuro, borda da pálpebra acesa de verde.
        float sx = x * 9f, sy = y * 12f + (MathF.Floor(sx) % 2f) * 0.5f;
        float scale = Clamp01(1f - MathF.Abs((sy - MathF.Floor(sy)) - 0.5f - 0.25f * MathF.Cos((sx - MathF.Floor(sx) - 0.5f) * 3f)) / 0.12f);
        float shade = Clamp01(1f - MathF.Abs(y - (upper ? top : bot)) / 0.25f);   // mais claro perto da borda
        var col = Mix(Rgb(0x4A1A70), Rgb(0x9A50D0), (upper ? 0.55f : 0.35f) + 0.3f * shade);
        col = Mix(col, Rgb(0x1A0428), scale * 0.75f);
        float edge = Clamp01(1f - (-lidD) / 0.03f);
        col = Mix(col, Rgb(0x9CFF5A), edge * 0.9f);
        return (col.r, col.g, col.b, a);
    });

    /// <summary>Olhinho amendoado verde (para a escuridão da Lendária).</summary>
    static float[] MiniEyeTex() => Paint(64, (x, y) =>
    {
        float d = Eye(x, y * 1.4f) / 1.4f;
        float glow = Clamp01(1f - Len(x, y * 2f) / 1.0f);
        float a = Cover(d * 0.6f, 64);
        var c = Mix(Rgb(0x8AFF4A), Rgb(0xF0FFC8), Clamp01(1f - Len(x, y) / 0.3f) * 0.7f);
        float pupil = Clamp01(1f - MathF.Abs(x) / 0.05f) * a;
        c = Mix(c, Rgb(0x040008), pupil);
        if (a <= 0f) c = Rgb(0x7CF04A);
        return (c.r, c.g, c.b, Math.Max(a, glow * glow * 0.55f));
    });

    /// <summary>Veias de corrupção na superfície da bola, mais densas na borda.</summary>
    static float[] CorruptVeins() => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        if (r > 0.92f) return default;
        float n1 = Fbm(x * 2.6f + 9.1f, y * 2.6f + 2.2f), n2 = Fbm(x * 4.8f - 3.3f, y * 4.8f + 6.1f);
        float v = MathF.Max(Clamp01(1f - MathF.Abs(n1 - 0.5f) / 0.03f), 0.75f * Clamp01(1f - MathF.Abs(n2 - 0.5f) / 0.025f));
        float rad = Clamp01((r - 0.25f) / 0.55f);
        float a = v * rad * Clamp01((0.92f - r) / 0.04f);
        if (a <= 0f) return default;
        var c = Mix(Rgb(0x6AE03A), Rgb(0xE070FF), Clamp01(n2 * 1.5f - 0.3f));
        return (c.r, c.g, c.b, a);
    });

    /// <summary>Miasma: fumaça venenosa roxa em volta, com fiapos esverdeados.</summary>
    static float[] Miasma(float seed, int dark, int light, int wisp = 0x6AE03A) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float n = Fbm(x * 2.4f + seed, y * 2.4f - seed * 0.7f);
        float body = Clamp01(1f - (r - 0.30f) / 0.70f) * Clamp01((r - 0.18f) / 0.15f);
        float wisps = Clamp01((n - 0.35f) * 2.2f);
        float a = body * (0.45f + 0.55f * wisps) * 0.9f;
        if (a <= 0f) return default;
        var c = Mix(Rgb(dark), Rgb(light), Clamp01(n * 1.3f - 0.25f));
        float g = Clamp01(1f - MathF.Abs(Fbm(x * 5f - seed, y * 5f) - 0.5f) / 0.04f) * 0.5f;
        c = Mix(c, Rgb(wisp), g * wisps);
        return (c.r, c.g, c.b, a);
    });

    static float[] Bubble() => Paint(P, (x, y) =>
    {
        float r = Len(x, y);
        float ring = Clamp01(1f - MathF.Abs(r - 0.72f) / 0.14f);
        float fill = r < 0.72f ? 0.25f : 0f;
        float hl = Clamp01(1f - Len(x + 0.3f, y - 0.3f) / 0.18f);
        var c = Mix(Rgb(0x6AE03A), (0.95f, 1f, 0.9f), Math.Max(hl, ring * 0.3f));
        return (c.r, c.g, c.b, Clamp01(Math.Max(Math.Max(ring, fill), hl)) * Cover(r - 0.9f, P));
    });

    static float[] Ember() => Paint(P, (x, y) =>
    {
        float r = Len(x, y);
        float a = MathF.Pow(Clamp01(1f - r), 2f);
        var c = Mix(Rgb(0xA040FF), Rgb(0xFFE8FF), Clamp01(1f - r * 1.6f));
        return (c.r, c.g, c.b, a);
    });

    // ───────────────────────── utilidades ─────────────────────────

    static float[] Paint(int size, Func<float, float, (float r, float g, float b, float a)> f)
    {
        var px = new float[size * size * 4];
        const int ss = 3;
        for (int j = 0; j < size; j++)
        for (int i = 0; i < size; i++)
        {
            float r = 0, g = 0, b = 0, a = 0;
            for (int sj = 0; sj < ss; sj++)
            for (int si = 0; si < ss; si++)
            {
                float x = ((i + (si + 0.5f) / ss) / size) * 2f - 1f;
                float y = ((j + (sj + 0.5f) / ss) / size) * 2f - 1f;
                var c = f(x, y);
                r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
            }
            int o = (j * size + i) * 4;
            px[o + 3] = a / (ss * ss);
            if (a > 0f) { px[o] = r / a; px[o + 1] = g / a; px[o + 2] = b / a; }
        }
        Bleed(px, size);
        return px;
    }

    // Espalha a cor das bordas nos pixels transparentes (evita contorno escuro nos mipmaps).
    static void Bleed(float[] px, int size)
    {
        for (int pass = 0; pass < 6; pass++)
        {
            var src = (float[])px.Clone();
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                int o = (j * size + i) * 4;
                if (src[o + 3] > 0.004f || src[o] + src[o + 1] + src[o + 2] > 0f) continue;
                float r = 0, g = 0, b = 0; int n = 0;
                for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    int x = i + di, y = j + dj;
                    if (x < 0 || y < 0 || x >= size || y >= size) continue;
                    int q = (y * size + x) * 4;
                    if (src[q + 3] <= 0.004f && src[q] + src[q + 1] + src[q + 2] <= 0f) continue;
                    r += src[q]; g += src[q + 1]; b += src[q + 2]; n++;
                }
                if (n > 0) { px[o] = r / n; px[o + 1] = g / n; px[o + 2] = b / n; }
            }
        }
    }

    static float Noise(float x, float y)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float H(int a, int b) => GemStyle.Hash(a * 73856093 ^ b * 19349663);
        float v0 = H(xi, yi) + (H(xi + 1, yi) - H(xi, yi)) * fx;
        float v1 = H(xi, yi + 1) + (H(xi + 1, yi + 1) - H(xi, yi + 1)) * fx;
        return v0 + (v1 - v0) * fy;
    }

    static float Fbm(float x, float y)
    {
        float s = 0, amp = 0.5f, norm = 0;
        for (int o = 0; o < 4; o++) { s += Noise(x, y) * amp; norm += amp; x *= 2.03f; y *= 2.03f; amp *= 0.5f; }
        return s / norm;
    }

    static void Rot(float x, float y, float a, out float px, out float py)
    {
        float c = MathF.Cos(a), s = MathF.Sin(a);
        px = x * c - y * s; py = x * s + y * c;
    }

    static float Seg(float px, float py, float ax, float ay, float bx, float by)
    {
        float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
        float h = Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
        return Len(pax - bax * h, pay - bay * h);
    }

    static float Box(float x, float y, float hx, float hy)
    {
        float dx = MathF.Abs(x) - hx, dy = MathF.Abs(y) - hy;
        return Len(MathF.Max(dx, 0f), MathF.Max(dy, 0f)) + MathF.Min(MathF.Max(dx, dy), 0f);
    }

    static float Poly(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        => PolyN(x, y, new[] { ax, ay, bx, by, cx, cy });

    // Distância com sinal a um polígono qualquer (Inigo Quilez).
    static float PolyN(float px, float py, float[] v)
    {
        int n = v.Length / 2;
        float d = Sq(px - v[0]) + Sq(py - v[1]);
        float s = 1f;
        for (int i = 0, j = n - 1; i < n; j = i, i++)
        {
            float ex = v[j * 2] - v[i * 2], ey = v[j * 2 + 1] - v[i * 2 + 1];
            float wx = px - v[i * 2], wy = py - v[i * 2 + 1];
            float h = Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
            d = MathF.Min(d, Sq(wx - ex * h) + Sq(wy - ey * h));
            bool c1 = py >= v[i * 2 + 1], c2 = py < v[j * 2 + 1], c3 = ex * wy > ey * wx;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * MathF.Sqrt(d);
    }

    static float SMin(float a, float b, float k)
    {
        float h = Clamp01(0.5f + 0.5f * (b - a) / k);
        return b + (a - b) * h - k * h * (1f - h);
    }

    static float Cover(float d, int size) => Clamp01(0.5f - d * size * 0.5f);
    static float Len(float x, float y) => MathF.Sqrt(x * x + y * y);
    static float Sq(float v) => v * v;
    static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    static (float r, float g, float b) Rgb(int hex) => (((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
    static (float r, float g, float b) Mix((float r, float g, float b) a, (float r, float g, float b) b, float t)
        => (a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t);
}
