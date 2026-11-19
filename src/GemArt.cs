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
    public int Gem2, RingSilver, RingGold, GemGlow, Shard, Progress;
    public int M1 = -1, M2 = -1;   // motivos do nível Lendário

    public const int Bruta = 0, Lapidada = 1, Lustrosa = 2, Lendaria = 3;

    /// <summary>
    /// Monta as camadas do quadro. e = 0 encaixada .. 1 solta/ativa; t = segundos;
    /// level = nível de evolução; flash = 1..0 logo depois de subir de nível.
    /// </summary>
    public void Frame(float e, float t, int level, float flash, List<Spr> o)
    {
        o.Clear();
        float lift = 0.30f * e, s = 1f + 0.06f * e;
        float gx = 0f, gy = lift;            // centro da joia
        float cy = lift * 0.5f;              // centro das auras
        float ea = level >= Lustrosa ? Math.Min(1f, e * 1.3f) : e; // aura mais intensa a partir da Lustrosa
        bool on = e > 0.001f;

        if (on && level >= Lendaria) LegendBack(e, t, gx, gy, cy, o);
        if (on) AuraBack(ea, t, gx, gy, cy, o);
        o.Add(new Spr(Ball, 0, 0, 1, 1, 0, 1));
        if (level == Lapidada) o.Add(new Spr(RingSilver, 0, 0, 1, 1, 0, 1));
        else if (level >= Lustrosa) o.Add(new Spr(RingGold, 0, 0, 1, 1, 0, 1));
        if (on)
        {
            o.Add(new Spr(Hole, 0, 0, 1, 1, 0, e));
            o.Add(new Spr(Shadow, 0.05f * e, lift - 0.07f * e, s, s, 0, 0.4f * e));
        }
        o.Add(new Spr(level >= Lapidada ? Gem2 : Gem, gx, gy, s, s, 0, 1));
        if (level >= Lendaria)   // luz interna pulsando, mesmo encaixada
            o.Add(new Spr(GemGlow, gx, gy, s * 1.1f, s * 1.1f, 0, 0.30f + 0.20f * MathF.Sin(t * 2.2f) + 0.2f * e));
        if (on) AuraFront(ea, t, level, o);
        if (on && level >= Lendaria) LegendFront(e, t, cy, o);
        if (flash > 0.001f)
        {
            float g = 1f + (1f - flash) * 1.2f;
            o.Add(new Spr(Halo, 0, cy, 2.4f * g, 2.4f * g, 0, flash));
            o.Add(new Spr(GemGlow, gx, gy, s * (1f + (1f - flash) * 0.6f), s * (1f + (1f - flash) * 0.6f), 0, flash));
        }
    }

    // ───────── nível Lendário: traços dos bosses ─────────

    void LegendBack(float e, float t, float gx, float gy, float cy, List<Spr> o)
    {
        // Segunda camada de aura, maior e mais suave.
        o.Add(new Spr(Halo, 0, cy, 3.0f, 3.0f, 0, 0.35f * e * (0.8f + 0.2f * MathF.Sin(t * 1.3f))));
        switch (Kind)
        {
            case GemArt.Sangue:      // coroa de espinhos de sangue
                o.Add(new Spr(M1, 0, cy, 2.5f, 2.5f, 0, e * (0.85f + 0.15f * MathF.Sin(t * 2f))));
                break;
            case GemArt.Tempestade:  // anéis de runas mecânicas
                o.Add(new Spr(M1, 0, cy, 1.95f, 1.95f, t * 20f, e));
                o.Add(new Spr(M2, 0, cy, 1.55f, 1.55f, -t * 32f, e));
                break;
            case GemArt.Caos:        // asas e chifres de chama roxa
            {
                float flap = 0.92f + 0.08f * MathF.Sin(t * 2.2f);
                float u = t * 6f, f = u - MathF.Floor(u);
                bool odd = ((int)MathF.Floor(u) & 1) == 1;
                o.Add(new Spr(odd ? M2 : M1, 0, cy + 0.05f, 2.9f * flap, 2.9f, 0, e * (1f - f)));
                o.Add(new Spr(odd ? M1 : M2, 0, cy + 0.05f, 2.9f * flap, 2.9f, 0, e * f));
                break;
            }
            case GemArt.Gelo:        // véu espectral da Mulher da Neve
                o.Add(new Spr(M1, 0, cy, 2.7f, 2.7f, 8f * MathF.Sin(t * 0.7f), e * 0.85f));
                o.Add(new Spr(M2, 0, cy, 2.5f, 2.5f, -10f * MathF.Sin(t * 0.9f + 1f), e * 0.75f));
                break;
            case GemArt.Luz:         // asas de luz + rosácea de vitral
            {
                float spread = 0.85f + 0.15f * MathF.Sin(t * 1.6f);
                o.Add(new Spr(M2, gx, gy, 2.9f * spread * e, 2.9f, 0, e));
                o.Add(new Spr(M1, gx, gy, 1.55f, 1.55f, t * 8f, e * 0.9f));
                break;
            }
        }
    }

    void LegendFront(float e, float t, float cy, List<Spr> o)
    {
        switch (Kind)
        {
            case GemArt.Sangue:      // morcegos circulando
                for (int k = 0; k < 5; k++)
                {
                    float ang = t * 0.9f + k * MathF.Tau / 5f;
                    float r = 0.80f + 0.08f * MathF.Sin(t * 1.7f + k);
                    float flap = 0.55f + 0.45f * MathF.Abs(MathF.Sin(t * 9f + k * 1.3f));
                    float tilt = MathF.Cos(ang) * 15f;
                    o.Add(new Spr(M2, MathF.Cos(ang) * r, cy + MathF.Sin(ang) * r * 0.72f, 0.34f, 0.24f * flap, tilt, e));
                }
                break;
            case GemArt.Corrompida:  // serpentes de corrupção enroladas na bola
                for (int sn = 0; sn < 2; sn++)
                {
                    float rise = MathF.Pow(Math.Max(0f, MathF.Sin(t * 0.9f + sn * 2.1f)), 4f);
                    for (int k = 13; k >= 0; k--)
                    {
                        float th = sn * MathF.PI + t * 0.8f - k * 0.22f;
                        float r = 0.58f + 0.05f * MathF.Sin(t * 3f - k * 0.8f) + (k == 0 ? 0.16f * rise : 0f);
                        float x = MathF.Cos(th) * r, y = cy + MathF.Sin(th) * r * 0.85f;
                        if (k == 0)
                        {
                            float sz = 0.27f * (1f + 0.3f * rise);
                            o.Add(new Spr(M2, x, y, sz, sz * 1.3f, th * 180f / MathF.PI, e));
                        }
                        else
                        {
                            float sz = 0.20f - k * 0.010f;
                            o.Add(new Spr(M1, x, y, sz, sz, 0, e));
                        }
                    }
                }
                break;
        }
        // Fragmentos de cristal orbitando (todos os elementos).
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
                o.Add(new Spr(Halo, gx, gy, 2.3f * (1 + 0.07f * pulse), 2.3f * (1 + 0.07f * pulse), 0, e * (0.8f + 0.2f * pulse)));
                o.Add(new Spr(A1, gx, gy, 2.6f, 2.6f, -t * 14f, e * (0.65f - 0.15f * pulse)));
                break;

            case GemArt.Sangue:
                o.Add(new Spr(Halo, 0, cy, 2.1f, 2.1f, 0, e * (0.75f + 0.15f * pulse)));
                o.Add(new Spr(A1, 0, cy, 2.5f, 2.5f, t * 11f, e * 0.9f));
                o.Add(new Spr(A2, 0, cy, 2.3f, 2.3f, -t * 7f, e * 0.8f));
                break;

            case GemArt.Gelo:
                o.Add(new Spr(Halo, 0, cy, 2.2f, 2.2f, 0, e * (0.8f + 0.15f * pulse)));
                o.Add(new Spr(A1, 0, cy, 2.3f, 2.3f, t * 6f, e * 0.85f));
                break;

            case GemArt.Corrompida:
            {
                o.Add(new Spr(Halo, 0, cy, 2.2f, 2.2f, 0, e * 0.7f));
                float w1 = 1f + 0.05f * MathF.Sin(t * 3.1f), w2 = 1f + 0.05f * MathF.Cos(t * 2.3f);
                o.Add(new Spr(A1, 0, cy, 2.0f * w1, 2.0f / w1, t * 20f, e * 0.95f));
                o.Add(new Spr(A2, 0, cy, 1.9f / w2, 1.9f * w2, -t * 27f, e * 0.9f));
                break;
            }

            case GemArt.Caos:
            {
                o.Add(new Spr(Halo, 0, cy, 2.2f, 2.2f, 0, e * (0.7f + 0.2f * pulse)));
                float u = t * 7f;
                int i = (int)MathF.Floor(u) % 3;
                float f = u - MathF.Floor(u);
                int[] fire = { A1, A2, A3 };
                float fl = 1f + 0.03f * MathF.Sin(t * 13f);
                o.Add(new Spr(fire[i], 0, cy + 0.10f, 2.3f, 2.3f * fl, 0, e * (1f - f)));
                o.Add(new Spr(fire[(i + 1) % 3], 0, cy + 0.10f, 2.3f, 2.3f * fl, 0, e * f));
                break;
            }

            case GemArt.Tempestade:
            {
                o.Add(new Spr(Halo, 0, cy, 2.1f, 2.1f, 0, e * (0.7f + 0.3f * MathF.Sin(t * 9f) * MathF.Sin(t * 5.3f))));
                int[] arcs = { A1, A2, A3, A4 };
                for (int j = 0; j < 2; j++)
                {
                    int tick = (int)MathF.Floor(t * 13f + j * 0.5f);
                    if (Hash(tick * 7 + j * 101) > 0.7f) continue;
                    int k = (int)(Hash(tick * 13 + j) * 4f) % 4;
                    float rot = Hash(tick * 17 + j * 3) * 360f;
                    o.Add(new Spr(arcs[k], 0, cy, 2.2f, 2.2f, rot, e * (0.6f + 0.4f * Hash(tick * 29 + j))));
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
            case GemArt.Sangue:  // gotas pingando da bola
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

            case GemArt.Gelo:    // flocos girando em volta e caindo devagar
                for (int k = 0, n = 10 + 4 * more; k < n; k++)
                {
                    float ang = k * MathF.Tau / n + Hash(k) * 0.5f + t * 0.35f;
                    float r = 0.62f + 0.25f * Hash(k + 7) + 0.04f * MathF.Sin(t * 1.3f + k);
                    float d = Frac(t * 0.15f + Hash(k + 3));
                    float sz = 0.13f + 0.08f * Hash(k + 11);
                    o.Add(new Spr(Particle, MathF.Cos(ang) * r, MathF.Sin(ang) * r - d * 0.25f, sz, sz,
                        t * 40f + k * 30f, e * MathF.Sin(MathF.PI * d) * 0.95f));
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
                    o.Add(new Spr(Particle, x0 + 0.06f * MathF.Sin(t * 3f + k), y0 + p * 0.7f, sz, sz, 0, e * a));
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
                Base(g, 0xC98A10, 0xFFE070, 0x7A4A05, Sun, 0xD9560A, 0xFF9A2E, 0x8A3205, 0x4A2304);
                g.Halo = g.Add(S, Halo(0xFFE89A, 0xFF8A1E));
                g.A1 = g.Add(S, Beams());
                g.M1 = g.Add(S, Rosette());
                g.M2 = g.Add(S, LightWings());
                break;

            case Sangue:
                Base(g, 0x3A040C, 0x8E1424, 0x200005, Drop, 0xA8001C, 0xFF5A6E, 0x3A0006, 0x1A0003);
                g.Halo = g.Add(S, Halo(0xFF4A5A, 0x5A0010));
                g.A1 = g.Add(S, Mist(3.1f, 0x3A0006, 0xD01E34));
                g.A2 = g.Add(S, Mist(11.7f, 0x5A0010, 0xFF3A50));
                g.Particle = g.Add(P, Small(P, Drop, 0x8A0010, 0xFF4A5A, 0x2A0004));
                g.M1 = g.Add(S, Crown());
                g.M2 = g.Add(64, Bat());
                break;

            case Gelo:
                Base(g, 0x2F7FC0, 0x8FD0F8, 0x1A4F80, Flake, 0xBFE2FA, 0xFFFFFF, 0x4F86B4, 0x0F2A48);
                g.Halo = g.Add(S, Halo(0xE6F8FF, 0x5FB8F0));
                g.A1 = g.Add(S, Frost());
                g.Particle = g.Add(P, Small(P, Flake, 0xCFE8FF, 0xFFFFFF, 0x7FB0D8));
                g.M1 = g.Add(S, Veil(4.2f));
                g.M2 = g.Add(S, Veil(17.9f));
                break;

            case Corrompida:
                Base(g, 0x9A6AD0, 0xE6CCFF, 0x5A2E8A, Eye, 0x3A0F5A, 0x6E2A9E, 0x16031F, 0x24063A, EyeColor);
                g.Halo = g.Add(S, Halo(0xD8A8FF, 0x5B178F));
                g.A1 = g.Add(S, Liquid(2.3f, 0x4A1078, 0xB070F0));
                g.A2 = g.Add(S, Liquid(9.1f, 0x5E1A90, 0xC890FF));
                g.Particle = g.Add(P, Bubble());
                g.M1 = g.Add(P, SnakeBody());
                g.M2 = g.Add(64, SnakeHead());
                break;

            case Tempestade:
                Base(g, 0x0B1A4E, 0x3456C0, 0x050C28, Bolt, 0xE0A000, 0xFFF080, 0x6A3E00, 0x02081E);
                g.Halo = g.Add(S, Halo(0xA8DCFF, 0x2040C0));
                g.A1 = g.Add(S, Arcs(11)); g.A2 = g.Add(S, Arcs(23)); g.A3 = g.Add(S, Arcs(37)); g.A4 = g.Add(S, Arcs(53));
                g.M1 = g.Add(S, GearRing(24, 18));
                g.M2 = g.Add(S, GearRing(16, 10));
                break;

            case Caos:
                Base(g, 0x24083E, 0x6A2E9E, 0x10021E, Skull, 0x7A2EC4, 0xE2B8FF, 0x2E0A50, 0x0E0218, SkullColor);
                g.Halo = g.Add(S, Halo(0xC27CFF, 0x4A1080));
                g.A1 = g.Add(S, Fire(1.3f)); g.A2 = g.Add(S, Fire(5.9f)); g.A3 = g.Add(S, Fire(12.4f));
                g.Particle = g.Add(P, Ember());
                g.M1 = g.Add(S, DemonWings(2.7f));
                g.M2 = g.Add(S, DemonWings(9.3f));
                break;
        }
        return g;
    }

    delegate float Sd(float x, float y);
    delegate (float r, float g, float b)? GemColorFn(float x, float y, float d);

    static void Base(GemStyle g, int ballDark, int ballLight, int ballRim, Sd shape,
                     int gemDark, int gemLight, int gemEdge, int hole, GemColorFn? custom = null)
    {
        g.Ball = g.Add(S, BallTex(ballDark, ballLight, ballRim));
        g.Hole = g.Add(S, HoleTex(shape, hole, ballLight));
        g.Shadow = g.Add(S, ShadowTex(shape));
        g.Gem = g.Add(S, CrystalTex(shape, gemDark, gemLight, gemEdge, custom));
        g.Gem2 = g.Add(S, CrystalTex(shape, gemDark, gemLight, gemEdge, custom, facets: 12f, polished: true));
        g.RingSilver = g.Add(S, Ring(false));
        g.RingGold = g.Add(S, Ring(true));
        g.GemGlow = g.Add(S, GlowTex(shape, gemLight));
        g.Shard = g.Add(P, CrystalSmall(P, ShardShape, gemDark, gemLight, gemEdge));
        g.Progress = g.Add(S, ProgressRing());
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
        float d = Len(x, y) - 0.15f;
        for (int k = 0; k < 6; k++)
        {
            float a = MathF.PI / 2f + k * MathF.PI / 3f;
            float dx = MathF.Cos(a), dy = MathF.Sin(a);
            d = MathF.Min(d, Seg(x, y, 0, 0, dx * 0.80f, dy * 0.80f) - 0.055f);
            foreach (var (dist, len) in new[] { (0.40f, 0.22f), (0.60f, 0.14f) })
            {
                float bx = dx * dist, by = dy * dist;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float ba = a + sgn * MathF.PI / 4f;
                    d = MathF.Min(d, Seg(x, y, bx, by, bx + MathF.Cos(ba) * len, by + MathF.Sin(ba) * len) - 0.04f);
                }
            }
        }
        return d;
    }

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
    static float[] CrystalTex(Sd shape, int dark, int light, int edge, GemColorFn? custom, float facets = 8f, bool polished = false) => Paint(S, (x, y) =>
    {
        float d = shape(x, y);
        float a = Cover(d, S);
        if (a <= 0f) return default;

        const float h = 0.01f, bevel = 0.13f, slope = 0.85f;
        float gx = shape(x + h, y) - shape(x - h, y);
        float gy = shape(x, y + h) - shape(x, y - h);
        float gl = MathF.Max(Len(gx, gy), 1e-5f);
        float depth = -d;                       // distância até a borda
        bool table = depth > bevel;

        // Normal da faceta: direção do gradiente arredondada para 8 cortes.
        float ang = MathF.Atan2(gy / gl, gx / gl);
        float sector = ang / (MathF.Tau / facets);
        float q = MathF.Round(sector) * (MathF.Tau / facets);
        float nx, ny, nz;
        if (table) { nx = 0f; ny = 0f; nz = 1f; }
        else { nx = MathF.Cos(q) * MathF.Sin(slope); ny = MathF.Sin(q) * MathF.Sin(slope); nz = MathF.Cos(slope); }

        // Luz de cima à esquerda, olhando de frente.
        const float lx = -0.48f, ly = 0.58f, lz = 0.66f;
        float diff = Clamp01(nx * lx + ny * ly + nz * lz);
        float hx = lx, hy = ly, hz = lz + 1f, hl = Len3(hx, hy, hz);
        float spec = MathF.Pow(Clamp01((nx * hx + ny * hy + nz * hz) / hl), 40f);

        // Cor de dentro do cristal: mais clara e "acesa" no centro, mais saturada nas bordas.
        var baseC = Mix(Rgb(dark), Rgb(light), Clamp01(0.35f + depth * 2.2f));
        var cc = custom?.Invoke(x, y, d);
        if (cc.HasValue) baseC = cc.Value;
        float shade = table ? 0.95f + 0.25f * (Fbm(x * 5f + 2f, y * 5f) - 0.5f) : 0.45f + 0.85f * diff;
        var c = (baseC.r * shade, baseC.g * shade, baseC.b * shade);

        // Linhas de corte: entre facetas e na borda da mesa.
        float frac = MathF.Abs(sector - MathF.Round(sector + 0.5f) + 0.5f); // 0 no meio, 0.5 na divisa
        float seam = table ? 0f : Clamp01((frac - 0.44f) / 0.06f);
        float tableEdge = Clamp01(1f - MathF.Abs(depth - bevel) / 0.012f);
        c = Mix(c, Mix(Rgb(light), (1f, 1f, 1f), 0.5f), Math.Max(seam * 0.55f, tableEdge * 0.6f));

        // Contorno escuro, reflexo especular e brilho em estrela.
        float rim = Clamp01(1f - MathF.Abs(d + 0.012f) / 0.016f);
        c = Mix(c, Rgb(edge), rim * 0.9f);
        c = Mix(c, (1f, 1f, 1f), Clamp01(spec * 0.9f));
        float sx = x + 0.20f, sy = y - 0.28f;
        float star = Clamp01(1f - Len(sx, sy) / 0.06f) + Clamp01(1f - MathF.Abs(sx) / 0.012f) * Clamp01(1f - MathF.Abs(sy) / 0.16f)
                   + Clamp01(1f - MathF.Abs(sy) / 0.012f) * Clamp01(1f - MathF.Abs(sx) / 0.16f);
        c = Mix(c, (1f, 1f, 1f), Clamp01(star) * 0.85f);
        if (polished)
        {
            // Lapidação fina: mais brilho nas facetas e um segundo brilho em estrela.
            c = Mix(c, Mix(Rgb(light), (1f, 1f, 1f), 0.4f), Clamp01(spec * 0.5f + diff * 0.15f));
            float s2x = x - 0.22f, s2y = y + 0.18f;
            float star2 = Clamp01(1f - Len(s2x, s2y) / 0.04f) + Clamp01(1f - MathF.Abs(s2x) / 0.01f) * Clamp01(1f - MathF.Abs(s2y) / 0.10f)
                        + Clamp01(1f - MathF.Abs(s2y) / 0.01f) * Clamp01(1f - MathF.Abs(s2x) / 0.10f);
            c = Mix(c, (1f, 1f, 1f), Clamp01(star2) * 0.7f);
            star = Math.Max(star, star2);
        }

        // Cristal: a mesa deixa ver um pouco através; as facetas são mais sólidas.
        float alpha = table ? 0.84f : 0.96f;
        alpha = Math.Max(alpha, Math.Max(rim, Clamp01(star)));
        return (Clamp01(c.Item1), Clamp01(c.Item2), Clamp01(c.Item3), a * alpha);
    });

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
    static float[] Ring(bool gold) => Paint(S, (x, y) =>
    {
        float r = Len(x, y);
        float inner = gold ? 0.86f : 0.89f, outer = 0.99f;
        float d = MathF.Max(inner - r, r - outer);
        float a = Cover(d, S);
        float ang = MathF.Atan2(y, x);
        // Metal: claro em cima à esquerda, escuro embaixo à direita, com faixa brilhante no meio.
        float l = Clamp01(0.5f + 0.5f * (-0.6f * x + 0.8f * y) / Math.Max(r, 0.01f));
        float mid = 1f - MathF.Abs((r - (inner + outer) / 2f) / ((outer - inner) / 2f));
        var dark = gold ? Rgb(0x7A4E0A) : Rgb(0x5E6670);
        var light = gold ? Rgb(0xFFE38A) : Rgb(0xF2F6FA);
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
        float r = Len(x, y);
        float a = Cover(r - 0.92f, P);
        if (a <= 0f) return default;
        var c = Mix(Rgb(0x2A0848), Rgb(0x9A4AE0), Clamp01(0.5f + 0.5f * (-0.5f * x + 0.8f * y)));
        float scale = MathF.Abs(MathF.Sin(x * 9f) * MathF.Sin(y * 9f));
        c = Mix(c, Rgb(0x1A0430), scale * 0.35f);
        float hl = Clamp01(1f - Len(x + 0.3f, y - 0.35f) / 0.3f);
        c = Mix(c, (1f, 0.9f, 1f), hl * 0.5f);
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
        int[] pal = { 0xFFD24A, 0xFF8A2A, 0xFFF2C0, 0xE85A1A, 0x8AC8FF, 0xFFB84A };
        var c = Rgb(pal[((id % pal.Length) + pal.Length) % pal.Length]);
        float aa = 0.80f;
        if (lead < 0.022f) { c = Rgb(0x3A2208); aa = 0.95f; }
        return (c.r, c.g, c.b, aa * Cover(r - 0.95f, S));
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

    static float[] Bubble() => Paint(P, (x, y) =>
    {
        float r = Len(x, y);
        float ring = Clamp01(1f - MathF.Abs(r - 0.72f) / 0.14f);
        float fill = r < 0.72f ? 0.25f : 0f;
        float hl = Clamp01(1f - Len(x + 0.3f, y - 0.3f) / 0.18f);
        var c = Mix(Rgb(0x9A4AE0), (1f, 0.95f, 1f), Math.Max(hl, ring * 0.3f));
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
