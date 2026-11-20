using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace CorrenteRapida;

/// <summary>
/// Atualização pelo GitHub: consulta a última Release de rnn0/CorrenteRapida e, conforme a
/// configuração, só avisa ou baixa a DLL nova (conferindo o SHA-256 publicado junto).
/// A troca vale na próxima vez que o jogo abrir, porque a DLL em uso fica travada pelo Windows.
/// </summary>
internal static class Updater
{
    const string Repo = "rnn0/CorrenteRapida";
    const string Asset = "CorrenteRapida.dll";

    /// <summary>Mensagem para mostrar no jogo (lida pela barra na thread principal).</summary>
    public static volatile string? Notice;

    public static void Start()
    {
        CleanupOld();
        var mode = Plugin.S.UpdateMode.Value.Trim().ToLowerInvariant();
        if (mode.StartsWith("deslig")) return;
        bool auto = mode.StartsWith("auto");
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(8));
                await Check(auto);
            }
            catch (Exception e) { Plugin.L.LogWarning($"[CorrenteRapida] Verificação de atualização falhou: {e.Message}"); }
        });
    }

    static async Task Check(bool auto)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"CorrenteRapida/{Plugin.Version}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest) || !Version.TryParse(Plugin.Version, out var current))
            return;
        if (latest <= current)
        {
            Plugin.L.LogInfo($"[CorrenteRapida] Você está na versão mais recente ({Plugin.Version}).");
            return;
        }

        string? dllUrl = null, shaUrl = null;
        foreach (var a in root.GetProperty("assets").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString();
            var url = a.GetProperty("browser_download_url").GetString();
            if (name == Asset) dllUrl = url;
            else if (name == Asset + ".sha256") shaUrl = url;
        }

        if (!auto || dllUrl == null || shaUrl == null)
        {
            Notice = $"CorrenteRapida {latest} disponível no GitHub";
            Plugin.L.LogInfo($"[CorrenteRapida] Nova versão {latest} disponível: https://github.com/{Repo}/releases/latest");
            return;
        }

        // Baixa e confere a integridade antes de tocar em qualquer arquivo.
        var bytes = await http.GetByteArrayAsync(dllUrl);
        var expected = (await http.GetStringAsync(shaUrl)).Trim().Split(' ', '\t', '\n')[0].ToLowerInvariant();
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (expected.Length != 64 || actual != expected)
        {
            Notice = "Atualização do CorrenteRapida descartada (arquivo não confere)";
            Plugin.L.LogWarning($"[CorrenteRapida] SHA-256 não confere (esperado {expected}, recebido {actual}). Nada foi alterado.");
            return;
        }

        var path = typeof(Plugin).Assembly.Location;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Notice = $"CorrenteRapida {latest} disponível no GitHub";
            return;
        }
        var newPath = path + ".new";
        var oldPath = path + ".old";
        File.WriteAllBytes(newPath, bytes);
        try
        {
            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(path, oldPath);      // o Windows permite renomear a DLL em uso
            File.Move(newPath, path);
            Notice = $"CorrenteRapida {latest} instalada — reinicie o jogo para aplicar";
            Plugin.L.LogInfo($"[CorrenteRapida] Versão {latest} baixada e conferida. Ela será usada na próxima vez que o jogo abrir.");
        }
        catch (Exception e)
        {
            // Volta ao estado anterior se algo falhou no meio.
            try { if (!File.Exists(path) && File.Exists(oldPath)) File.Move(oldPath, path); } catch { }
            try { if (File.Exists(newPath)) File.Delete(newPath); } catch { }
            Notice = $"CorrenteRapida {latest} disponível no GitHub";
            Plugin.L.LogWarning($"[CorrenteRapida] Não consegui trocar a DLL: {e.Message}");
        }
    }

    /// <summary>Apaga a DLL antiga deixada pela última atualização.</summary>
    static void CleanupOld()
    {
        try
        {
            var path = typeof(Plugin).Assembly.Location;
            if (string.IsNullOrEmpty(path)) return;
            foreach (var p in new[] { path + ".old", path + ".new" })
                if (File.Exists(p)) File.Delete(p);
        }
        catch { }
    }
}
