using System;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;

namespace CorrenteRapida;

/// <summary>
/// Tempo de uso de cada Corrente, salvo em BepInEx/config/CorrenteRapida_tempo.txt.
/// Fica só neste PC; serve apenas para a evolução visual dos botões.
/// </summary>
internal sealed class Usage
{
    const int N = 6;
    readonly string _path = Path.Combine(Paths.ConfigPath, "CorrenteRapida_tempo.txt");
    readonly double[] _sec = new double[N];
    bool _dirty;
    float _nextSave;

    public Usage() => Load();

    public double Hours(int i) => _sec[i] / 3600.0;

    public void Add(int i, float seconds, float now)
    {
        if (i < 0 || i >= N || seconds <= 0f || seconds > 5f) return; // ignora saltos (jogo travado/minimizado)
        _sec[i] += seconds;
        _dirty = true;
        if (now >= _nextSave) Save(now);
    }

    public void Save(float now)
    {
        _nextSave = now + 30f;
        if (!_dirty) return;
        _dirty = false;
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Tempo de uso de cada Corrente (segundos). Usado só para a evolução visual dos botões.");
            for (int i = 0; i < N; i++)
                sb.AppendLine($"{Settings.Titles[i]}={_sec[i].ToString("0.0", CultureInfo.InvariantCulture)}   # {Hours(i):0.0} h");
            File.WriteAllText(_path, sb.ToString());
        }
        catch (Exception e) { Plugin.L.LogWarning($"[CorrenteRapida] Não consegui salvar o tempo de uso: {e.Message}"); }
    }

    void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            foreach (var raw in File.ReadAllLines(_path))
            {
                var line = raw;
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                int i = Array.IndexOf(Settings.Titles, line.Substring(0, eq).Trim());
                if (i >= 0 && double.TryParse(line.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    _sec[i] = Math.Max(0, v);
            }
        }
        catch (Exception e) { Plugin.L.LogWarning($"[CorrenteRapida] Não consegui ler o tempo de uso: {e.Message}"); }
    }

    static double[] Thresholds()
    {
        var t = new double[] { 5, 20, 50 };
        try
        {
            var parts = Plugin.S.LevelHours.Value.Split(',', ';');
            for (int k = 0; k < 3 && k < parts.Length; k++)
                if (double.TryParse(parts[k].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) t[k] = v;
        }
        catch { }
        return t;
    }

    /// <summary>0 Bruta, 1 Lapidada, 2 Lustrosa, 3 Lendária.</summary>
    public int Level(int i)
    {
        int forced = Plugin.S.ForceLevel.Value;
        if (forced >= 0) return Math.Min(3, forced);
        var t = Thresholds();
        double h = Hours(i);
        return h >= t[2] ? 3 : h >= t[1] ? 2 : h >= t[0] ? 1 : 0;
    }

    /// <summary>0..1 do caminho até o próximo nível (1 no Lendário).</summary>
    public float Progress(int i)
    {
        int lv = Level(i);
        if (lv >= 3) return 1f;
        var t = Thresholds();
        double from = lv == 0 ? 0 : t[lv - 1], to = t[lv];
        return (float)Math.Clamp((Hours(i) - from) / Math.Max(0.001, to - from), 0, 1);
    }
}
