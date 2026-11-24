using System;
using System.Collections.Generic;
using System.Numerics;

namespace CorrenteRapida;

/// <summary>
/// Motor 3D bem simples (por software) para o estilo 3D: monta pedras lapidadas a partir de um
/// contorno (mesa, coroa e pavilhão), gira e rasteriza com sombreamento por faceta.
/// Os quadros saem no mesmo formato das texturas de GemArt (RGBA float, linha 0 embaixo).
/// </summary>
internal static class Gem3D
{
    internal sealed class Mesh
    {
        public readonly List<Vector3> V = new();
        public readonly List<(int a, int b, int c, int kind)> F = new();   // kind: 0 faceta, 1 mesa
    }

    internal sealed class GemMat
    {
        public Vector3 Base;        // cor da pedra
        public Vector3 Deep;        // cor no fundo (mesa)
        public float Sparkle = 0.35f;
        public bool Edges = true;       // linhas claras entre as facetas (pedras); metal liso não tem
        public bool Metal;              // metal: reflexo de ambiente forte e colorido pela própria cor
        public bool Matte;              // fosco/orgânico (chifre, tecido, pele): difuso + brilho suave
        public Vector3 Alt;             // cor das faces kind 2 (ex.: forro da gola)
        public Vector3 Rim;             // contraluz (cor do elemento)
        public float Gloss = 0.3f;
        public float SpecPow = 24f;
        public bool Clear;              // gelo/vidro: mesa lisa e escura (transparente), sem o leque do brilhante     // brilho mais fino quanto maior (gosma molhada)
        public float Ambient = 0.18f;
        public bool Fire;               // diamante: reflexos com as cores do arco-íris (dispersão)
    }

