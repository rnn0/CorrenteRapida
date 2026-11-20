using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace CorrenteRapida;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "sangria.correnterapida";
    public const string Name = "CorrenteRapida";
    public const string Version = "2.4.0";

    internal static ManualLogSource L = null!;
    internal static Settings S = null!;

    public override void Load()
    {
        L = Log;
        S = new Settings(Config);
        AddComponent<QuickBar>();
        L.LogInfo($"{Name} {Version} carregado.");
    }
}

internal sealed class Settings
{
    public readonly ConfigFile File;

    // Ordem igual à janela "Trocar de Corrente".
    public static readonly string[] Titles =
    {
        "CORRENTE GLACIAL", "CORRENTE DO CAOS", "CORRENTE DA TEMPESTADE",
        "CORRENTE CORROMPIDA", "CORRENTE LUMINOSA", "CORRENTE SANGUINEA",
    };
    public static readonly string[] Short = { "GL", "CA", "TE", "CO", "LU", "SA" };
    public static readonly string[] Colors = { "#5FA8E8", "#8A3FD1", "#3E6BFF", "#B04BE0", "#E8C25A", "#C8243A" };

    public readonly ConfigEntry<bool> Enabled;
    public readonly ConfigEntry<string> OpenerPath;
    public readonly ConfigEntry<float> OffsetX;
    public readonly ConfigEntry<float> OffsetY;
    public readonly ConfigEntry<float> ButtonSize;
    public readonly ConfigEntry<float> Spacing;
    public readonly ConfigEntry<bool> UseCardArt;
    public readonly ConfigEntry<bool> HidePanelDuringSwitch;
    public readonly ConfigEntry<bool> DumpPanel;
    public readonly ConfigEntry<bool> DirectSwitch;
    public readonly ConfigEntry<float> MinInterval;
    public readonly ConfigEntry<bool> RightClick;
    public readonly ConfigEntry<bool> ShowProgress;
    public readonly ConfigEntry<int> ForceLevel;
    public readonly ConfigEntry<string> LevelHours;
    public readonly ConfigEntry<float> AfkMinutes;
    public readonly ConfigEntry<bool> Preload;
    public readonly ConfigEntry<string> PanelLayerPath;

    public Settings(ConfigFile f)
    {
        File = f;
        Enabled = f.Bind("Geral", "Ativado", true, "Mostra a barra de Correntes.");
        OpenerPath = f.Bind("Geral", "CaminhoBotaoAbrir", "",
            "Caminho do botão que abre 'Trocar de Corrente'. É aprendido sozinho: abra a janela uma vez pelo botão normal. Apague para reaprender.");
        OffsetX = f.Bind("Posicao", "DeslocamentoX", 8f, "Distância horizontal da barra em relação ao botão original.");
        OffsetY = f.Bind("Posicao", "DeslocamentoY", 42f, "Distância vertical da barra em relação ao botão original.");
        ButtonSize = f.Bind("Posicao", "TamanhoBotao", 34f, "Tamanho de cada botão.");
        Spacing = f.Bind("Posicao", "Espacamento", 4f, "Espaço entre os botões.");
        UseCardArt = f.Bind("Visual", "UsarArteDasCartas", true, "Usa um recorte da arte de cada Corrente como ícone (depois que a janela for aberta uma vez).");
        HidePanelDuringSwitch = f.Bind("Visual", "EsconderJanelaNaTroca", true, "Deixa a janela invisível enquanto o mod troca a Corrente.");
        DirectSwitch = f.Bind("Geral", "TrocaDireta", true,
            "Troca sem abrir a janela, apertando o USAR da janela escondida. Se a troca não acontecer de verdade, mude para false.");
        MinInterval = f.Bind("Geral", "IntervaloMinimo", 1.2f,
            "Segundos mínimos entre duas trocas, para o servidor terminar uma antes da outra.");
        RightClick = f.Bind("Geral", "BotaoDireito", true, "Permite trocar a Corrente clicando com o botão direito também.");
        ShowProgress = f.Bind("Evolucao", "MostrarProgresso", true, "Mostra o anel fino de progresso até o próximo nível em volta de cada bola.");
        ForceLevel = f.Bind("Evolucao", "ForcarNivel", -1,
            "Força um nível em todos os botões para testar o visual: 0 Bruta, 1 Lapidada, 2 Brônzea, 3 Prateada, 4 Dourada, 5 Lustrosa, 6 Radiante, 7 Lendária. -1 = usar o tempo real.");
        LevelHours = f.Bind("Evolucao", "HorasNiveis", "1,2,3,4,5,20,50",
            "Horas de uso para Lapidada, Brônzea, Prateada, Dourada, Lustrosa, Radiante e Lendária (7 valores).");
        // Migração de versões antigas: posição e níveis ainda no padrão antigo passam para o novo.
        var cfgVersion = f.Bind("Interno", "VersaoConfig", 1, "Não altere.");
        if (cfgVersion.Value < 2)
        {
            if (OffsetY.Value == 0f) OffsetY.Value = 42f;
            if (LevelHours.Value.Replace(" ", "") == "5,20,50") LevelHours.Value = "1,2,3,4,5,20,50";
            cfgVersion.Value = 2;
            f.Save();
        }
        AfkMinutes = f.Bind("Evolucao", "MinutosAfk", 5f, "Depois de tantos minutos sem mexer o mouse nem apertar teclas, o tempo para de contar.");
        Preload = f.Bind("Geral", "PreCarregarJanela", true,
            "Ao entrar no servidor, abre e fecha a janela de Correntes de forma invisível para aprender os botões (evita a janela aparecer na primeira troca).");
        PanelLayerPath = f.Bind("Geral", "CaminhoCamadaJanela", "",
            "Camada da interface onde a janela aparece. É aprendida sozinha; apague para reaprender.");
        DumpPanel = f.Bind("Debug", "SalvarEstruturaDaJanela", true, "Salva a estrutura da janela em BepInEx/config/CorrenteRapida_dump.txt (útil para diagnosticar).");
    }
}