    /// <summary>Toro (elo de corrente / aro): raio maior R, raio do tubo r, achatado em y por sy.</summary>
    internal static Mesh Torus(float R, float r, int seg = 28, int side = 10, float sx = 1f)
    {
        var m = new Mesh();
        for (int i = 0; i < seg; i++)
        {
            float u = i / (float)seg * MathF.Tau;
            for (int j = 0; j < side; j++)
            {
                float v = j / (float)side * MathF.Tau;
                float rr = R + r * MathF.Cos(v);
                m.V.Add(new Vector3(rr * MathF.Cos(u) * sx, rr * MathF.Sin(u), r * MathF.Sin(v)));
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < side; j++)
            {
                int a = i * side + j, b = ((i + 1) % seg) * side + j, c = ((i + 1) % seg) * side + (j + 1) % side, d = i * side + (j + 1) % side;
                m.F.Add((a, b, c, 0)); m.F.Add((a, c, d, 0));
            }
        return m;
    }

    /// <summary>
    /// Esfera de hematita polida: cinza-metálico quase preto, espelhada, refletindo um "estúdio"
    /// (janela de luz em cima à esquerda, faixa de horizonte, chão escuro), com tom avermelhado
    /// nos reflexos escuros (como a hematita) e contraluz na cor do elemento.
    /// </summary>
    internal static float[] HematiteSphere(int n, float radius, Vector3 rimCol)
    {
        var px = new float[n * n * 4];
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        var Hh = Vector3.Normalize(L + Vector3.UnitZ);
        var metal = new Vector3(0.62f, 0.62f, 0.66f);
        var redTint = new Vector3(0.30f, 0.10f, 0.10f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = ((x + 0.5f) / n * 2f - 1f) / radius, v = ((y + 0.5f) / n * 2f - 1f) / radius;
                float q = u * u + v * v;
                // Antisserrilhado da borda.
                float edge = Math.Clamp((1f - MathF.Sqrt(q)) * n * radius * 0.5f + 0.5f, 0f, 1f);
                if (edge <= 0f) continue;
                q = MathF.Min(q, 0.9999f);
                var N = new Vector3(u, v, MathF.Sqrt(1f - q));
                var R = Vector3.UnitZ * -1f - 2f * Vector3.Dot(-Vector3.UnitZ, N) * N;   // reflexo da visão
                // Ambiente: céu claro em cima, janela de luz forte em cima à esquerda, horizonte, chão escuro.
                float sky = Math.Clamp(R.Y * 0.8f + 0.2f, 0f, 1f);
                float win = MathF.Pow(Math.Clamp(Vector3.Dot(R, Vector3.Normalize(new Vector3(-0.55f, 0.6f, 0.58f))), 0f, 1f), 7f);
                float horizon = MathF.Exp(-MathF.Pow((R.Y + 0.05f) / 0.12f, 2f)) * 0.55f;
                float ground = Math.Clamp(-R.Y, 0f, 1f);
                var env = metal * (0.12f + 0.75f * sky + horizon) + Vector3.One * (win * 0.9f) + redTint * (ground * 0.9f);
                float fres = MathF.Pow(1f - N.Z, 4f);
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, Hh)), 200f) * 0.8f;
                float back = MathF.Max(0f, -N.X * 0.35f - N.Y * 0.75f + 0.15f) * MathF.Pow(1f - N.Z, 2f);
                var c = env * (0.55f + 0.45f * fres) + new Vector3(spec) + rimCol * (back * 0.9f);
                int o = (y * n + x) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = edge;
            }
        return px;
    }

    // Ruído 3D simples (valor) para a superfície da pedra.
    static float H3(int x, int y, int z) { uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177); h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) / (float)uint.MaxValue; }
    internal static float Noise3(Vector3 p)
    {
        int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y), z = (int)MathF.Floor(p.Z);
        float fx = p.X - x, fy = p.Y - y, fz = p.Z - z;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
        float L(float a, float b, float t) => a + (b - a) * t;
        return L(L(L(H3(x, y, z), H3(x + 1, y, z), fx), L(H3(x, y + 1, z), H3(x + 1, y + 1, z), fx), fy),
                 L(L(H3(x, y, z + 1), H3(x + 1, y, z + 1), fx), L(H3(x, y + 1, z + 1), H3(x + 1, y + 1, z + 1), fx), fy), fz);
    }
    static float Fbm3(Vector3 p) { float s = 0f, a = 0.5f; for (int i = 0; i < 4; i++) { s += a * Noise3(p); p *= 2.03f; a *= 0.5f; } return s; }

    /// <summary>
    /// Pedra mineral bruta: rocha arredondada com lascas (facetas irregulares de quebra), superfície
    /// áspera, cinza-chumbo fosco com manchas avermelhadas de ferrugem (hematita) e pontos metálicos
    /// que cintilam. Contorno levemente irregular.
    /// </summary>
    internal static float[] MineralStone(int n, float radius, Vector3 rimCol, int seed = 7, Vector3? stoneA = null, Vector3? stoneB = null, float sheen = 0f, Vector3? sheenCol = null, float fleckAmt = 1.1f, float ambient = 0.22f)
    {
        var px = new float[n * n * 4];
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        var rnd = new Random(seed);
        // Lascas: direções na esfera; cada lasca é um plano de quebra (achata a superfície naquela região).
        var chips = new Vector3[22];
        var chipK = new float[22];
        for (int i = 0; i < chips.Length; i++)
            { chips[i] = Vector3.Normalize(new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 1.4f - 0.2f)); chipK[i] = 0.25f + 0.5f * (float)rnd.NextDouble(); }
        var off = new Vector3(seed * 3.1f, seed * 1.7f, seed * 5.3f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float ang = MathF.Atan2(v, u);
                float rr = radius * (1f + 0.035f * (Noise3(new Vector3(MathF.Cos(ang) * 2.5f, MathF.Sin(ang) * 2.5f, 0.5f) + off) - 0.5f) * 2f);
                float d = MathF.Sqrt(u * u + v * v);
                float edge = Math.Clamp((rr - d) * n * 0.5f + 0.5f, 0f, 1f);
                if (edge <= 0f) continue;
                float uu = u / rr, vv = v / rr, q = MathF.Min(uu * uu + vv * vv, 0.999f);
                var P = new Vector3(uu, vv, MathF.Sqrt(1f - q));
                // Normal da lasca mais próxima (facetas de quebra) misturada com a esfera.
                int best = 0; float bd = -2f, bd2 = -2f;
                for (int i = 0; i < chips.Length; i++) { float dd = Vector3.Dot(P, chips[i]); if (dd > bd) { bd2 = bd; bd = dd; best = i; } else if (dd > bd2) bd2 = dd; }
                var Nc = Vector3.Normalize(Vector3.Lerp(P, chips[best], chipK[best]));
                float ridge = Math.Clamp(1f - (bd - bd2) / 0.015f, 0f, 1f) * (0.3f + 0.7f * Noise3(P * 9f));              // aresta entre lascas
                // Aspereza: perturba a normal com o gradiente do ruído.
                const float e = 0.02f;
                var sp = P * 6f + off;
                float h0 = Fbm3(sp);
                var g = new Vector3(Fbm3(sp + new Vector3(e, 0, 0)) - h0, Fbm3(sp + new Vector3(0, e, 0)) - h0, Fbm3(sp + new Vector3(0, 0, e)) - h0) / e;
                var N = Vector3.Normalize(Nc - g * 0.11f);
                float ndl = MathF.Max(0f, Vector3.Dot(N, L));
                // Cor: chumbo fosco, ferrugem avermelhada em manchas, cavidades mais escuras.
                float rust = Math.Clamp((Fbm3(P * 2.2f + off * 0.7f) - 0.45f) * 3f, 0f, 1f);
                var baseC = Vector3.Lerp(stoneA ?? new Vector3(0.29f, 0.28f, 0.29f), stoneB ?? new Vector3(0.46f, 0.17f, 0.11f), rust * 0.85f);
                float cav = Math.Clamp((h0 - 0.35f) * 2.2f, 0f, 1f);
                baseC *= 0.65f + 0.35f * cav;
                // Pontos metálicos (cristais de hematita) que pegam a luz.
                float fleck = Noise3(P * 38f + off) > 0.86f ? 1f : 0f;
                var Hh = Vector3.Normalize(L + Vector3.UnitZ);
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, Hh)), 30f) * (0.12f + fleckAmt * fleck);
                // Adulária (pedra-da-lua): brilho azulado flutuante que aparece em certas direções.
                float sch = sheen * MathF.Pow(Math.Clamp(Vector3.Dot(N, Vector3.Normalize(new Vector3(0.3f, 0.5f, 0.8f))), 0f, 1f), 6f) * (0.5f + 0.5f * Fbm3(P * 3f + off));
                float back = MathF.Max(0f, -N.X * 0.35f - N.Y * 0.75f + 0.15f) * MathF.Pow(1f - N.Z, 2f);
                var c = baseC * (ambient + 1.05f * ndl) + new Vector3(0.85f, 0.85f, 0.9f) * spec + new Vector3(0.06f) * ridge * ndl + rimCol * (back * 0.7f) + (sheenCol ?? new Vector3(0.55f, 0.75f, 1f)) * sch;
                int o = (y * n + x) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = edge;
            }
        return px;
    }

    /// <summary>
    /// Gosma viva: esfera de líquido preto e viscoso com fibras em relevo que escorrem e se torcem.
    /// phase 0..1 fecha um ciclo (os quadros emendam). Brilho molhado, borda com reflexo colorido.
    /// </summary>
    static float Sq(float x) => x * x;
    static float Sm(float a, float b, float x) { float t = Math.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

    internal static float[] GooBall(int n, float radius, float phase, Vector3 rimCol, Vector3 sheenCol, int seed = 11, Func<float, float, float> clip = null, float[] glowOut = null, Vector3? glowPos = null, Vector3? glowCol = null,
        Func<float, float, float> hole = null, Func<float, float, float> outer = null)
    {
        var px = new float[n * n * 4];
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        var off = new Vector3(seed * 3.1f, seed * 1.7f, seed * 5.3f);
        // Escoamento em duas fases (como mapa de fluxo): duas cópias do relevo deslizando na direção da
        // correnteza, defasadas meio ciclo e trocando de peso; o ciclo emenda sem salto e o líquido sempre "desce".
        float ph1 = phase, ph2 = phase + 0.5f; ph2 -= MathF.Floor(ph2);
        float w1 = 1f - MathF.Abs(2f * ph1 - 1f);
        float w2 = 1f - w1;
        const float FlowAmp = 0.9f;
        float Rel(Vector3 p)
        {
            var q = p * 1.0f + off;
            var w = new Vector3(Fbm3(q + new Vector3(4.1f, 0, 0)), Fbm3(q + new Vector3(0, 7.3f, 0)), Fbm3(q + new Vector3(0, 0, 2.9f))) - new Vector3(0.5f);
            var r = new Vector3(p.X * 2.6f, p.Y * 1.3f, p.Z * 2.6f) + w * 1.8f + off * 0.5f;
            float a = 1f - MathF.Abs(2f * Fbm3(r) - 1f);
            float bb = Fbm3(p * 1.4f + off * 0.3f);
            return MathF.Pow(a, 4f) * 0.7f + bb * 0.6f;
        }
        Vector3 Flow(Vector3 P)
        {
            // Correnteza: escorre para baixo pela superfície, girando um pouco em volta do eixo vertical.
            var down = new Vector3(0, -1, 0); down -= P * Vector3.Dot(down, P);
            var swirl = Vector3.Cross(new Vector3(0, 1, 0), P) * 0.45f;
            return down + swirl;
        }
        float H(Vector3 P)
        {
            var f = Flow(P);
            return Rel(P - f * (ph1 - 0.5f) * FlowAmp) * w1 + Rel(P - f * (ph2 - 0.5f) * FlowAmp + new Vector3(3.7f, 1.9f, 5.1f)) * w2;
        }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                Vector3 P; float edge, ao = 1f;
                if (clip != null)
                {
                    // Dentro de uma abertura (o olho): esfera maior vista só pela abertura.
                    float cd = clip(u, v);
                    edge = Math.Clamp(0.5f - cd * n * 0.5f, 0f, 1f);
                    if (edge <= 0f) continue;
                    float z = MathF.Sqrt(MathF.Max(0.02f, radius * radius - u * u - v * v));
                    P = new Vector3(u, v, z) / radius;
                    ao = Math.Clamp(-cd / 0.16f, 0f, 1f);
                }
                else
                {
                    float d = MathF.Sqrt(u * u + v * v);
                    edge = Math.Clamp((radius - d) * n * 0.5f + 0.5f, 0f, 1f);
                    if (edge <= 0f) continue;
                    float uu = u / radius, vv = v / radius, q = MathF.Min(uu * uu + vv * vv, 0.999f);
                    P = new Vector3(uu, vv, MathF.Sqrt(1f - q));
                }
                float lip = 0f; Vector3 lipDir = Vector3.Zero;
                if (hole != null)
                {
                    // Membrana de gosma sobre o encaixe, com um furo que abre; a borda do furo é um lábio grosso.
                    float od = outer(u, v);
                    if (od > 0.02f) continue;
                    float hd = hole(u, v);
                    if (hd < -0.02f) continue;
                    edge = MathF.Min(edge, MathF.Min(Math.Clamp(0.5f - od * n * 0.5f, 0f, 1f), Math.Clamp(0.5f + hd * n * 0.5f, 0f, 1f)));
                    if (edge <= 0f) continue;
                    const float he = 0.01f;
                    var hg = new Vector2(hole(u + he, v) - hd, hole(u, v + he) - hd) / he;
                    lip = MathF.Exp(-MathF.Max(0f, hd) / 0.05f);
                    lipDir = new Vector3(-hg.X, -hg.Y, 0f);   // borda virando para dentro do furo
                    ao = Math.Clamp(-od / 0.06f, 0.4f, 1f);
                }
                const float e = 0.025f;
                float h0 = H(P);
                var g = new Vector3(H(P + new Vector3(e, 0, 0)) - h0, H(P + new Vector3(0, e, 0)) - h0, H(P + new Vector3(0, 0, e)) - h0) / e;
                g -= P * Vector3.Dot(g, P);
                var N = Vector3.Normalize(P - g * 0.10f + lipDir * lip * 0.9f);
                float ndl = MathF.Max(0f, Vector3.Dot(N, L));
                // Reflexo de ambiente (caixas de luz): os brilhos aparecem em todas as cordas, não num ponto só.
                var R = Vector3.Reflect(-Vector3.UnitZ, N);
                float sb = Sm(0.45f, 0.62f, R.Y) * (1f - Sm(0.80f, 0.92f, R.Y)) * Sm(-0.85f, -0.65f, R.X) * (1f - Sm(-0.25f, -0.05f, R.X));
                float side = Sm(0.55f, 0.80f, -R.X) * (1f - Sm(0.1f, 0.4f, MathF.Abs(R.Y))) * 0.35f;
                float grn = Sm(0.45f, 0.75f, R.X) * Sm(-0.6f, -0.2f, R.Y) * (1f - Sm(-0.1f, 0.2f, R.Y));
                float low = Sm(0.25f, 0.70f, -R.Y);
                float fres = 0.10f + 0.90f * MathF.Pow(1f - MathF.Max(0f, N.Z), 4f);
                var env = new Vector3(0.95f, 0.92f, 1f) * (sb * 1.3f + side) + sheenCol * grn * 0.55f + rimCol * (low * 0.55f + 0.10f);
                var baseC = new Vector3(0.016f, 0.011f, 0.024f) * (0.6f + 0.8f * ndl);
                var Hh = Vector3.Normalize(L + Vector3.UnitZ);
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, Hh)), 90f) * 1.2f;
                var c = baseC + env * MathF.Max(fres, 0.32f * Math.Clamp(sb + side + grn, 0f, 1f)) + new Vector3(1f) * spec;
                c *= 0.30f + 0.70f * ao;
                if (glowOut != null)
                {
                    // Luz verde do olho batendo na gosma: difuso suave + reflexo molhado, só a parte de cima/frente.
                    var S3 = new Vector3(u, v, P.Z * radius);
                    float lum = 0f, gsMax = 0f;
                    // Duas fontes: o olho flutuando em cima e a poça acesa no encaixe.
                    for (int li = 0; li < 2; li++)
                    {
                        var lp = li == 0 ? glowPos.Value : new Vector3(0f, -0.05f, 1.25f);
                        float lk = li == 0 ? 1f : 0.45f;
                        var Ld = lp - S3; float dist = Ld.Length(); Ld /= dist;
                        float fall = 1f / (1f + dist * dist * 0.8f);
                        float gd = MathF.Max(0f, Vector3.Dot(N, Ld));
                        float gsp = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, Vector3.Normalize(Ld + Vector3.UnitZ))), 40f);
                        lum += (gd * 1.4f + gsp * 3.0f) * fall * lk; gsMax = MathF.Max(gsMax, gsp * fall);
                    }
                    lum = Math.Clamp(lum, 0f, 1f);
                    int og = (y * n + x) * 4;
                    var gc = Vector3.Lerp(glowCol.Value, Vector3.One, Math.Clamp(gsMax - 0.4f, 0f, 1f) * 0.6f);
                    glowOut[og] = gc.X; glowOut[og + 1] = gc.Y; glowOut[og + 2] = gc.Z; glowOut[og + 3] = lum * edge;
                }
                int o = (y * n + x) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = edge;
            }
        return px;
    }

    /// <summary>Elo oval de corrente (estádio): meia-volta em cima e embaixo ligadas por trechos retos (eixo maior em x).</summary>
    internal static Mesh Link(float halfLen, float R, float r, int seg = 40, int side = 10)
    {
        var m = new Mesh();
        // Caminho central do elo: estádio no plano xy.
        var path = new List<(Vector2 p, Vector2 t)>();
        float straight = 2f * halfLen, per = straight * 2f + MathF.Tau * R;
        for (int i = 0; i < seg; i++)
        {
            float d = i / (float)seg * per;
            Vector2 p, t;
            if (d < straight) { p = new Vector2(-halfLen + d, -R); t = Vector2.UnitX; }
            else if (d < straight + MathF.PI * R) { float a = -MathF.PI / 2f + (d - straight) / R; p = new Vector2(halfLen + R * MathF.Cos(a), R * MathF.Sin(a)); t = new Vector2(-MathF.Sin(a), MathF.Cos(a)); }
            else if (d < 2f * straight + MathF.PI * R) { float dd = d - straight - MathF.PI * R; p = new Vector2(halfLen - dd, R); t = -Vector2.UnitX; }
            else { float a = MathF.PI / 2f + (d - 2f * straight - MathF.PI * R) / R; p = new Vector2(-halfLen + R * MathF.Cos(a), R * MathF.Sin(a)); t = new Vector2(-MathF.Sin(a), MathF.Cos(a)); }
            path.Add((p, t));
        }
        foreach (var (p, t) in path)
        {
            var nOut = new Vector2(t.Y, -t.X);                       // normal no plano, para fora
            for (int j = 0; j < side; j++)
            {
                float v = j / (float)side * MathF.Tau;
                var q = p + nOut * (r * MathF.Cos(v));
                m.V.Add(new Vector3(q, r * MathF.Sin(v)));
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < side; j++)
            {
                int a0 = i * side + j, b0 = ((i + 1) % seg) * side + j, c0 = ((i + 1) % seg) * side + (j + 1) % side, d0 = i * side + (j + 1) % side;
                m.F.Add((a0, b0, c0, 0)); m.F.Add((a0, c0, d0, 0));
            }
        return m;
    }

    /// <summary>Olhal chumbado na pedra: placa de ferro com rebites e uma argola em pé.</summary>
    internal static Mesh EyeBolt()
    {
        var m = Torus(0.42f, 0.13f, 28, 10);                         // argola (de frente)
        // Placa: disco achatado atrás da argola.
        int baseIdx = m.V.Count; const int k = 20;
        m.V.Add(new Vector3(0f, -0.55f, -0.08f));
        for (int i = 0; i < k; i++) { float a = i / (float)k * MathF.Tau; m.V.Add(new Vector3(MathF.Cos(a) * 0.62f, -0.55f + MathF.Sin(a) * 0.32f, -0.20f)); }
        for (int i = 0; i < k; i++) m.F.Add((baseIdx, baseIdx + 1 + i, baseIdx + 1 + (i + 1) % k, 0));
        return m;
    }

    /// <summary>
    /// Encaixe esculpido na pedra: cavidade no formato da joia, com paredes chanfradas e lascadas,
    /// fundo áspero, sombra da borda de cima projetada no fundo e uma borda clara onde a luz bate.
    /// sd: forma da joia (já no tamanho do encaixe). Só a cavidade tem cor (o resto é transparente).
    /// </summary>
    /// <summary>
    /// Entalhe de verdade na própria pedra: a superfície da bola afunda "depth" dentro do contorno, com paredes
    /// inclinadas iluminadas pela luz, sombra projetada da parede no fundo e oclusão nos cantos. A cor vem da
    /// textura da bola (o buraco é da mesma pedra), escurecida pela profundidade.
    /// </summary>
    /// <summary>
    /// Entalhe que segue a curva da esfera (raio R): o fundo afunda ao longo da normal da bola, então perto das
    /// bordas a parede de fora aparece e a de dentro some, e o fundo encolhe em direção ao centro, como num
    /// buraco de verdade numa bola. Feito por "raio" saindo da tela até bater na superfície entalhada.
    /// </summary>
    internal static float[] CarvedSphere(float[] ball, Func<float, float, float> sd, int n, float R, float depth, float wall, int ballN, out float[] floorMask)
    {
        var px = new float[n * n * 4];
        floorMask = new float[n * n];
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        float Dp(Vector3 dir) => depth * Sm(wall * 0.5f, -wall * 0.5f, sd(dir.X * R, dir.Y * R));
        float F(Vector3 p) { float r = p.Length(); return r - (R - Dp(p / r)); }   // < 0: dentro da pedra
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n * 2f - 1f, v = (j + 0.5f) / n * 2f - 1f;
                float q = u * u + v * v;
                if (q >= R * R) continue;
                float z0 = MathF.Sqrt(R * R - q);
                // Só precisa marchar onde a esfera de fora está sobre o entalhe (ou perto dele).
                var d0 = new Vector3(u, v, z0) / R;
                bool near = false;
                for (float k = 0f; k <= depth * 1.5f; k += depth * 0.25f)
                { var dd = Vector3.Normalize(new Vector3(u, v, z0 - k)); if (sd(dd.X * R, dd.Y * R) < wall) { near = true; break; } }
                if (!near) continue;
                float z = z0, hitZ = float.NaN;
                float zEnd = MathF.Sqrt(MathF.Max(0f, (R - depth) * (R - depth) - q)) - 0.02f;
                const float step = 0.0035f;
                for (; z > zEnd - 0.2f; z -= step)
                    if (F(new Vector3(u, v, z)) <= 0f) { hitZ = z; break; }
                if (float.IsNaN(hitZ)) continue;
                // Refina o ponto de batida.
                float a = hitZ + step, b = hitZ;
                for (int it = 0; it < 6; it++) { float m = (a + b) * 0.5f; if (F(new Vector3(u, v, m)) <= 0f) b = m; else a = m; }
                var P = new Vector3(u, v, b);
                float rr = P.Length(); var dir = P / rr;
                float dep = Dp(dir);
                if (dep < 0.0015f) continue;   // ainda na superfície de fora: a bola mostra por baixo
                const float e = 0.004f;
                var N = Vector3.Normalize(new Vector3(F(P + new Vector3(e, 0, 0)) - F(P - new Vector3(e, 0, 0)), F(P + new Vector3(0, e, 0)) - F(P - new Vector3(0, e, 0)), F(P + new Vector3(0, 0, e)) - F(P - new Vector3(0, 0, e))));
                float diff = MathF.Max(0f, Vector3.Dot(N, L));
                // Sombra: do ponto, anda em direção à luz; se entrar na pedra antes de sair do buraco, está na sombra.
                float sh = 1f;
                for (int k = 1; k <= 10; k++)
                {
                    var sp = P + L * (k * depth * 0.22f);
                    if (F(sp) < -0.002f) { sh = 0.60f + 0.04f * k; break; }
                }
                float rim = sd(dir.X * R, dir.Y * R);
                float ao = 0.75f + 0.25f * Math.Clamp(-rim / 0.10f, 0f, 1f);
                float lit = (0.45f + 0.95f * diff) * ao * sh * (1f - 0.15f * dep / depth);
                int bx = Math.Clamp((int)((dir.X * R + 1f) * 0.5f * ballN), 0, ballN - 1), by = Math.Clamp((int)((dir.Y * R + 1f) * 0.5f * ballN), 0, ballN - 1);
                int bi = (by * ballN + bx) * 4;
                var c = new Vector3(ball[bi], ball[bi + 1], ball[bi + 2]);
                float baseLit = 0.35f + 1.05f * MathF.Max(0f, Vector3.Dot(dir, L));
                c = c / MathF.Max(0.25f, baseLit) * lit;
                int o = (j * n + i) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = Math.Clamp(dep / 0.004f, 0f, 1f);
                floorMask[j * n + i] = Math.Clamp((dep - depth * 0.85f) / (depth * 0.1f), 0f, 1f);   // fundo plano (para a camada de gelo)
            }
        return px;
    }

    static Vector3 Hash33(int x, int y, int z, int seed)
    {
        uint h = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed * 2654435761u);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        float a = (h & 1023) / 1023f; h = h * 1664525u + 1013904223u;
        float b = (h & 1023) / 1023f; h = h * 1664525u + 1013904223u;
        float c = (h & 1023) / 1023f;
        return new Vector3(a, b, c);
    }

    /// <summary>Voronoi 3D: distância até a borda da célula (F2 - F1) e F1.</summary>
    static float Voro(Vector3 q, int seed, out float f1)
    {
        int ix = (int)MathF.Floor(q.X), iy = (int)MathF.Floor(q.Y), iz = (int)MathF.Floor(q.Z);
        float d1 = 9f, d2 = 9f; Vector3 c1 = default;
        for (int dz = -1; dz <= 1; dz++) for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
        {
            var cp = new Vector3(ix + dx, iy + dy, iz + dz) + Hash33(ix + dx, iy + dy, iz + dz, seed) * 0.85f;
            float d = Vector3.DistanceSquared(q, cp);
            if (d < d1) { d2 = d1; d1 = d; c1 = cp; } else if (d < d2) d2 = d;
        }
        f1 = MathF.Sqrt(d1);
        return (MathF.Sqrt(d2) - f1) * 0.5f;
    }

    /// <summary>
    /// Trincas acesas numa esfera (raio R na textura): rede de placas de gelo cujas juntas brilham em ciano,
    /// abrindo em volta do ponto de impacto (hx, hy) até "reach" (ângulo, rad). Placas um pouco mais escuras com a
    /// borda chanfrada acesa, e trincas finas apagadas dentro das placas. dormant: rede inteira, fraca e esparsa.
    /// </summary>
    internal static float[] GlowCracks(int n, float R, float hx, float hy, float reach, int seed, bool dormant = false, Vector3? glowC = null, Vector3? coreC = null, float scale = 4.6f, float dormStr = 0.30f)
    {
        var GC = glowC ?? new Vector3(0.10f, 0.62f, 1.0f); var CC = coreC ?? new Vector3(0.80f, 0.98f, 1.0f);
        var px = new float[n * n * 4];
        float hz = MathF.Sqrt(MathF.Max(0.01f, R * R - hx * hx - hy * hy));
        var H = Vector3.Normalize(new Vector3(hx, hy, hz));
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n * 2f - 1f, v = (j + 0.5f) / n * 2f - 1f;
                float q = u * u + v * v;
                if (q >= R * R) continue;
                var P = new Vector3(u, v, MathF.Sqrt(R * R - q)) / R;
                float w;
                float dA = MathF.Acos(Math.Clamp(Vector3.Dot(P, H), -1f, 1f));
                if (dormant) w = 0.55f;
                else
                {
                    // Borda do trecho trincado irregular (não um círculo).
                    float jag = (Noise3(P * 4f + new Vector3(seed)) - 0.5f) * 0.35f * reach;
                    w = Sm(reach + jag, reach * 0.45f + jag, dA);
                }
                if (w <= 0.001f) continue;
                float f1;
                float edge = Voro(P * scale + new Vector3(seed * 1.3f, seed * 0.7f, 0f), seed, out f1);
                float fine = Voro(P * 11f + new Vector3(0f, seed * 2.1f, 3f), seed + 7, out _);
                float str = dormant ? dormStr : 0.75f + 0.45f * (1f - Math.Clamp(dA / MathF.Max(0.01f, reach), 0f, 1f));
                if (dormant && Noise3(P * 3f + new Vector3(seed)) < 0.5f) str *= 0.2f;   // trincas antigas só em alguns trechos
                float core = MathF.Exp(-Sq(edge / 0.026f)) * str;
                float glow = MathF.Exp(-Sq(edge / 0.13f)) * str;
                float bevel = MathF.Exp(-Sq(edge / 0.05f)) * str;
                float hair = MathF.Exp(-Sq(fine / 0.018f)) * (dormant ? 0f : 0.45f) * Sm(0.6f, 0.2f, edge * 4f);
                float plate = dormant ? 0f : 0.18f;
                // Limbo: perto da borda da bola a rede some.
                float limb = Sm(0.02f, 0.25f, P.Z);
                float a = Math.Clamp(core + glow * 0.8f + plate + hair * 0.5f, 0f, 1f) * w * limb;
                if (a < 0.004f) continue;
                var c = new Vector3(0.02f, 0.05f, 0.12f);                                   // placa escura
                c = Vector3.Lerp(c, GC, Math.Clamp(glow * 1.3f + bevel * 0.5f, 0f, 1f));   // luz vazando
                c = Vector3.Lerp(c, CC, Math.Clamp(core * 1.4f, 0f, 1f));                  // junta acesa
                c = Vector3.Lerp(c, new Vector3(0.01f, 0.03f, 0.08f), Math.Clamp(hair, 0f, 1f) * 0.7f);                 // fissuras finas
                int o = (j * n + i) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = a * Math.Clamp((R - MathF.Sqrt(q)) * n * 0.5f, 0f, 1f);
            }
        return px;
    }

    /// <summary>Brilho azul preso no miolo da pedra (como luz por baixo do gelo), mais forte no centro.</summary>
    /// <summary>Veias orgânicas acesas numa esfera (ruído em cristas): galhos finos ramificando, mais grossos no meio.</summary>
    internal static float[] Veins(int n, float R, int seed, Vector3 col, Vector3 hot)
    {
        var px = new float[n * n * 4];
        var off = new Vector3(seed * 1.7f, seed * 3.1f, seed * 0.9f);
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n * 2f - 1f, v = (j + 0.5f) / n * 2f - 1f;
                float q = u * u + v * v; if (q >= R * R) continue;
                var P = new Vector3(u, v, MathF.Sqrt(R * R - q)) / R;
                var w = new Vector3(Fbm3(P * 1.6f + off), Fbm3(P * 1.6f + off + new Vector3(5f)), 0f) - new Vector3(0.5f, 0.5f, 0f);
                float r1 = 1f - MathF.Abs(2f * Fbm3(P * 2.6f + w * 1.2f + off) - 1f);
                float r2 = 1f - MathF.Abs(2f * Fbm3(P * 5.5f + w * 0.8f + off * 1.3f) - 1f);
                float main = MathF.Pow(r1, 14f), fine = MathF.Pow(r2, 22f) * 0.6f;
                float core = Math.Clamp(main + fine, 0f, 1f);
                float glow = MathF.Pow(r1, 5f) * 0.35f;
                float limb = Sm(0.05f, 0.35f, P.Z);
                float a = Math.Clamp(core + glow, 0f, 1f) * limb;
                if (a < 0.004f) continue;
                var c = Vector3.Lerp(col * 0.6f, hot, Math.Clamp(core * 1.2f, 0f, 1f));
                int o = (j * n + i) * 4;
                px[o] = c.X; px[o + 1] = c.Y; px[o + 2] = c.Z; px[o + 3] = a;
            }
        return px;
    }

    internal static float[] InnerGlow(int n, float R)
    {
        var px = new float[n * n * 4];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n * 2f - 1f, v = (j + 0.5f) / n * 2f - 1f;
                float r = MathF.Sqrt(u * u + v * v) / R;
                if (r >= 1f) continue;
                float g = MathF.Exp(-r * r / 0.35f) * 0.55f + Noise3(new Vector3(u * 3f, v * 3f, 1.7f)) * 0.15f * (1f - r);
                int o = (j * n + i) * 4;
                px[o] = 0.15f; px[o + 1] = 0.60f; px[o + 2] = 1.0f;
                px[o + 3] = Math.Clamp(g, 0f, 1f) * Math.Clamp((1f - r) * n * 0.4f, 0f, 1f);
            }
        return px;
    }

    /// <summary>
    /// Fogo sagrado em volta da joia (um quadro de um ciclo que emenda): coluna de chamas subindo da pedra,
    /// com o ruído escorrendo para cima e a forma torcida. Cores pela temperatura: miolo branco-quente, amarelo,
    /// borda verde-limão translúcida. front = camada da frente (mais baixa, deixa a joia aparecer no meio).
    /// </summary>
    static float PHash(int x, int y, int s) { uint h = (uint)(x * 374761393 + y * 668265263 + s * 2147483647); h = (h ^ (h >> 13)) * 1274126177u; return (h & 0xFFFF) / 65535f; }

    /// <summary>Ruído de valor que se repete na vertical a cada "per" unidades (para o fogo subir em ciclo sem salto).</summary>
    static float PNoise(float x, float y, int per, int seed)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float fx = x - ix, fy = y - iy; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        int y0 = ((iy % per) + per) % per, y1 = (y0 + 1) % per;
        float a = PHash(ix, y0, seed), b = PHash(ix + 1, y0, seed), c = PHash(ix, y1, seed), d = PHash(ix + 1, y1, seed);
        return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy;
    }

    static float PFbm(float x, float y, int per, int seed)
    {
        float s = 0f, a = 0.5f, n = 0f;
        for (int o = 0; o < 4; o++) { s += a * PNoise(x, y, per, seed + o * 31); n += a; x *= 2f; y *= 2f; per *= 2; a *= 0.5f; }
        return s / n;
    }

    /// <summary>
    /// Fogo sagrado em volta da joia (um quadro de um ciclo que emenda): várias línguas de fogo subindo da pedra,
    /// o ruído sobe em ciclo perfeito (sem perder contraste). Cores pela temperatura: miolo branco-quente,
    /// amarelo, pontas verde-limão translúcidas. front = camada da frente: só as línguas dos lados e de baixo.
    /// </summary>
    /// <summary>
    /// Véu de chama sagrada (como no jogo): muitas fitas finas e translúcidas subindo bem alto, ondulando;
    /// miolo amarelo-claro, bordas puxando para o verde-água, sumindo no topo. Um quadro de ciclo que emenda.
    /// </summary>
    internal static float[] HolyVeilSheet(int n, float phase, int seed)
    {
        var px = new float[n * n * 4];
        const int PER = 3;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n * 2f - 1f, v = (j + 0.5f) / n * 2f - 1f;   // v -1 base .. 1 topo
                float h = (v + 1f) * 0.5f;                                            // 0..1
                float ny = v * 0.9f - phase * PER;
                // Fitas ondulam de lado, mais quanto mais alto.
                float sway = (PFbm(u * 0.7f + 5f, ny * 0.55f, PER, seed + 3) - 0.5f) * (0.30f + 1.3f * h);
                float uu = u + sway;
                // Fitas: cristas finas e longas (x muito fino, y muito longo).
                float r1 = 1f - MathF.Abs(2f * PFbm(uu * 3.2f + seed, ny * 0.5f, PER, seed) - 1f);
                float r2 = 1f - MathF.Abs(2f * PFbm(uu * 11f + seed * 2f, ny * 1.1f, PER * 2, seed + 40) - 1f);
                float sheet = Sm(0.45f, 0.75f, PFbm(uu * 1.8f + 2f, ny * 0.45f, PER, seed + 21));
                float strands = MathF.Pow(r1, 4f) * 0.9f + MathF.Pow(r2, 10f) * 0.35f + sheet * 0.22f;
                // Envelope: estreito na base (sai da pedra), abre um pouco no meio, some no topo.
                float width = 0.22f + 0.38f * MathF.Sin(MathF.PI * MathF.Min(1f, h * 1.1f));
                float env = MathF.Exp(-Sq(uu / width) * 1.2f) * Sm(0.0f, 0.12f, h) * Sm(1.0f, 0.55f, h);
                // Véu difuso entre as fitas (bem fraco).
                float haze = PFbm(uu * 2.5f + 9f, ny * 0.5f, PER, seed + 90);
                float a = env * (strands * 1.1f + haze * 0.12f);
                float edgeT = Sm(0.98f, 0.80f, MathF.Abs(u));
                a *= edgeT;
                if (a < 0.01f) continue;
                // Paleta da Luz: miolo branco quente, bordas com o brilho de opala (arco-íris suave) que corre pela fita.
                float hue = (uu * 0.9f + h * 1.3f + PFbm(uu * 1.2f + 4f, ny * 0.4f, PER, seed + 60) * 1.4f) % 1f; if (hue < 0f) hue += 1f;
                var rb = new Vector3(0.5f + 0.5f * MathF.Cos(hue * MathF.Tau), 0.5f + 0.5f * MathF.Cos(hue * MathF.Tau + 2.1f), 0.5f + 0.5f * MathF.Cos(hue * MathF.Tau + 4.2f));
                Vector3 core = new(1.0f, 0.99f, 0.95f), tint = Vector3.Lerp(rb, Vector3.One, 0.35f);
                var c = Vector3.Lerp(tint, core, Math.Clamp(strands * 1.3f + (1f - h) * 0.35f - 0.15f, 0f, 1f));
                int o = (j * n + i) * 4;
                px[o] = c.X; px[o + 1] = c.Y; px[o + 2] = c.Z; px[o + 3] = Math.Clamp(a * 0.95f, 0f, 1f);
            }
        return px;
    }

    /// <summary>Clarão amarelo saturado (miolo branco), como o ponto mais quente do fogo sagrado.</summary>
    internal static float[] YellowBloom(int n)
    {
        var px = new float[n * n * 4];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n * 2f - 1f, v = (j + 0.5f) / n * 2f - 1f, r = MathF.Sqrt(u * u + v * v);
                float core = MathF.Exp(-r * r / 0.03f), glow = MathF.Exp(-r * r / 0.22f);
                float ang = MathF.Atan2(v, u) / MathF.Tau + r * 1.5f;
                var rb = new Vector3(0.5f + 0.5f * MathF.Cos(ang * MathF.Tau), 0.5f + 0.5f * MathF.Cos(ang * MathF.Tau + 2.1f), 0.5f + 0.5f * MathF.Cos(ang * MathF.Tau + 4.2f));
                var c = Vector3.Lerp(Vector3.Lerp(rb, new Vector3(1f, 0.97f, 0.85f), 0.45f), Vector3.One, core);
                int o = (j * n + i) * 4;
                px[o] = c.X; px[o + 1] = c.Y; px[o + 2] = c.Z; px[o + 3] = Math.Clamp(core + glow * 0.85f, 0f, 1f);
            }
        return px;
    }

    /// <summary>Lasca de luz (traço curto e curvo, branco com borda verde-água) que espirra de lado.</summary>
    internal static float[] LightShard(int n)
    {
        var px = new float[n * n * 4];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n * 2f - 1f, v = (j + 0.5f) / n * 2f - 1f;
                float cx = 0.25f * v * v - 0.1f;                                      // curvinha
                float w = 0.16f * MathF.Max(0f, 1f - MathF.Abs(v));
                float d = MathF.Abs(u - cx);
                if (d > w * 2.2f) continue;
                float core = MathF.Exp(-Sq(d / MathF.Max(0.01f, w * 0.6f))), glow = MathF.Exp(-Sq(d / MathF.Max(0.01f, w * 1.8f))) * 0.5f;
                float hh = (v * 0.5f + 0.5f + u) % 1f; if (hh < 0f) hh += 1f;
                var rb2 = new Vector3(0.5f + 0.5f * MathF.Cos(hh * MathF.Tau), 0.5f + 0.5f * MathF.Cos(hh * MathF.Tau + 2.1f), 0.5f + 0.5f * MathF.Cos(hh * MathF.Tau + 4.2f));
                var c = Vector3.Lerp(Vector3.Lerp(rb2, Vector3.One, 0.4f), Vector3.One, core);
                int o = (j * n + i) * 4;
                px[o] = c.X; px[o + 1] = c.Y; px[o + 2] = c.Z; px[o + 3] = Math.Clamp(core + glow, 0f, 1f) * Sm(1f, 0.6f, MathF.Abs(v));
            }
        return px;
    }

    internal static float[] HolyFireSheet(int n, float phase, bool front, int seed = 3)
    {
        var px = new float[n * n * 4];
        const int PER = 4;                                 // período vertical do ruído (em unidades de ruído)
        float sy = -0.40f, top = front ? 1.0f : 1.45f;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n * 2f - 1f, v = (j + 0.5f) / n * 2f - 1f;
                float h = v - sy;
                // Ruído esticado na vertical, subindo: y desce com a fase (um período inteiro por ciclo).
                float ny = v * 1.6f - phase * PER;
                float warp = (PFbm(u * 1.6f + 11f, ny * 0.6f, PER, seed + 7) - 0.5f) * 0.50f * Math.Clamp(h + 0.1f, 0f, 1.2f);
                float uu = u + warp;
                // Fiapos verticais: ruído bem esticado (x fino, y longo) em forma de crista.
                float r1 = 1f - MathF.Abs(2f * PFbm(uu * 6.5f + seed, ny, PER, seed) - 1f);
                float r2 = PFbm(uu * 13f + 3f, ny * 2f, PER * 2, seed + 50);
                float hn = Math.Clamp(h / top, 0f, 1.3f);
                float width = 0.44f * MathF.Pow(MathF.Max(0f, 1f - hn * 0.8f), 0.7f) + 0.03f;
                float g = MathF.Exp(-Sq(uu / width) * 1.4f);
                float below = h < 0f ? Math.Clamp(1f + h / 0.40f, 0f, 1f) : 1f;
                float field = g * MathF.Pow(MathF.Max(0f, 1f - hn), 0.6f) * below;
                if (field < 0.04f) continue;
                // O campo vira línguas: cristas finas levam o fogo para cima, o resto se apaga.
                float tongues = MathF.Pow(r1, 2.2f) * (0.75f + 0.5f * r2);
                float dens = field * (0.35f + 1.25f * tongues) - 0.22f - hn * 0.10f;
                if (front)
                {
                    float cr = MathF.Sqrt(u * u + Sq(v - sy - 0.12f) * 1.3f);
                    dens -= Sm(0.40f, 0.10f, cr) * 1.2f;                                 // furo no meio: a joia aparece
                    dens *= Sm(-0.35f, 0.05f, h);                                       // nada embaixo da pedra
                }
                float edge = Sm(0.98f, 0.78f, MathF.Abs(u)) * Sm(0.98f, 0.80f, v) * Sm(-0.98f, -0.80f, v);
                dens *= edge;
                if (dens <= 0f) continue;
                float aa = Sm(0f, 0.18f, dens);
                float T = Math.Clamp(dens * 1.5f + (1f - Math.Clamp(hn, 0f, 1f)) * 0.10f - 0.05f, 0f, 1f);
                var lime = new Vector3(0.70f, 0.92f, 0.22f);
                var yel = new Vector3(1.0f, 0.95f, 0.38f);
                var hot = new Vector3(1.0f, 1.0f, 0.90f);
                var c = T < 0.5f ? Vector3.Lerp(lime, yel, T / 0.5f) : Vector3.Lerp(yel, hot, Math.Clamp((T - 0.5f) / 0.5f, 0f, 1f));
                aa *= 0.45f + 0.55f * T;
                int o = (j * n + i) * 4;
                px[o] = c.X; px[o + 1] = c.Y; px[o + 2] = c.Z; px[o + 3] = Math.Clamp(aa, 0f, 1f);
            }
        return px;
    }


    internal static void Fade(float[] px, float k) { for (int i = 3; i < px.Length; i += 4) px[i] *= k; }

    internal static float[] CarvedRelief(float[] ball, Func<float, float, float> sd, int n, float depth = 0.07f, float wall = 0.035f, int ballN = 0)
    {
        if (ballN == 0) ballN = n;
        var px = new float[n * n * 4];
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        var L2 = Vector2.Normalize(new Vector2(L.X, L.Y));
        float D(float x, float y) => depth * Sm(wall * 0.5f, -wall * 0.5f, sd(x, y));   // profundidade (0 fora, depth no fundo)
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n * 2f - 1f, v = (j + 0.5f) / n * 2f - 1f;
                float d0 = sd(u, v);
                if (d0 > wall * 0.5f + 0.01f) continue;
                float q = u * u + v * v; if (q > 0.99f) continue;
                const float e = 0.008f;
                float h = D(u, v), hx = (D(u + e, v) - D(u - e, v)) / (2f * e), hy = (D(u, v + e) - D(u, v - e)) / (2f * e);
                var n0 = new Vector3(u, v, MathF.Sqrt(MathF.Max(0.02f, 1f - q)));
                var N = Vector3.Normalize(n0 + new Vector3(hx, hy, 0f) * 1.0f);   // descendo para dentro: normal vira para o centro do buraco
                float diff = MathF.Max(0f, Vector3.Dot(N, L));
                // Sombra: a parede do lado da luz encobre o fundo (anda em direção à luz e vê se sai do buraco a tempo).
                float sh = 1f;
                if (h > depth * 0.3f)
                    for (int k = 1; k <= 6; k++)
                    {
                        float tt = k / 6f * depth * 1.6f;
                        if (sd(u + L2.X * tt, v + L2.Y * tt) > -wall * 0.2f) { sh = 0.35f + 0.65f * (k - 1) / 6f; break; }
                    }
                float ao = 0.55f + 0.45f * Math.Clamp(-d0 / 0.10f, 0f, 1f);
                float lit = (0.22f + 0.95f * diff) * (h > 0.001f ? ao * sh : 1f) * (1f - 0.35f * h / depth);
                int bi = (Math.Min(ballN - 1, j * ballN / n) * ballN + Math.Min(ballN - 1, i * ballN / n)) * 4;
                var c = new Vector3(ball[bi], ball[bi + 1], ball[bi + 2]);
                // A cor da bola já tem luz embutida: tira a luz da esfera e aplica a do entalhe.
                float baseLit = 0.35f + 1.05f * MathF.Max(0f, Vector3.Dot(n0, L));
                c = c / MathF.Max(0.25f, baseLit) * lit;
                // Aresta da borda pega luz (quina viva).
                float rimEdge = MathF.Exp(-MathF.Pow((d0 - wall * 0.5f) / 0.010f, 2f)) * MathF.Max(0f, Vector3.Dot(new Vector3(-hx, -hy, 0.3f), L)) * 0.0f;
                c += new Vector3(rimEdge);
                int o = (j * n + i) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = Math.Clamp((wall * 0.5f + 0.01f - d0) * n * 0.5f, 0f, 1f) * (ball[bi + 3]);
            }
        return px;
    }

    internal static float[] CarvedSocket(Func<float, float, float> sd, int n, float wall = 0.10f, int seed = 3)
    {
        var px = new float[n * n * 4];
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        var off = new Vector3(seed * 2.3f, seed * 4.1f, 1.7f);
        float H(float x, float y)
        {
            float d = sd(x, y);
            if (d > 0f) return 0f;
            float k = Math.Clamp(-d / wall, 0f, 1f);
            float rough = (Fbm3(new Vector3(x * 9f, y * 9f, 0f) + off) - 0.5f) * 0.05f;
            return -(k * k * (3f - 2f * k)) * 0.30f + rough * k;           // desce pela parede até o fundo
        }
        float e = 2f / n;
        for (int yy = 0; yy < n; yy++)
            for (int xx = 0; xx < n; xx++)
            {
                float x = (xx + 0.5f) / n * 2f - 1f, y = (yy + 0.5f) / n * 2f - 1f;
                float d = sd(x, y);
                float cover = Math.Clamp(0.5f - d * n * 0.5f, 0f, 1f);
                float lip = Math.Clamp(1f - MathF.Abs(d - 0.012f) / 0.014f, 0f, 1f);      // borda lascada em volta
                if (cover <= 0f && lip <= 0f) continue;
                float h = H(x, y);
                var N = Vector3.Normalize(new Vector3((H(x - e, y) - H(x + e, y)) / (2f * e), (H(x, y - e) - H(x, y + e)) / (2f * e), 1f));
                float ndl = MathF.Max(0f, Vector3.Dot(N, L));
                float depth = Math.Clamp(-h / 0.30f, 0f, 1f);
                // Sombra: o fundo perto da borda de cima/esquerda fica na sombra da própria borda.
                float sh = 0f;
                for (int k = 1; k <= 6; k++)
                {
                    float t = k * 0.03f;
                    if (sd(x - L.X * t, y - L.Y * t) > 0f && depth > t * 1.6f) { sh = 1f; break; }
                }
                var stone = Vector3.Lerp(new Vector3(0.22f, 0.21f, 0.22f), new Vector3(0.30f, 0.12f, 0.09f),
                    Math.Clamp((Fbm3(new Vector3(x * 3f, y * 3f, 0.3f) + off) - 0.45f) * 3f, 0f, 1f) * 0.7f);
                var c = stone * (0.18f + 0.95f * ndl) * (1f - 0.55f * depth) * (1f - 0.6f * sh);
                float a = cover;
                if (lip > 0f && cover < 1f)
                {
                    // Borda da pedra em volta da cavidade (chanfro claro do lado da luz).
                    float lit = Math.Clamp(0.5f - (x * L.X + y * L.Y) * 0.6f, 0f, 1f);
                    var lc = new Vector3(0.40f, 0.38f, 0.38f) * (0.4f + 0.6f * lit);
                    c = Vector3.Lerp(lc, c, cover);
                    a = MathF.Max(cover, lip * 0.85f);
                }
                int o = (yy * n + xx) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = a;
            }
        return px;
    }

    /// <summary>Poça de sangue brilhando no fundo do encaixe (mais forte no meio, borda irregular).</summary>
    internal static float[] SocketPool(Func<float, float, float> sd, int n, Vector3 col, float wall = 0.10f)
    {
        var px = new float[n * n * 4];
        for (int yy = 0; yy < n; yy++)
            for (int xx = 0; xx < n; xx++)
            {
                float x = (xx + 0.5f) / n * 2f - 1f, y = (yy + 0.5f) / n * 2f - 1f;
                float d = sd(x, y) + wall * 0.9f + (Noise3(new Vector3(x * 7f, y * 7f, 2.2f)) - 0.5f) * 0.05f;
                if (d > 0.02f) continue;
                float k = Math.Clamp(-d / 0.20f, 0f, 1f);
                var c = Vector3.Lerp(col * 0.30f, Vector3.Lerp(col, Vector3.One, 0.12f), k * k * 0.85f);
                float sheen = MathF.Pow(Math.Clamp(1f - MathF.Abs(d + 0.025f) / 0.012f, 0f, 1f), 2f) * 0.35f;   // menisco claro
                c += new Vector3(sheen);
                int o = (yy * n + xx) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = Math.Clamp(0.5f - d * n * 0.5f, 0f, 1f) * 0.95f;
            }
        return px;
    }

    /// <summary>
    /// Gosma tomando o anel: placas pretas e molhadas cobrindo trechos do aro (mais com cobertura maior)
    /// e escorrendo em fios com gota na ponta pela metade de baixo.
    /// </summary>
    internal static float[] GooRing(int n, float coverage, int seed = 4, float ringR = 0.927f)
    {
        var px = new float[n * n * 4];
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        var rnd = new Random(seed);
        float Mask(float a) => Sm(1f - coverage - 0.06f, 1f - coverage + 0.06f, Noise3(new Vector3(MathF.Cos(a) * 1.6f + seed, MathF.Sin(a) * 1.6f, 0.7f)) * 0.9f + 0.05f + 0.25f * Sm(0.2f, 1f, MathF.Sin(a) * 0.5f + 0.5f));
        // Fios pendurados: só onde há gosma, na metade de baixo.
        var drips = new List<(float x, float y, float len, float w)>();
        for (int i = 0; i < 40 && drips.Count < 2 + (int)(coverage * 6f); i++)
        {
            float a = -MathF.PI * (0.15f + 0.7f * (float)rnd.NextDouble());
            if (Mask(a) < 0.6f) continue;
            drips.Add((MathF.Cos(a) * ringR, MathF.Sin(a) * ringR, 0.05f + 0.10f * (float)rnd.NextDouble() * (0.5f + coverage), 0.018f + 0.010f * (float)rnd.NextDouble()));
        }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = MathF.Sqrt(u * u + v * v), a = MathF.Atan2(v, u);
                Vector3 N = Vector3.Zero; float cov = 0f;
                float m = Mask(a);
                float W = (0.060f + 0.025f * Noise3(new Vector3(a * 3f + seed, 1.1f, 2f))) * m;
                float rd = r - ringR;
                if (W > 0.005f && MathF.Abs(rd) < W)
                {
                    float q = rd / W, h = MathF.Sqrt(MathF.Max(0f, 1f - q * q));
                    var radial = new Vector3(u / MathF.Max(r, 1e-4f), v / MathF.Max(r, 1e-4f), 0f);
                    N = Vector3.Normalize(radial * q * 1.2f + new Vector3(0, 0, h + 0.05f));
                    cov = Math.Clamp((W - MathF.Abs(rd)) * n * 0.5f, 0f, 1f);
                }
                foreach (var (dx0, dy0, len, w) in drips)
                {
                    float dy = dy0 - v, dx = u - dx0;
                    if (dy < -w || dy > len + w * 2.5f) continue;
                    float k = Math.Clamp(dy / len, 0f, 1f);
                    float rr = w * (1f - 0.55f * k) + w * 1.3f * MathF.Exp(-Sq2((dy - len) / (w * 1.4f)));
                    float ad = MathF.Abs(dx);
                    if (ad < rr && (dy < len + rr))
                    {
                        float c = Math.Clamp((rr - ad) * n * 0.5f, 0f, 1f);
                        if (dy > len) c *= Math.Clamp((len + rr - dy) * n * 0.5f, 0f, 1f);
                        if (c > cov) { cov = c; float qx = dx / rr; N = Vector3.Normalize(new Vector3(qx, dy > len ? -(dy - len) / rr : 0f, MathF.Sqrt(MathF.Max(0.05f, 1f - qx * qx)))); }
                    }
                }
                if (cov <= 0f) continue;
                N = Vector3.Normalize(N + new Vector3(Noise3(new Vector3(u * 14f, v * 14f, 1f)) - 0.5f, Noise3(new Vector3(u * 14f, v * 14f, 4f)) - 0.5f, 0f) * 0.5f);
                var R = Vector3.Reflect(-Vector3.UnitZ, N);
                float sb = Sm(0.45f, 0.62f, R.Y) * Sm(-0.85f, -0.65f, R.X) * (1f - Sm(-0.25f, -0.05f, R.X));
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, Vector3.Normalize(L + Vector3.UnitZ))), 70f) * 1.3f;
                float fres = MathF.Pow(1f - MathF.Max(0f, N.Z), 3f);
                var col = new Vector3(0.02f, 0.014f, 0.03f) * (0.6f + 0.8f * MathF.Max(0f, Vector3.Dot(N, L)))
                        + new Vector3(0.95f, 0.92f, 1f) * (sb * 0.9f + spec) + new Vector3(0.55f, 0.25f, 0.85f) * fres * 0.5f;
                int o = (y * n + x) * 4;
                px[o] = Math.Clamp(col.X, 0f, 1f); px[o + 1] = Math.Clamp(col.Y, 0f, 1f); px[o + 2] = Math.Clamp(col.Z, 0f, 1f);
                px[o + 3] = cov;
            }
        return px;
    }
    static float Sq2(float x) => x * x;

    /// <summary>Bolha 3D: película fina e transparente com cores de interferência na borda e reflexos de janela.</summary>
    internal static float[] Bubble3D(int n, Vector3 tint, int seed = 2)
    {
        var px = new float[n * n * 4];
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                const float R = 0.86f;
                float d = MathF.Sqrt(u * u + v * v);
                float edge = Math.Clamp((R - d) * n * 0.5f + 0.5f, 0f, 1f);
                if (edge <= 0f) continue;
                float uu = u / R, vv = v / R;
                var N = new Vector3(uu, vv, MathF.Sqrt(MathF.Max(0f, 1f - uu * uu - vv * vv)));
                float f = MathF.Pow(1f - N.Z, 2.2f);
                // Interferência da película: verde, roxo e dourado girando com o ângulo e uma mancha de espessura.
                float th = N.Z * 5.5f + Noise3(new Vector3(uu * 2.5f + seed, vv * 2.5f, 1.3f)) * 3f;
                var film = new Vector3(0.5f + 0.5f * MathF.Cos(th), 0.5f + 0.5f * MathF.Cos(th + 2.1f), 0.5f + 0.5f * MathF.Cos(th + 4.2f));
                film = Vector3.Lerp(film, tint, 0.45f);
                var Rf = Vector3.Reflect(-Vector3.UnitZ, N);
                float win = Sm(0.35f, 0.5f, Rf.Y) * Sm(-0.75f, -0.55f, Rf.X) * (1f - Sm(-0.30f, -0.15f, Rf.X)) * (1f - Sm(0.75f, 0.85f, Rf.Y));
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, Vector3.Normalize(L + Vector3.UnitZ))), 120f);
                float win2 = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, Vector3.Normalize(new Vector3(0.5f, -0.45f, 0.75f)))), 60f) * 0.6f;
                float a = 0.08f + 0.85f * f + win * 0.8f + spec + win2;
                var c = film * (0.4f + 0.8f * f) + new Vector3(1f) * (win * 0.9f + spec + win2);
                a = Math.Clamp(a, 0f, 1f);
                int o = (y * n + x) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = a * edge;
            }
        return px;
    }

    /// <summary>
    /// Poça de líquido no encaixe: superfície que sobe nas paredes (menisco), ondinhas, reflexo de luz
    /// e brilho próprio mais forte no fundo.
    /// </summary>
    internal static float[] LiquidPool(Func<float, float, float> sd, int n, Vector3 col, float wall = 0.10f)
    {
        var px = new float[n * n * 4];
        float D(float x, float y) => sd(x, y) + wall * 0.9f;
        for (int yy = 0; yy < n; yy++)
            for (int xx = 0; xx < n; xx++)
            {
                float x = (xx + 0.5f) / n * 2f - 1f, y = (yy + 0.5f) / n * 2f - 1f;
                float d = D(x, y);
                if (d > 0.02f) continue;
                float k = Math.Clamp(-d / 0.22f, 0f, 1f);
                const float e = 0.01f;
                float gx = (D(x + e, y) - d) / e, gy = (D(x, y + e) - d) / e;
                float men = MathF.Exp(d / 0.035f);                          // perto da parede a superfície sobe
                float rx = (Noise3(new Vector3(x * 9f + 0.01f, y * 9f, 3f)) - Noise3(new Vector3(x * 9f, y * 9f, 3f))) / 0.01f;
                float ry = (Noise3(new Vector3(x * 9f, y * 9f + 0.01f, 3f)) - Noise3(new Vector3(x * 9f, y * 9f, 3f))) / 0.01f;
                var N = Vector3.Normalize(new Vector3(gx * men * 1.6f - rx * 0.025f, gy * men * 1.6f - ry * 0.025f, 1f));
                var Rf = Vector3.Reflect(-Vector3.UnitZ, N);
                float win = Sm(0.30f, 0.55f, Rf.Y) * Sm(-0.85f, -0.55f, Rf.X) * (1f - Sm(-0.15f, 0.10f, Rf.X));
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, Vector3.Normalize(new Vector3(-0.45f, 0.65f, 1.6f)))), 160f);
                float fres = MathF.Pow(1f - N.Z, 2f);
                var c = Vector3.Lerp(col * 0.25f, col, 0.35f + 0.65f * k * k);    // mais fundo = mais aceso
                c += Vector3.Lerp(col, Vector3.One, 0.6f) * (win * 0.55f + fres * 0.5f) + new Vector3(1f) * spec * 0.9f;
                int o = (yy * n + xx) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = Math.Clamp(0.5f - d * n * 0.5f, 0f, 1f);
            }
        return px;
    }

    /// <summary>Tubo afinando ao longo de um caminho (chifres, ossos de asa, bordas).</summary>
    internal static Mesh Tube(IList<Vector3> path, Func<float, float> radius, int side = 10, int kind = 0)
    {
        var m = new Mesh();
        int n = path.Count;
        var up = Vector3.UnitZ;
        for (int i = 0; i < n; i++)
        {
            var t = Vector3.Normalize(path[Math.Min(n - 1, i + 1)] - path[Math.Max(0, i - 1)]);
            var a = Vector3.Cross(t, up); if (a.LengthSquared() < 1e-6f) a = Vector3.Cross(t, Vector3.UnitX);
            a = Vector3.Normalize(a); var b = Vector3.Cross(t, a);
            float r = radius(i / (float)(n - 1));
            for (int j = 0; j < side; j++)
            {
                float v = j / (float)side * MathF.Tau;
                m.V.Add(path[i] + (a * MathF.Cos(v) + b * MathF.Sin(v)) * r);
            }
        }
        for (int i = 0; i + 1 < n; i++)
            for (int j = 0; j < side; j++)
            {
                int a0 = i * side + j, b0 = (i + 1) * side + j, c0 = (i + 1) * side + (j + 1) % side, d0 = i * side + (j + 1) % side;
                m.F.Add((a0, b0, c0, kind)); m.F.Add((a0, c0, d0, kind));
            }
        return m;
    }

    /// <summary>Superfície em grade (u, v em 0..1) — tecido, membranas.</summary>
    internal static Mesh Sheet(Func<float, float, Vector3> f, int nu, int nv, int kind = 0)
    {
        var m = new Mesh();
        for (int j = 0; j <= nv; j++) for (int i = 0; i <= nu; i++) m.V.Add(f(i / (float)nu, j / (float)nv));
        for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                int a = j * (nu + 1) + i, b = a + 1, c = a + nu + 2, d = a + nu + 1;
                m.F.Add((a, b, c, kind)); m.F.Add((a, c, d, kind));
            }
        return m;
    }

    /// <summary>Esfera/elipsoide (UV).</summary>
    internal static Mesh Ellipsoid(Vector3 r, int seg = 16, int ring = 10, int kind = 0)
        => Sheet((u, v) =>
        {
            float th = u * MathF.Tau, ph = v * MathF.PI;
            return new Vector3(MathF.Cos(th) * MathF.Sin(ph) * r.X, -MathF.Cos(ph) * r.Y, MathF.Sin(th) * MathF.Sin(ph) * r.Z);
        }, seg, ring, kind);

    /// <summary>
    /// Par de olhos 3D de criatura: globo ocular sombreado como esfera, íris vermelha incandescente com
    /// fibras, pupila em fenda, brilho especular e a pálpebra fazendo sombra em cima. aspect = altura/largura
    /// do sprite onde será desenhada (o desenho compensa o achatamento).
    /// </summary>
    internal static float[] EyePair(int n, float aspect, Vector3 glowCol)
    {
        var px = new float[n * n * 4];
        var L = Vector3.Normalize(new Vector3(-0.4f, 0.6f, 0.7f));
        var Hh = Vector3.Normalize(L + Vector3.UnitZ);
        for (int yy = 0; yy < n; yy++)
            for (int xx = 0; xx < n; xx++)
            {
                float x = (xx + 0.5f) / n * 2f - 1f, y = ((yy + 0.5f) / n * 2f - 1f) * aspect;   // espaço da tela
                float ax = MathF.Abs(x);
                float ex = ax - 0.43f, ey = y - (ax - 0.43f) * 0.22f;                         // canto de fora mais alto
                // Abertura amendoada.
                float w = 0.26f, h = 0.11f;
                float q = ex / w; float open = h * MathF.Max(0f, 1f - q * q);
                float inside = open - MathF.Abs(ey);
                float aa = Math.Clamp(inside * n * 0.5f / aspect + 0.5f, 0f, 1f);
                float glow = MathF.Exp(-MathF.Max(0f, -inside) / 0.05f) * Math.Clamp(1f - MathF.Abs(q) * 0.8f, 0f, 1f);
                Vector3 c = glowCol * 0.9f; float a = glow * 0.55f;
                if (aa > 0f)
                {
                    // Globo: esfera de raio maior que a abertura.
                    float R = 0.24f, u = ex / R, v = ey / R, qq = MathF.Min(u * u + v * v, 0.98f);
                    var N = new Vector3(u, v, MathF.Sqrt(1f - qq));
                    float ndl = MathF.Max(0f, Vector3.Dot(N, L));
                    float r = MathF.Sqrt(ex * ex + ey * ey);
                    Vector3 col;
                    if (r < 0.10f)
                    {
                        // Íris: vermelho incandescente mais claro no meio, fibras radiais.
                        float k = r / 0.10f;
                        float fib = 0.5f + 0.5f * MathF.Sin(MathF.Atan2(ey, ex) * 26f);
                        col = Vector3.Lerp(new Vector3(1f, 0.75f, 0.45f), glowCol, k) * (0.85f + 0.15f * fib);
                        col = Vector3.Lerp(col, glowCol * 0.35f, Math.Clamp((k - 0.85f) / 0.15f, 0f, 1f));   // aro escuro
                        float slit = MathF.Abs(ex) - 0.018f * MathF.Sqrt(MathF.Max(0f, 1f - MathF.Pow(ey / 0.10f, 2f)));
                        if (slit < 0f) col = new Vector3(0.02f, 0f, 0.01f);
                    }
                    else col = new Vector3(0.22f, 0.05f, 0.05f) * (0.35f + 0.8f * ndl);   // branco do olho, avermelhado e escuro
                    // Sombra da pálpebra de cima e brilho molhado.
                    float lid = Math.Clamp((ey - (open - 0.04f)) / 0.04f + 1f, 0f, 1f);
                    col *= 1f - 0.6f * Math.Clamp((ey + 0.02f - open * 0.5f) / (open * 0.6f + 1e-3f), 0f, 1f);
                    float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, Hh)), 60f);
                    col += new Vector3(spec * 1.2f);
                    c = Vector3.Lerp(c, col, aa); a = MathF.Max(a, aa);
                }
                if (a <= 0.003f) continue;
                int o = (yy * n + xx) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f); px[o + 3] = Math.Clamp(a, 0f, 1f);
            }
        return px;
    }

    /// <summary>Corte rosa: cúpula de facetas triangulares subindo até um pico (sem mesa plana). Fundo achatado.</summary>
    internal static Mesh Rose(Vector2[] girdle, float height = 0.45f, int rings = 3)
    {
        var m = new Mesh(); int n = girdle.Length;
        var c = Vector2.Zero; foreach (var q in girdle) c += q; c /= n;
        for (int r = 0; r < rings; r++)
        {
            float k = r / (float)rings, sc = MathF.Cos(k * MathF.PI / 2f), z = MathF.Sin(k * MathF.PI / 2f) * height;
            float tw = (r & 1) == 1 ? 0.5f : 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (i + tw) / n; int i0 = (int)MathF.Floor(t * n) % n, i1 = (i0 + 1) % n; float f = t * n - MathF.Floor(t * n);
                var q = Vector2.Lerp(girdle[i0], girdle[i1], f);
                m.V.Add(new Vector3(c + (q - c) * sc, z));
            }
        }
        m.V.Add(new Vector3(c, height)); int apex = m.V.Count - 1;
        m.V.Add(new Vector3(c, -0.15f)); int bot = m.V.Count - 1;
        for (int r = 0; r + 1 < rings; r++)
            for (int i = 0; i < n; i++)
            {
                int a0 = r * n + i, a1 = r * n + (i + 1) % n, b0 = (r + 1) * n + i, b1 = (r + 1) * n + (i + n - 1) % n;
                if ((r & 1) == 0) { m.F.Add((a0, a1, b0, 0)); m.F.Add((a0, b0, (r + 1) * n + (i + n - 1) % n, 0)); }
                else { m.F.Add((a0, a1, (r + 1) * n + (i + 1) % n, 0)); m.F.Add((a0, (r + 1) * n + (i + 1) % n, b0, 0)); }
            }
        int last = (rings - 1) * n;
        for (int i = 0; i < n; i++) m.F.Add((last + i, last + (i + 1) % n, apex, 0));
        for (int i = 0; i < n; i++) m.F.Add(((i + 1) % n, i, bot, 0));
        return m;
    }

    /// <summary>Ponta de cristal: prisma hexagonal em pé com terminação pontuda (como quartzo).</summary>
    internal static Mesh CrystalPoint(float r = 0.30f, float h0 = -0.85f, float h1 = 0.35f, float tip = 0.90f)
    {
        var m = new Mesh();
        for (int i = 0; i < 6; i++) { float a = i / 6f * MathF.Tau + 0.3f; m.V.Add(new Vector3(MathF.Cos(a) * r, h0, MathF.Sin(a) * r)); }
        for (int i = 0; i < 6; i++) { float a = i / 6f * MathF.Tau + 0.3f; m.V.Add(new Vector3(MathF.Cos(a) * r, h1, MathF.Sin(a) * r)); }
        m.V.Add(new Vector3(0, tip, 0)); int t = 12;
        for (int i = 0; i < 6; i++) { int j = (i + 1) % 6; m.F.Add((i, j, 6 + j, 0)); m.F.Add((i, 6 + j, 6 + i, 0)); m.F.Add((6 + i, 6 + j, t, 0)); }
        return m;
    }

    /// <summary>Briolette: gota toda facetada (sem mesa), sólido de revolução com poucas divisões.</summary>
    internal static Mesh Briolette(int seg = 10, int rings = 7)
    {
        var m = Sheet((u, v) =>
        {
            float th = u * MathF.Tau + v * 0.6f, y = 0.92f - 1.84f * v, k = v;
            float r = k < 0.6f ? 0.60f * MathF.Pow(k / 0.6f, 1.1f) : 0.60f * MathF.Sqrt(MathF.Max(0f, 1f - MathF.Pow((k - 0.6f) / 0.4f, 2f)));
            return new Vector3(MathF.Cos(th) * r, y, MathF.Sin(th) * r);
        }, seg, rings);
        return m;
    }

    /// <summary>Octaedro (bipirâmide de base quadrada), como um diamante bruto natural.</summary>
    internal static Mesh Octahedron(float s = 0.62f)
    {
        var m = new Mesh();
        m.V.Add(new Vector3(0, s, 0)); m.V.Add(new Vector3(0, -s, 0));
        for (int i = 0; i < 4; i++) { float a = i / 4f * MathF.Tau; m.V.Add(new Vector3(MathF.Cos(a) * s * 0.75f, 0, MathF.Sin(a) * s * 0.75f)); }
        for (int i = 0; i < 4; i++) { int a = 2 + i, b = 2 + (i + 1) % 4; m.F.Add((0, a, b, 0)); m.F.Add((1, b, a, 0)); }
        return m;
    }

    /// <summary>Raios do sol: n pirâmides triangulares lapidadas em volta do centro, giradas por "spin" (radianos).</summary>
    internal static Mesh SunRays(int n, float spin, float r0 = 0.40f, float r1 = 0.80f, float half = 0.17f)
    {
        var m = new Mesh();
        for (int k = 0; k < n; k++)
        {
            float a = spin + k * MathF.Tau / n;
            Vector3 P(float x, float y, float z) { float c = MathF.Cos(a - MathF.PI / 2f), s = MathF.Sin(a - MathF.PI / 2f); return new Vector3(x * c - y * s, x * s + y * c, z); }
            int b = m.V.Count;
            m.V.Add(P(-half, r0, 0f)); m.V.Add(P(half, r0, 0f)); m.V.Add(P(0f, r1, 0f));
            float my = r0 + (r1 - r0) * 0.38f;
            m.V.Add(P(0f, my, 0.13f));                       // crista de cima
            m.V.Add(P(0f, my, -0.06f));                      // costas
            m.V.Add(P(-half * 0.45f, r0 + 0.03f, 0.08f)); m.V.Add(P(half * 0.45f, r0 + 0.03f, 0.08f));   // chanfro da base
            m.F.Add((b + 0, b + 5, b + 3, 0)); m.F.Add((b + 5, b + 6, b + 3, 0)); m.F.Add((b + 6, b + 1, b + 3, 0));
            m.F.Add((b + 1, b + 2, b + 3, 0)); m.F.Add((b + 2, b + 0, b + 3, 0));
            m.F.Add((b + 0, b + 1, b + 5, 0)); m.F.Add((b + 1, b + 6, b + 5, 0));
            m.F.Add((b + 1, b + 0, b + 4, 0)); m.F.Add((b + 2, b + 1, b + 4, 0)); m.F.Add((b + 0, b + 2, b + 4, 0));
        }
        return m;
    }

    /// <summary>Junta malhas (com deslocamento) numa só.</summary>
    internal static Mesh Merge(params (Mesh m, Vector3 at, float scale)[] parts)
    {
        var r = new Mesh();
        foreach (var (m, at, sc) in parts)
        {
            int b0 = r.V.Count;
            foreach (var v in m.V) r.V.Add(v * sc + at);
            foreach (var (a, b, c, k) in m.F) r.F.Add((a + b0, b + b0, c + b0, k));
        }
        return r;
    }

    /// <summary>Compõe b por cima de a (mesmo tamanho, formato RGBA float).</summary>
    internal static float[] Over(float[] a, float[] b)
    {
        var r = (float[])a.Clone();
        for (int i = 0; i < r.Length; i += 4)
        {
            float ba = b[i + 3]; if (ba <= 0f) continue;
            float aa = r[i + 3], oa = ba + aa * (1f - ba);
            for (int k = 0; k < 3; k++) r[i + k] = (b[i + k] * ba + r[i + k] * aa * (1f - ba)) / Math.Max(oa, 1e-5f);
            r[i + 3] = oa;
        }
        return r;
    }

    /// <summary>
    /// Aro de metal 3D em volta da bola: tubo com frisos (dois cordões finos nas bordas), e no ouro
    /// oito rubis lapidados cravados em engastes. metal: 0 bronze, 1 prata, 2 ouro.
    /// </summary>
    internal static float[] MetalRing(int metal, int n, Vector3? gemCol = null)
    {
        var col = metal == 2 ? new Vector3(0.98f, 0.72f, 0.28f) : metal == 1 ? new Vector3(0.80f, 0.83f, 0.88f) : new Vector3(0.78f, 0.45f, 0.22f);
        var mat = new GemMat { Base = col, Metal = true, Edges = false };
        var band = Merge((Torus(0.935f, 0.045f, 72, 10), Vector3.Zero, 1f),
                         (Torus(0.875f, 0.016f, 72, 6), new Vector3(0, 0, 0.01f), 1f),
                         (Torus(0.993f, 0.016f, 72, 6), new Vector3(0, 0, 0.01f), 1f));
        var img = Render(band, 0f, 0f, n, mat, 1f);
        if (metal != 2) return img;
        // Engastes (discos de ouro) e rubis lapidados.
        var parts = new List<(Mesh, Vector3, float)>();
        var gems = new List<(Mesh, Vector3, float)>();
        var round = new Vector2[16];
        for (int i = 0; i < 16; i++) { float a = i / 16f * MathF.Tau; round[i] = new Vector2(MathF.Cos(a), MathF.Sin(a)) * 0.5f; }
        var cut = Cut(round, table: 0.45f, crown: 0.30f, depth: 0.5f, convex: true);
        for (int k = 0; k < 8; k++)
        {
            float a = k / 8f * MathF.Tau + MathF.PI / 2f;
            var at = new Vector3(MathF.Cos(a) * 0.935f, MathF.Sin(a) * 0.935f, 0.05f);
            parts.Add((Torus(0.075f, 0.022f, 24, 6), at, 1f));
            gems.Add((cut, at + new Vector3(0, 0, 0.04f), 0.15f));
        }
        img = Over(img, Render(Merge(parts.ToArray()), 0f, 0f, n, mat, 1f));
        var gc = gemCol ?? new Vector3(0.92f, 0.06f, 0.14f);
        var ruby = new GemMat { Base = gc, Deep = gc * 0.5f, Sparkle = 0.5f, Edges = false, Fire = gemCol.HasValue && gc.X + gc.Y + gc.Z > 2.2f };
        return Over(img, Render(Merge(gems.ToArray()), 0f, 0f, n, ruby, 1f));
    }

    /// <summary>
    /// Opala branca: pedra leitosa e reluzente (luz "envolvente", sem lado escuro duro), com manchas de
    /// cores do arco-íris que mudam conforme a curvatura (jogo de cores) e brilho polido.
    /// </summary>
    internal static float[] WhiteOpal(int n, float radius, int seed = 5, float phase = 0f)
    {
        var drift = new Vector3(MathF.Cos(phase * MathF.Tau), MathF.Sin(phase * MathF.Tau), 0f) * 0.35f;   // o jogo de cores anda (fecha o ciclo)
        var px = new float[n * n * 4];
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        var Hh = Vector3.Normalize(L + Vector3.UnitZ);
        var off = new Vector3(seed * 1.3f, seed * 2.9f, 0.7f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float ang = MathF.Atan2(v, u);
                float rr = radius * (1f + 0.025f * (Noise3(new Vector3(MathF.Cos(ang) * 2f, MathF.Sin(ang) * 2f, 0.5f) + off) - 0.5f) * 2f);
                float d = MathF.Sqrt(u * u + v * v);
                float edge = Math.Clamp((rr - d) * n * 0.5f + 0.5f, 0f, 1f);
                if (edge <= 0f) continue;
                float uu = u / rr, vv = v / rr, q = MathF.Min(uu * uu + vv * vv, 0.999f);
                var P = new Vector3(uu, vv, MathF.Sqrt(1f - q));
                // Superfície levemente ondulada (pedra natural polida).
                const float e = 0.03f;
                var sp = P * 2.5f + off;
                float h0 = Fbm3(sp);
                var g = new Vector3(Fbm3(sp + new Vector3(e, 0, 0)) - h0, Fbm3(sp + new Vector3(0, e, 0)) - h0, 0f) / e;
                var N = Vector3.Normalize(P - g * 0.05f);
                float wrap = Math.Clamp((Vector3.Dot(N, L) + 0.6f) / 1.6f, 0f, 1f);          // luz envolvente (translúcida)
                var c = new Vector3(0.86f, 0.87f, 0.92f) * (0.62f + 0.42f * wrap);
                // Jogo de cores: manchas iridescentes; a cor depende da mancha e do ângulo da superfície.
                float patch = Fbm3(P * 3.2f + off * 0.5f + drift);
                float flash = Math.Clamp((patch - 0.38f) * 3.2f, 0f, 1f);
                flash *= 0.6f + 0.4f * MathF.Sin(patch * 40f) * MathF.Sin(patch * 23f + 1f);   // fragmentos de cor
                float hue = (patch * 2.2f + N.X * 0.6f + N.Y * 0.9f + phase) % 1f; if (hue < 0f) hue += 1f;
                c = Vector3.Lerp(c, Hue(hue) * 0.85f + new Vector3(0.30f), Math.Clamp(flash, 0f, 1f) * 0.75f);
                // Brilho polido e reflexo na borda.
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, Hh)), 70f);
                float fres = MathF.Pow(1f - N.Z, 3f);
                c += new Vector3(spec * 1.1f) + new Vector3(0.9f, 0.95f, 1f) * (fres * 0.25f);
                int o = (y * n + x) * 4;
                px[o] = Math.Clamp(c.X, 0f, 1f); px[o + 1] = Math.Clamp(c.Y, 0f, 1f); px[o + 2] = Math.Clamp(c.Z, 0f, 1f);
                px[o + 3] = edge;
            }
        return px;
    }

    /// <summary>
    /// Ilumina uma textura redonda como esfera 3D (normal exata da esfera): luz principal, brilho
    /// especular, reflexo de borda (fresnel) e contraluz. A pintura vira o "albedo".
    /// </summary>
    internal static void SphereShade(float[] px, int n, float radius, Vector3 rimCol)
    {
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        var H = Vector3.Normalize(L + Vector3.UnitZ);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int o = (y * n + x) * 4;
                if (px[o + 3] <= 0.001f) continue;
                float u = ((x + 0.5f) / n * 2f - 1f) / radius, v = ((y + 0.5f) / n * 2f - 1f) / radius;
                float q = u * u + v * v;
                if (q >= 1f) continue;
                var N = new Vector3(u, v, MathF.Sqrt(1f - q));
                float ndl = MathF.Max(0f, Vector3.Dot(N, L));
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(N, H)), 60f) * 0.9f;
                float fres = MathF.Pow(1f - N.Z, 3f);
                float back = MathF.Max(0f, -N.X * 0.4f - N.Y * 0.7f + 0.2f) * fres;
                float diff = 0.30f + 0.85f * ndl;
                for (int k = 0; k < 3; k++)
                {
                    float rc = k == 0 ? rimCol.X : k == 1 ? rimCol.Y : rimCol.Z;
                    px[o + k] = Math.Clamp(px[o + k] * diff + spec + fres * 0.25f + back * rc * 0.9f, 0f, 1f);
                }
            }
    }

    static Vector3 Hue(float h)
    {
        float r = Math.Clamp(MathF.Abs(h * 6f - 3f) - 1f, 0f, 1f), g = Math.Clamp(2f - MathF.Abs(h * 6f - 2f), 0f, 1f), b = Math.Clamp(2f - MathF.Abs(h * 6f - 4f), 0f, 1f);
        return new Vector3(r, g, b);
    }

    /// <summary>Recua o contorno (polígono anti-horário) por uma distância, pelo bissetor de cada vértice.</summary>
    static Vector2[] Inset(Vector2[] p, float d)
    {
        int n = p.Length; var r = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            var a = p[(i + n - 1) % n]; var b = p[i]; var c = p[(i + 1) % n];
            var e1 = Vector2.Normalize(b - a); var e2 = Vector2.Normalize(c - b);
            var n1 = new Vector2(-e1.Y, e1.X); var n2 = new Vector2(-e2.Y, e2.X);   // normais para dentro (anti-horário)
            var bis = n1 + n2; float bl = bis.Length();
            if (bl < 1e-4f) { r[i] = b + n1 * d; continue; }
            bis /= bl;
            float cos = Vector2.Dot(bis, n1);
            r[i] = b + bis * (d / MathF.Max(cos, 0.35f));
        }
        return r;
    }

    /// <summary>Pedra lapidada a partir do contorno da cintura (anti-horário, no plano xy, z para a câmera).</summary>
    /// <summary>
    /// Pedra lapidada para contornos côncavos (ex.: raio): chanfro em dois degraus subindo da cintura até a mesa
    /// (mesa triangulada por "orelhas"), e o fundo espelhado.
    /// </summary>
    internal static Mesh CutConcave(Vector2[] girdle, float inset1 = 0.05f, float inset2 = 0.11f, float crown = 0.22f, float depth = 0.35f)
    {
        float area = 0f; int n = girdle.Length;
        for (int i = 0; i < n; i++) { var a = girdle[i]; var b = girdle[(i + 1) % n]; area += a.X * b.Y - b.X * a.Y; }
        if (area < 0f) { girdle = (Vector2[])girdle.Clone(); Array.Reverse(girdle); }
        var mid = Inset(girdle, inset1); var top = Inset(girdle, inset2);
        var m = new Mesh();
        foreach (var q in girdle) m.V.Add(new Vector3(q, 0f));                  // 0..n-1
        foreach (var q in mid) m.V.Add(new Vector3(q, crown * 0.6f));            // n..2n-1
        foreach (var q in top) m.V.Add(new Vector3(q, crown));                   // 2n..3n-1
        foreach (var q in mid) m.V.Add(new Vector3(q, -depth * 0.6f));           // 3n..4n-1
        foreach (var q in top) m.V.Add(new Vector3(q, -depth));                  // 4n..5n-1
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            m.F.Add((i, j, n + j, 0)); m.F.Add((i, n + j, n + i, 0));
            m.F.Add((n + i, n + j, 2 * n + j, 0)); m.F.Add((n + i, 2 * n + j, 2 * n + i, 0));
            m.F.Add((j, i, 3 * n + i, 0)); m.F.Add((j, 3 * n + i, 3 * n + j, 0));
            m.F.Add((3 * n + j, 3 * n + i, 4 * n + i, 0)); m.F.Add((3 * n + j, 4 * n + i, 4 * n + j, 0));
        }
        // Mesa: corte em orelhas (polígono simples anti-horário).
        var idx = new List<int>(); for (int i = 0; i < n; i++) idx.Add(i);
        int guard = 0;
        while (idx.Count > 3 && guard++ < 500)
        {
            bool cut = false;
            for (int k = 0; k < idx.Count; k++)
            {
                int ia = idx[(k + idx.Count - 1) % idx.Count], ib = idx[k], ic = idx[(k + 1) % idx.Count];
                Vector2 A = top[ia], B = top[ib], C = top[ic];
                if ((B.X - A.X) * (C.Y - A.Y) - (B.Y - A.Y) * (C.X - A.X) <= 0f) continue;
                bool inside = false;
                foreach (int o in idx)
                {
                    if (o == ia || o == ib || o == ic) continue;
                    var P = top[o];
                    float d1 = (B.X - A.X) * (P.Y - A.Y) - (B.Y - A.Y) * (P.X - A.X);
                    float d2 = (C.X - B.X) * (P.Y - B.Y) - (C.Y - B.Y) * (P.X - B.X);
                    float d3 = (A.X - C.X) * (P.Y - C.Y) - (A.Y - C.Y) * (P.X - C.X);
                    if (d1 >= 0 && d2 >= 0 && d3 >= 0) { inside = true; break; }
                }
                if (inside) continue;
                m.F.Add((2 * n + ia, 2 * n + ib, 2 * n + ic, 1));
                idx.RemoveAt(k); cut = true; break;
            }
            if (!cut) break;
        }
        if (idx.Count == 3) m.F.Add((2 * n + idx[0], 2 * n + idx[1], 2 * n + idx[2], 1));
        return m;
    }

    internal static Mesh Cut(Vector2[] girdle, float table = 0.16f, float crown = 0.26f, float depth = 0.70f, bool convex = false)
    {
        var m = new Mesh();
        int n = girdle.Length;
        var c = Vector2.Zero; foreach (var q in girdle) c += q; c /= n;
        // Formas convexas: mesa e facetas-estrela pela escala em direção ao centro (sem dobras nas pontas).
        Vector2[] Scaled(float k) { var r = new Vector2[n]; for (int i = 0; i < n; i++) r[i] = c + (girdle[i] - c) * k; return r; }
        var mid = convex ? Scaled(1f - table * 0.45f) : Inset(girdle, table * 0.45f);
        var top = convex ? Scaled(1f - table) : Inset(girdle, table);
        foreach (var q in girdle) m.V.Add(new Vector3(q, 0f));                   // 0..n-1 cintura
        foreach (var q in top) m.V.Add(new Vector3(q, crown));                   // n..2n-1 mesa
        for (int i = 0; i < n; i++)                                              // 2n..3n-1 facetas-estrela
        {
            var q = (mid[i] + mid[(i + 1) % n]) * 0.5f;
            m.V.Add(new Vector3(q, crown * 0.55f));
        }
        m.V.Add(new Vector3(c, crown)); int tc = m.V.Count - 1;
        m.V.Add(new Vector3(c.X, c.Y - 0.03f, -depth)); int cul = m.V.Count - 1;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n, g0 = i, g1 = j, t0 = n + i, t1 = n + j, s = 2 * n + i;
            m.F.Add((g0, g1, s, 0)); m.F.Add((g0, s, t0, 0)); m.F.Add((g1, t1, s, 0)); m.F.Add((t0, s, t1, 0));
            m.F.Add((t0, t1, tc, 1));
            m.F.Add((g1, g0, cul, 0));
        }
        return m;
    }

    /// <summary>Contorno de gota (corte pera): redondo embaixo, afinando até a ponta em cima. Anti-horário.</summary>
    internal static Vector2[] Pear(int n, float w = 0.56f, float h = 0.56f, float cy = -0.20f, float tip = 1.62f)
    {
        var p = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * MathF.Tau;
            float x = MathF.Sin(a), y = -MathF.Cos(a);
            if (y > 0f) { x *= MathF.Pow(1f - y, 0.9f); y *= tip; }
            p[i] = new Vector2(-x * w, y * h + cy);      // -x: anti-horário com y para cima
        }
        return p;
    }

    /// <summary>Contorno a partir de uma forma 2D (SDF): marcha pela borda em volta do centro.</summary>
    internal static Vector2[] OutlineFromSdf(Func<float, float, float> sd, int n, float scale = 1f)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * MathF.Tau;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            float lo = 0f, hi = 1.5f;
            for (int k = 0; k < 30; k++) { float m = (lo + hi) * 0.5f; if (sd(dir.X * m, dir.Y * m) < 0f) lo = m; else hi = m; }
            pts[i] = dir * lo * scale;
        }
        return pts;
    }

    /// <summary>Rasteriza a pedra girada (yaw em torno de y, pitch em torno de x). Supersampling 2x.</summary>
    // Centro e número de lados da mesa da pedra que está sendo renderizada (para o reflexo interno).
    [ThreadStatic] static Vector2 TableC;
    [ThreadStatic] static float TableSides;

    /// <summary>
    /// Mapa de coordenadas locais: para cada pixel, de que ponto da peça "original" (lista local, mesma ordem de
    /// vértices) ele veio. Saída: (x, y, cobertura) por pixel, linha 0 embaixo. Serve para pintar efeitos que
    /// acompanham a peça em qualquer pose (dobrada, curvada, batendo asas) e ficam só dentro dela.
    /// </summary>
    internal static float[] RenderLocal(Mesh m, List<Vector3> local, int size, float fit = 1f)
    {
        int S = size * 2;
        var lx = new float[S * S]; var ly = new float[S * S]; var cov = new float[S * S]; var zb = new float[S * S];
        Array.Fill(zb, -1e9f);
        float sc = S * 0.5f * fit, cx = S * 0.5f, cy = S * 0.5f;
        foreach (var (ia, ib, ic, kind) in m.F)
        {
            var a = m.V[ia]; var b = m.V[ib]; var c = m.V[ic];
            float ax = cx + a.X * sc, ay = cy + a.Y * sc, bx = cx + b.X * sc, by = cy + b.Y * sc, qx = cx + c.X * sc, qy = cy + c.Y * sc;
            int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, qx)))), x1 = Math.Min(S - 1, (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, qx))));
            int y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, qy)))), y1 = Math.Min(S - 1, (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, qy))));
            float den = (by - qy) * (ax - qx) + (qx - bx) * (ay - qy);
            if (MathF.Abs(den) < 1e-6f) continue;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((by - qy) * (px - qx) + (qx - bx) * (py - qy)) / den;
                    float w1 = ((qy - ay) * (px - qx) + (ax - qx) * (py - qy)) / den;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                    float z = w0 * a.Z + w1 * b.Z + w2 * c.Z;
                    int i = y * S + x;
                    if (z <= zb[i]) continue;
                    zb[i] = z;
                    var o = local[ia] * w0 + local[ib] * w1 + local[ic] * w2;
                    lx[i] = o.X; ly[i] = o.Y; cov[i] = 1f;
                }
        }
        var outp = new float[size * size * 3];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float sx = 0, sy = 0, sa = 0;
                for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                { int i = (y * 2 + dy) * S + x * 2 + dx; if (cov[i] > 0) { sx += lx[i]; sy += ly[i]; sa += 1f; } }
                int o = (y * size + x) * 3;
                if (sa > 0) { outp[o] = sx / sa; outp[o + 1] = sy / sa; }
                outp[o + 2] = sa / 4f;
            }
        return outp;
    }

    internal static float[] Render(Mesh m, float yaw, float pitch, int size, GemMat mat, float fit = 0.92f)
    {
        int S = size * 2;
        var col = new Vector3[S * S]; var alpha = new float[S * S]; var zb = new float[S * S];
        Array.Fill(zb, -1e9f);
        var R = Matrix4x4.CreateRotationX(pitch) * Matrix4x4.CreateRotationY(yaw);
        var P = new Vector3[m.V.Count];
        for (int i = 0; i < P.Length; i++) P[i] = Vector3.Transform(m.V[i], R);
        {
            int nt = 0; var c = Vector2.Zero;
            foreach (var f in m.F) if (f.kind == 1) { nt++; c += new Vector2(P[f.c].X, P[f.c].Y); }
            TableC = nt > 0 ? c / nt : Vector2.Zero; TableSides = Math.Max(8, nt);
        }
        float sc = S * 0.5f * fit, cx = S * 0.5f, cy = S * 0.5f;
        var L = Vector3.Normalize(new Vector3(-0.45f, 0.65f, 0.62f));
        var Hh = Vector3.Normalize(L + Vector3.UnitZ);
        foreach (var (ia, ib, ic, kind) in m.F)
        {
            var a = P[ia]; var b = P[ib]; var c = P[ic];
            var nrm = Vector3.Cross(b - a, c - a); float ln = nrm.Length();
            if (ln < 1e-9f) continue;
            nrm /= ln; if (nrm.Z < 0f) nrm = -nrm;
            // Cor da faceta.
            Vector3 fc;
            if (mat.Matte)
            {
                var bc = kind == 2 ? mat.Alt : mat.Base;
                float ndl = MathF.Max(0f, Vector3.Dot(nrm, L));
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(nrm, Hh)), mat.SpecPow) * mat.Gloss;
                float rim = MathF.Pow(1f - MathF.Abs(nrm.Z), 2f) * MathF.Max(0f, -nrm.X * 0.3f - nrm.Y * 0.7f + 0.3f);
                fc = bc * (mat.Ambient + (1.13f - mat.Ambient) * ndl) + Vector3.Lerp(bc, Vector3.One, 0.5f) * spec + mat.Rim * (rim * 0.9f);
            }
            else if (mat.Metal)
            {
                float ndl = MathF.Max(0f, Vector3.Dot(nrm, L));
                var refl = Vector3.UnitZ - 2f * nrm * nrm.Z;
                float env = MathF.Pow(0.5f + 0.5f * MathF.Sin(refl.X * 4f + refl.Y * 5f + 1f), 3f);
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(nrm, Hh)), 50f);
                float ao = 0.45f + 0.55f * nrm.Z;                          // dobras do metal mais escuras
                fc = (mat.Base * (0.04f + 0.85f * ndl * ndl + 0.75f * env) + Vector3.Lerp(mat.Base, Vector3.One, 0.6f) * (spec * 1.3f)) * ao;
            }
            else if (kind == 1)
            {
                // Mesa: plana, mostra o fundo da pedra com reflexos internos que mudam ao girar.
                float k = 0.5f + 0.5f * MathF.Sin(yaw * 6f + 1f);
                fc = mat.Deep * (0.55f + 0.45f * k);
                if (mat.Fire) fc += Hue((yaw * 0.9f + 0.3f) % 1f + (yaw < 0 ? 1f : 0f)) * (0.15f * k);
            }
            else
            {
                float ndl = MathF.Max(0f, Vector3.Dot(nrm, L));
                var refl = Vector3.UnitZ - 2f * nrm * nrm.Z;
                float env = MathF.Pow(0.5f + 0.5f * MathF.Sin(refl.X * 9f + refl.Y * 7f), 12f);
                float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(nrm, Hh)), 120f);
                // Reflexo colorido pela própria pedra (não branco puro), só nas facetas mais viradas para a luz.
                var tint = Vector3.Lerp(mat.Base, Vector3.One, 0.45f);
                if (mat.Fire)
                {
                    // Cor do "fogo" do diamante muda com a direção do reflexo.
                    float hue = (refl.X * 1.7f + refl.Y * 2.3f + yaw * 0.8f) % 1f; if (hue < 0f) hue += 1f;
                    tint = Hue(hue) * 0.75f + Vector3.One * 0.35f;
                }
                fc = mat.Base * (0.10f + MathF.Pow(0.30f + 0.70f * ndl, 1.8f)) + tint * (env * mat.Sparkle) + tint * (spec * 0.6f);
            }
            float ax = cx + a.X * sc, ay = cy + a.Y * sc, bx = cx + b.X * sc, by = cy + b.Y * sc, qx = cx + c.X * sc, qy = cy + c.Y * sc;
            int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, qx)))), x1 = Math.Min(S - 1, (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, qx))));
            int y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, qy)))), y1 = Math.Min(S - 1, (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, qy))));
            float den = (by - qy) * (ax - qx) + (qx - bx) * (ay - qy);
            if (MathF.Abs(den) < 1e-6f) continue;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((by - qy) * (px - qx) + (qx - bx) * (py - qy)) / den;
                    float w1 = ((qy - ay) * (px - qx) + (ax - qx) * (py - qy)) / den;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                    float z = w0 * a.Z + w1 * b.Z + w2 * c.Z;
                    int i = y * S + x;
                    if (z <= zb[i]) continue;
                    zb[i] = z;
                    var pc = fc;
                    if (kind == 1 && mat.Clear)
                    {
                        // Gelo: mesa lisa com um reflexo suave de céu que corre ao girar.
                        float lx = (px - cx) / sc - TableC.X, ly = (py - cy) / sc - TableC.Y;
                        float band = MathF.Exp(-MathF.Pow((lx * 0.8f + ly * 0.6f + yaw * 0.5f - 0.05f) / 0.08f, 2f));
                        float frost = Noise3(new Vector3(lx * 6f, ly * 6f, 2.7f)) * 0.7f + Noise3(new Vector3(lx * 12f, ly * 12f, 5.1f)) * 0.3f;
                        pc = Vector3.Lerp(mat.Deep, mat.Base, 0.55f) * (0.62f + 0.30f * frost) + Vector3.Lerp(mat.Base, Vector3.One, 0.6f) * band * 0.55f;
                    }
                    else if (kind == 1)
                    {
                        // Mesa: não é plana na aparência. Através dela se vê o pavilhão refletindo:
                        // setores claros e escuros (as "setas" do brilhante) com o fogo colorido.
                        float lx = (px - cx) / sc - TableC.X, ly = (py - cy) / sc - TableC.Y;
                        float ang = MathF.Atan2(ly, lx), rad = MathF.Sqrt(lx * lx + ly * ly);
                        float sect = MathF.Abs(MathF.Sin(ang * TableSides * 0.5f + yaw * 3f));
                        float arrows = MathF.Pow(sect, 3f);
                        float ring = 0.5f + 0.5f * MathF.Cos(rad * 38f - yaw * 4f);
                        float bright = Math.Clamp(0.25f + 0.75f * arrows * (0.6f + 0.4f * ring), 0f, 1f);
                        pc = mat.Deep * 0.55f + Vector3.Lerp(mat.Base, Vector3.One, 0.5f) * bright * 0.85f;
                        if (mat.Fire) pc += Hue(((ang / MathF.Tau + 1f) * 2f + yaw) % 1f) * (0.30f * (1f - arrows));
                        pc *= 0.85f + 0.15f * (1f - Math.Clamp(rad / 0.4f, 0f, 1f));
                    }
                    // Arestas claras entre as facetas (a mesa fica lisa).
                    float edgeW = 1.6f / (S / 256f) * 0.012f;
                    if (mat.Edges && kind == 0 && (w0 < edgeW || w1 < edgeW || w2 < edgeW)) pc = pc * 0.8f + Vector3.Lerp(mat.Base, Vector3.One, 0.35f) * 0.3f;
                    col[i] = pc; alpha[i] = 1f;
                }
        }
        // Reduz 2x (antisserrilhado) para o formato de GemArt (y para cima: linha 0 embaixo).
        var outp = new float[size * size * 4];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector3 s = Vector3.Zero; float a = 0f;
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                    {
                        int i = (y * 2 + dy) * S + x * 2 + dx;
                        s += col[i] * alpha[i]; a += alpha[i];
                    }
                int o = (y * size + x) * 4;
                if (a > 0f) s /= a;
                outp[o] = Math.Clamp(s.X, 0f, 1f); outp[o + 1] = Math.Clamp(s.Y, 0f, 1f); outp[o + 2] = Math.Clamp(s.Z, 0f, 1f);
                outp[o + 3] = a / 4f;
            }
        return outp;
    }
}
