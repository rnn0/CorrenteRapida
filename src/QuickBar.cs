using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace CorrenteRapida;

/// <summary>
/// Barra com um botão por Corrente. Ao clicar, o mod abre a janela
/// "Trocar de Corrente" do ScarletInterface (se estiver fechada), aperta o
/// "USAR" da Corrente escolhida e fecha a janela de novo.
/// Não envia nada ao servidor por conta própria: só aperta os botões da
/// interface original, então todas as regras (recarga, bloqueio) continuam valendo.
/// </summary>
public class QuickBar : MonoBehaviour
{
    public QuickBar(IntPtr ptr) : base(ptr) { }

    static readonly BarLogic Logic = new();

    void Update() => Logic.Update();
}

internal sealed class BarLogic
{

    enum Status { Unknown, Available, Active, Locked }

    sealed class Card
    {
        public Transform? Root;
        public GameObject? ClickTarget;
        public Button.ButtonClickedEvent? ClickEvent;
        public Status Status;
        public Texture? ArtTex;
        public Rect ArtUv;
    }

    sealed class SlotUI
    {
        public GameObject Go = null!;
        public Image Frame = null!;
        public Image Fill = null!;
        public RawImage Art = null!;
        public TextMeshProUGUI Label = null!;
        public bool HasArt;
        public GemIcon? Gem;
        public bool WasPreview;
    }

    /// <summary>Bola + joia + aura de um elemento, desenhada em camadas (RawImages reaproveitadas).</summary>
    sealed class GemIcon
    {
        public GemStyle Style = null!;
        public Texture2D Atlas = null!;
        public Rect[] Uv = null!, Px = null!;
        public RawImage[] Pool = null!;
        public RectTransform[] Rts = null!;
        public int[] CurTex = null!;
        public bool[] On = null!;
        public readonly List<Spr> Buf = new();
        public float T;
        public bool WasActive, Settled;
        public Status LastStatus = (Status)(-1);
        public int LastLevel = -1;
        public float FlashUntil;
        public Image? ProgressImg;
        public Spr[] Last = null!;          // últimos valores aplicados (só mexe no que mudou)
        public GameObject? Holder, ProgressGo;
        public float NextDraw, LastDim = -1f;
    }

    const int PoolSize = 200;   // Caos e Sangue Lendários chegam a ~145 camadas (+ abate)

    // ───────── Abates: o alvo voa até a joia ativa ─────────
    readonly KillWatcher _killWatch = new();
    sealed class KillFx { public Vector2 From; public int Slot; public float T0; public bool Absorbed; public bool Blast; }
    readonly List<KillFx> _fx = new();
    const int FxPool = 220;
    RawImage[]? _fxImgs; RectTransform[]? _fxRts;
    readonly List<Spr> _fxBuf = new();
    readonly float[] _absorbAt = { -99f, -99f, -99f, -99f, -99f, -99f };
    const float FlashTime = 1.2f;

    readonly Usage _usage = new();
    Vector3 _lastMouse;
    float _lastInput, _lastTick;

    // As texturas são calculadas em segundo plano (leva 1–3 s) para não travar o jogo.
    static volatile GemStyle[]? _styles;
    static volatile AtlasData[]? _atlases, _pendingAtlases;
    static volatile GemStyle[]? _pending;     // estilos novos gerados depois de trocar a aparência no jogo
    static volatile bool _building;
    static BarLogic() => StartBuild(false);

    /// <summary>Gera as artes em segundo plano com o estilo/cor atuais do config.</summary>
    static void StartBuild(bool replace)
    {
        _building = true;
        GemArt.LuzColor = Plugin.S.LuzColor.Value.Trim();
        GemStyle.LuzKillMode = Array.IndexOf(new[] { "fios", "feixe", "cometas", "penas", "runas", "aureola", "morcegos", "constelacao" }, Plugin.S.LuzKill.Value.Trim().ToLowerInvariant()) is int m && m >= 0 ? m : 4;
        // Fios próprios com prioridade baixa: gerar as joias leva alguns segundos de CPU e não deve
        // disputar com o jogo. Até 3 fios (metade dos núcleos), cada um pegando a próxima joia da fila;
        // as mais pesadas primeiro.
        var a = new GemStyle[N];
        var at = new AtlasData[N];
        int[] queue = { GemArt.Gelo, GemArt.Corrompida, GemArt.Tempestade, GemArt.Caos, GemArt.Luz, GemArt.Sangue };
        int next = -1, left = Math.Clamp(Environment.ProcessorCount / 2, 1, 3), failed = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int workers = left;
        for (int w = 0; w < workers; w++)
        {
            new System.Threading.Thread(() =>
            {
                try
                {
                    int q;
                    while ((q = System.Threading.Interlocked.Increment(ref next)) < N)
                    {
                        int i = queue[q];
                        a[i] = GemArt.Build(i);
                        at[i] = BuildAtlasData(a[i]);
                        a[i].FreePixels();   // o atlas já tem tudo; as texturas em float ocupavam ~300 MB
                    }
                }
                catch (Exception e) { System.Threading.Interlocked.Increment(ref failed); Plugin.L.LogWarning($"[CorrenteRapida] Falha ao gerar ícones: {e}"); }
                finally
                {
                    if (System.Threading.Interlocked.Decrement(ref left) == 0)
                    {
                        if (failed == 0)
                        {
                            if (replace) { _pendingAtlases = at; _pending = a; } else { _atlases = at; _styles = a; }
                            Plugin.L.LogInfo($"[CorrenteRapida] Joias geradas em {sw.ElapsedMilliseconds / 1000f:0.0} s ({workers} fios).");
                        }
                        _building = false;
                    }
                }
            }) { IsBackground = true, Priority = System.Threading.ThreadPriority.Lowest, Name = "CorrenteRapida-Joias" + w }.Start();
        }
    }

    enum St { Idle, Opening, Clicking, Settle, Closing }

    const int N = 6;
    static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);

    // Janela original
    Transform? _root;
    readonly Card[] _cards = new Card[N];
    GameObject? _closeBtn;
    bool _panelOpen;
    float _nextScan, _nextCardRefresh;
    bool _dumped;

    // Botão original que abre a janela
    GameObject? _opener;
    float _nextOpenerLookup;
    GameObject? _lastClick;
    float _lastClickTime = -99f;
    bool _hintLogged;

    // Nossa barra
    GameObject? _canvasGo;
    Canvas? _canvas;
    RectTransform? _bar;
    readonly SlotUI[] _slots = new SlotUI[N];
    TMP_FontAsset? _font;
    float _nextFontLookup;

    // Troca em andamento
    St _state = St.Idle;
    int _req = -1;
    bool _closeAfter;
    float _deadline;
    int _frames;
    CanvasGroup? _cg;
    float _cgAlpha = 1f;
    bool _hid;

    int _errors;
    float _ignoreStatusUntil;
    int _pendingActive = -1;
    float _cooldownUntil;

    public void Update()
    {
        try { Tick(); }
        catch (Exception e)
        {
            if (_errors++ < 10) Plugin.L.LogWarning($"[CorrenteRapida] {e}");
            ResetSwitch();
        }
    }

    void Tick()
    {
        float now = Time.unscaledTime;
        EnsureBar();
        if (!Plugin.S.Enabled.Value) { SetBarVisible(false); return; }

        TrackMouse(now);
        TrackRightClick();
        RefreshPanel(now);
        ResolveOpener(now);
        WarmUp(now);
        RunSwitch(now);
        if (_state == St.Idle && _warmStage == 0) Harvest(now);
        UpdateBar(now);
        if (Plugin.S.KillFx.Value && _bar != null && _bar.gameObject.activeInHierarchy && (_fx.Count > 0 || AnyActive())) UpdateKills(now);
        TrackUsage(now);
    }

    // ───────────────────────────── janela original ─────────────────────────────

    void RefreshPanel(float now)
    {
        bool wasOpen = _panelOpen;
        _panelOpen = Alive(_root) && _root!.gameObject.activeInHierarchy;

        // Procurar a janela varre todos os textos da interface (pesado): só quando ainda não a
        // conhecemos, e no máximo a cada 2 s, a não ser que estejamos abrindo ela.
        bool urgent = _state == St.Opening || _warmStage == 1;
        if (!_panelOpen && (urgent || (now >= _nextScan && !Alive(_root))))
        {
            _nextScan = now + 2f;
            if (ScanForPanel()) _panelOpen = true;
        }

        if (_panelOpen && !wasOpen) OnPanelOpened(now);
        if (_panelOpen && now >= _nextCardRefresh)
        {
            _nextCardRefresh = now + 0.5f;
            ReadCards();
        }
    }

    bool ScanForPanel()
    {
        var all = UObject.FindObjectsOfType(Il2CppType.Of<TMP_Text>());
        TMP_Text? title = null;
        foreach (var o in all)
        {
            var t = o.TryCast<TMP_Text>();
            if (t == null) continue;
            if (_font == null && t.font != null) _font = t.font;
            if (Norm(t.text).Contains("TROCAR DE CORRENTE")) { title = t; break; }
        }
        if (title == null) return false;

        Transform cur = title.transform;
        while (cur.parent != null)
        {
            cur = cur.parent;
            if (CountTitles(cur) >= 4) { _root = cur; return true; }
        }
        return false;
    }

    void OnPanelOpened(float now)
    {
        // Aprende qual botão abriu a janela: o último clique feito pouco antes dela aparecer.
        if (_state == St.Idle && _warmStage == 0 && _lastClick != null && Alive(_lastClick) && now - _lastClickTime < 1.5f
            && !IsUnder(_lastClick.transform, _root!))
        {
            string path = PathOf(_lastClick.transform);
            if (path != Plugin.S.OpenerPath.Value)
            {
                Plugin.S.OpenerPath.Value = path;
                Plugin.S.File.Save();
                Plugin.L.LogInfo($"[CorrenteRapida] Botão de abrir aprendido: {path}");
            }
            _opener = _lastClick;
        }

        ReadCards();
        if (Plugin.S.DumpPanel.Value && !_dumped) { _dumped = true; Dump(); }
        LearnPanelLayer();
    }

    // ───────── pré-carregamento invisível ao entrar no servidor ─────────
    // O mod guarda (em arquivo) em qual camada da interface a janela aparece. Na sessão seguinte,
    // deixa essa camada invisível, abre a janela, aprende os botões, fecha e mostra a camada de novo.

    int _warmStage;          // 0 parado, 1 abrindo, 2 fechando
    bool _warmDone;
    float _openerSince = -1f, _warmDeadline;
    int _warmFrames;
    CanvasGroup? _warmCg;
    float _warmAlpha = 1f;

    void LearnPanelLayer()
    {
        if (!Alive(_root) || _root!.parent == null) return;
        var cv = _root.parent.GetComponentInParent<Canvas>();
        if (cv == null) return;
        string path = PathOf(cv.transform);
        if (path == Plugin.S.PanelLayerPath.Value) return;
        Plugin.S.PanelLayerPath.Value = path;
        Plugin.S.File.Save();
        Plugin.L.LogInfo($"[CorrenteRapida] Camada da janela aprendida: {path}");
    }

    void WarmUp(float now)
    {
        if (_warmDone || !Plugin.S.Preload.Value) return;
        switch (_warmStage)
        {
            case 0:
            {
                if (_panelOpen) { _warmDone = true; return; }        // você mesmo já abriu
                if (_state != St.Idle) return;
                if (!OpenerVisible) { _openerSince = -1f; return; }
                if (_openerSince < 0f) { _openerSince = now; return; }
                if (now - _openerSince < 3f) return;                 // espera o HUD terminar de carregar

                string path = Plugin.S.PanelLayerPath.Value;
                var layer = string.IsNullOrEmpty(path) ? null : GameObject.Find(path);
                if (layer == null) { _warmDone = true; return; }     // sem camada conhecida: não arrisca aparecer
                _warmCg = layer.GetComponent<CanvasGroup>() ?? layer.AddComponent<CanvasGroup>();
                _warmAlpha = _warmCg.alpha;
                _warmCg.alpha = 0f;
                _warmStage = 1;
                _warmDeadline = now + 2.5f;
                _nextScan = 0f;
                if (!Click(_opener!)) FinishWarm("não consegui apertar o botão de abrir");
                break;
            }
            case 1:
                if (_panelOpen)
                {
                    // Esconde a própria janela também (caso a camada não esconda tudo).
                    if (Alive(_root))
                    {
                        _warmRootCg = _root!.GetComponent<CanvasGroup>() ?? _root.gameObject.AddComponent<CanvasGroup>();
                        _warmRootAlpha = _warmRootCg.alpha;
                        _warmRootCg.alpha = 0f;
                    }
                    ReadCards();
                    CloseForWarm();
                    _warmStage = 2;
                    _warmDeadline = now + 1.5f;
                    _warmRetry = false;
                }
                else if (now > _warmDeadline) FinishWarm("a janela não abriu a tempo");
                break;
            case 2:
            {
                // Só termina quando a janela realmente fechou (antes reabria sem querer se o fechamento demorasse).
                if (_panelOpen && now < _warmDeadline) return;
                if (_panelOpen && !_warmRetry)
                {
                    _warmRetry = true;
                    _warmDeadline = now + 1.5f;
                    CloseForWarm();
                    return;
                }
                int known = 0;
                foreach (var c in _cards) if (c?.ClickTarget != null && Alive(c.ClickTarget)) known++;
                FinishWarm(_panelOpen ? "a janela não fechou" : null, known);
                break;
            }
        }
    }

    CanvasGroup? _warmRootCg;
    float _warmRootAlpha = 1f;
    bool _warmRetry;

    void CloseForWarm()
    {
        if (_closeBtn != null && Alive(_closeBtn)) Click(_closeBtn);
        else if (_opener != null && Alive(_opener)) Click(_opener);
    }

    void FinishWarm(string? error, int known = 0)
    {
        if (_warmRootCg != null && Alive(_warmRootCg)) _warmRootCg.alpha = _warmRootAlpha;
        _warmRootCg = null;
        if (_warmCg != null && Alive(_warmCg)) _warmCg.alpha = _warmAlpha;
        _warmCg = null;
        _warmStage = 0;
        _warmDone = true;
        if (error != null) Plugin.L.LogWarning($"[CorrenteRapida] Pré-carregamento cancelado: {error}.");
        else Plugin.L.LogInfo($"[CorrenteRapida] Pré-carregamento invisível concluído: {known} botão(ões) conhecido(s).");
    }

    /// <param name="updateStatus">
    /// false = só aprende botões (janela escondida, cujo estado pode estar desatualizado).
    /// </param>
    /// <returns>Quantos botões novos foram aprendidos.</returns>
    int ReadCards(bool updateStatus = true)
    {
        int learned = 0;
        if (!Alive(_root)) return 0;
        var texts = _root!.GetComponentsInChildren<TMP_Text>(true);
        var titleTexts = new TMP_Text?[N];
        foreach (var t in texts)
        {
            if (_font == null && t.font != null) _font = t.font;
            int i = Array.IndexOf(Settings.Titles, Norm(t.text));
            if (i >= 0 && titleTexts[i] == null) titleTexts[i] = t;
        }

        var cardRoots = new List<Transform>();
        for (int i = 0; i < N; i++)
        {
            var card = _cards[i] ??= new Card();
            var newStatus = Status.Unknown;
            card.Root = null;
            if (updateStatus) card.Status = Status.Unknown;
            if (card.ClickTarget != null && !Alive(card.ClickTarget)) card.ClickTarget = null;
            var tt = titleTexts[i];
            if (tt == null) continue;

            // Sobe enquanto o pai contém só esta Corrente: esse é o "cartão".
            Transform cur = tt.transform;
            while (cur.parent != null && cur.parent != _root && CountTitles(cur.parent) == 1) cur = cur.parent;
            card.Root = cur;
            cardRoots.Add(cur);

            TMP_Text? useLabel = null;
            foreach (var t in cur.GetComponentsInChildren<TMP_Text>(true))
            {
                string n = Norm(t.text);
                if (n == "ATIVA" || n.EndsWith(" ATIVA")) newStatus = Status.Active;
                else if (n.Contains("BLOQUEADA")) newStatus = Status.Locked;
                else if (n.Contains("USAR")) { useLabel = t; if (newStatus == Status.Unknown) newStatus = Status.Available; }
            }
            if (updateStatus) card.Status = newStatus;

            if (useLabel != null)
            {
                var b = useLabel.GetComponentInParent<Button>(true);
                // Guarda o botão: quando esta Corrente estiver ATIVA o botão some da janela,
                // mas o que já vimos continua servindo para voltar a ela sem abrir nada.
                var target = b != null && IsUnder(b.transform, cur) ? b.gameObject : useLabel.gameObject;
                if (card.ClickTarget == null || !Alive(card.ClickTarget) || card.ClickTarget.GetInstanceID() != target.GetInstanceID())
                    learned++;
                card.ClickTarget = target;
                // Guarda também a ação do botão: ela continua existindo mesmo se a janela for destruída.
                if (b != null && IsUnder(b.transform, cur) && b.onClick != null) card.ClickEvent = b.onClick;
            }

            if (Plugin.S.UseCardArt.Value && card.ArtTex == null) FindArt(card);
        }

        // Logo depois de uma troca a janela ainda mostra a Corrente antiga como ATIVA.
        // Até o servidor confirmar (ou o prazo passar), mantém a troca que acabamos de fazer.
        if (updateStatus && _pendingActive >= 0)
        {
            var pc = _cards[_pendingActive];
            bool confirmed = pc != null && pc.Status == Status.Active;
            if (confirmed || Time.unscaledTime > _ignoreStatusUntil) _pendingActive = -1;
            if (!confirmed && Time.unscaledTime <= _ignoreStatusUntil) ApplyActive(_pendingActive);
        }

        // Botão de fechar: um botão da janela que não pertence a nenhum cartão.
        _closeBtn = null;
        GameObject? fallback = null;
        foreach (var b in _root.GetComponentsInChildren<Button>(true))
        {
            bool inCard = false;
            foreach (var cr in cardRoots) if (IsUnder(b.transform, cr)) { inCard = true; break; }
            if (inCard) continue;
            string name = b.gameObject.name.ToLowerInvariant();
            string label = "";
            foreach (var t in b.GetComponentsInChildren<TMP_Text>(true)) label += Norm(t.text);
            if (name.Contains("close") || name.Contains("fechar") || name.Contains("exit")
                || label is "X" or "✕" or "×" or "✖")
            { _closeBtn = b.gameObject; break; }
            fallback ??= b.gameObject;
        }
        _closeBtn ??= fallback;
        return learned;
    }

    // ───────── tempo de uso (evolução) ─────────

    void TrackUsage(float now)
    {
        float dt = _lastTick > 0f ? now - _lastTick : 0f;
        _lastTick = now;
        try
        {
            var mp = Input.mousePosition;
            if (mp != _lastMouse || Input.anyKey) _lastInput = now;
            _lastMouse = mp;
        }
        catch { _lastInput = now; }

        if (_bar == null || !_bar.gameObject.activeInHierarchy) return;      // só dentro do jogo
        if (now - _lastInput > Plugin.S.AfkMinutes.Value * 60f) return;      // AFK
        int active = -1;
        for (int i = 0; i < N; i++) if (_cards[i]?.Status == Status.Active) { active = i; break; }
        if (active >= 0) _usage.Add(active, dt, now);
    }

    // ───────── aprender botões pela janela escondida ─────────
    // O ScarletInterface recria os cartões mesmo com a janela fechada. Depois de cada troca
    // o cartão da Corrente que deixamos passa a ter um USAR: lendo a janela escondida,
    // aprendemos esse botão e não precisamos abrir a janela para voltar a ela.

    float _harvestAt = float.MaxValue;

    void ScheduleHarvest() => _harvestAt = Time.unscaledTime + 1.2f;

    void Harvest(float now)
    {
        if (now < _harvestAt || _panelOpen) return;
        _harvestAt = float.MaxValue;
        if (!Alive(_root) && !FindHiddenPanel()) return;
        int learned = ReadCards(updateStatus: false);
        int known = 0;
        foreach (var c in _cards) if (c?.ClickTarget != null && Alive(c.ClickTarget)) known++;
        if (learned > 0)
            Plugin.L.LogInfo($"[CorrenteRapida] Janela escondida lida: {learned} botão(ões) novo(s), {known} conhecido(s).");
    }

    /// <summary>Procura a janela mesmo desativada (busca mais pesada, só depois de uma troca).</summary>
    bool FindHiddenPanel()
    {
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TMP_Text>()))
        {
            var t = o.TryCast<TMP_Text>();
            if (t == null || !Norm(t.text).Contains("TROCAR DE CORRENTE")) continue;
            if (!t.gameObject.scene.IsValid()) continue; // ignora prefabs/arquivos, só objetos da cena
            Transform cur = t.transform;
            while (cur.parent != null)
            {
                cur = cur.parent;
                if (CountTitles(cur) >= 4) { _root = cur; return true; }
            }
        }
        return false;
    }

    void FindArt(Card card)
    {
        float best = 0f;
        foreach (var img in card.Root!.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite == null) continue;
            var r = img.rectTransform.rect;
            float area = r.width * r.height;
            if (area <= best) continue;
            try
            {
                var sp = img.sprite;
                var tex = sp.texture;
                Rect px;
                try { px = sp.textureRect; } catch { px = sp.rect; }
                card.ArtTex = tex;
                card.ArtUv = new Rect(px.x / tex.width, px.y / tex.height, px.width / tex.width, px.height / tex.height);
                best = area;
            }
            catch { }
        }
        foreach (var raw in card.Root!.GetComponentsInChildren<RawImage>(true))
        {
            if (raw.texture == null) continue;
            var r = raw.rectTransform.rect;
            float area = r.width * r.height;
            if (area <= best) continue;
            card.ArtTex = raw.texture;
            card.ArtUv = raw.uvRect;
            best = area;
        }
        if (card.ArtTex == null) return;

        // Recorte quadrado no terço de cima (rosto/mãos do personagem).
        var tx = card.ArtTex;
        var uv = card.ArtUv;
        float pw = uv.width * tx.width, ph = uv.height * tx.height;
        float side = Math.Min(pw, ph) * 0.62f;
        float su = side / tx.width, sv = side / tx.height;
        float cx = uv.x + uv.width * 0.5f, cy = uv.y + uv.height * 0.70f;
        cy = Math.Min(cy, uv.y + uv.height - sv / 2f);
        card.ArtUv = new Rect(cx - su / 2f, cy - sv / 2f, su, sv);
    }

    // ───────────────────────────── botão de abrir ─────────────────────────────

    void TrackMouse(float now)
    {
        if (_state != St.Idle || _warmStage != 0) return;
        bool down;
        try { down = Input.GetMouseButtonDown(0); } catch { return; }
        if (!down) return;

        var es = EventSystem.current;
        if (es == null) return;
        var ped = new PointerEventData(es);
        var mp = Input.mousePosition;
        ped.position = new Vector2(mp.x, mp.y);
        var hits = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
        es.RaycastAll(ped, hits);
        if (hits.Count == 0) return;

        var go = hits[0].gameObject;
        if (go == null) return;
        if (_canvasGo != null && IsUnder(go.transform, _canvasGo.transform)) return;
        var b = go.GetComponentInParent<Button>();
        _lastClick = b != null ? b.gameObject : go;
        _lastClickTime = now;

        // Clique manual no USAR da janela original: registra a troca na hora,
        // sem esperar a janela mostrar a nova Corrente (ela pode fechar antes).
        if (_panelOpen) DetectManualSwitch(go);
    }

    void DetectManualSwitch(GameObject hit)
    {
        for (int i = 0; i < N; i++)
        {
            var c = _cards[i];
            if (c?.ClickTarget == null || !Alive(c.ClickTarget) || c.Status != Status.Available) continue;
            if (!IsUnder(hit.transform, c.ClickTarget.transform)) continue;
            MarkSwitched(i);
            Plugin.L.LogInfo($"[CorrenteRapida] Troca manual detectada: {Settings.Titles[i]}");
            return;
        }
    }

    // O Button do Unity só responde ao botão esquerdo; o direito é tratado aqui.
    void TrackRightClick()
    {
        if (!Plugin.S.RightClick.Value || _bar == null || !_bar.gameObject.activeInHierarchy) return;
        bool down;
        try { down = Input.GetMouseButtonDown(1); } catch { return; }
        if (!down) return;

        var es = EventSystem.current;
        if (es == null) return;
        var ped = new PointerEventData(es);
        var mp = Input.mousePosition;
        ped.position = new Vector2(mp.x, mp.y);
        var hits = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
        es.RaycastAll(ped, hits);
        if (hits.Count == 0) return;
        var go = hits[0].gameObject;
        if (go == null) return;

        for (int i = 0; i < N; i++)
            if (IsUnder(go.transform, _slots[i].Go.transform)) { OnSlotClicked(i); return; }
    }

    void ResolveOpener(float now)
    {
        if (Alive(_opener) || now < _nextOpenerLookup) return;
        _nextOpenerLookup = now + 0.5f;
        string path = Plugin.S.OpenerPath.Value;
        if (string.IsNullOrEmpty(path))
        {
            if (!_hintLogged)
            {
                _hintLogged = true;
                Plugin.L.LogInfo("[CorrenteRapida] Abra a janela 'Trocar de Corrente' uma vez pelo botão normal para o mod aprender onde ele fica.");
            }
            return;
        }
        _opener = GameObject.Find(path);
    }

    bool OpenerVisible => Alive(_opener) && _opener!.activeInHierarchy;

    // ───────────────────────────── troca ─────────────────────────────

    void OnSlotClicked(int i)
    {
        try
        {
            if (_state != St.Idle || _warmStage != 0) return;
            float t0 = Time.unscaledTime;
            if (t0 < _cooldownUntil || i == _pendingActive) return;
            var st = _cards[i]?.Status ?? Status.Unknown;
            if (st == Status.Locked || st == Status.Active) return;

            _req = i;
            float now = Time.unscaledTime;
            var cached = _cards[i];
            if (!_panelOpen && Plugin.S.DirectSwitch.Value && cached != null)
            {
                // 1) A janela fechada continua existindo: aperta o USAR dela sem abrir nada.
                if (cached.ClickTarget != null && Alive(cached.ClickTarget) && ClickButtonOnly(cached.ClickTarget))
                {
                    MarkSwitched(i);
                    Plugin.L.LogInfo($"[CorrenteRapida] Troca direta (botão guardado): {Settings.Titles[i]}");
                    return;
                }
                // 2) A janela foi destruída: executa a ação que o USAR dela tinha.
                if (cached.ClickEvent != null)
                {
                    try
                    {
                        cached.ClickEvent.Invoke();
                        MarkSwitched(i);
                        Plugin.L.LogInfo($"[CorrenteRapida] Troca direta (ação guardada): {Settings.Titles[i]}");
                        return;
                    }
                    catch (Exception e)
                    {
                        cached.ClickEvent = null;
                        Plugin.L.LogWarning($"[CorrenteRapida] Ação guardada de {Settings.Titles[i]} falhou: {e.Message}");
                    }
                }
                Plugin.L.LogInfo($"[CorrenteRapida] Abrindo a janela para {Settings.Titles[i]}: "
                    + (cached.ClickTarget == null ? "botão ainda não visto" : "botão guardado não existe mais")
                    + (cached.ClickEvent == null ? ", sem ação guardada." : "."));
            }

            if (_panelOpen)
            {
                _closeAfter = false;
                _state = St.Clicking;
                _deadline = now + 1f;
            }
            else if (OpenerVisible)
            {
                _closeAfter = true;
                _state = St.Opening;
                _deadline = now + 2f;
                _nextScan = 0f;
                if (Plugin.S.HidePanelDuringSwitch.Value && Alive(_root)) HidePanel();
                if (!Click(_opener!))
                {
                    Plugin.L.LogWarning("[CorrenteRapida] Não consegui apertar o botão de abrir a janela.");
                    ResetSwitch();
                }
            }
            else
            {
                Plugin.L.LogInfo("[CorrenteRapida] Ainda não sei qual botão abre a janela. Abra-a uma vez pelo botão normal.");
            }
        }
        catch (Exception e) { Plugin.L.LogWarning($"[CorrenteRapida] {e}"); ResetSwitch(); }
    }

    void RunSwitch(float now)
    {
        switch (_state)
        {
            case St.Opening:
                if (_panelOpen)
                {
                    if (Plugin.S.HidePanelDuringSwitch.Value && !_hid) HidePanel();
                    ReadCards();
                    _state = St.Clicking;
                    _deadline = now + 1.5f;
                    goto case St.Clicking;
                }
                if (now > _deadline)
                {
                    Plugin.L.LogWarning("[CorrenteRapida] A janela não abriu a tempo.");
                    ResetSwitch();
                }
                break;

            case St.Clicking:
            {
                var card = _cards[_req];
                if (card?.ClickTarget == null || !Alive(card.ClickTarget))
                {
                    // A janela pode levar alguns quadros para montar os cartões.
                    if (now < _deadline) { ReadCards(); break; }
                    Plugin.L.LogWarning($"[CorrenteRapida] Não achei o botão USAR de {Settings.Titles[_req]}.");
                    _state = St.Settle; _frames = 1;
                    break;
                }
                if (Click(card.ClickTarget)) MarkSwitched(_req);
                else Plugin.L.LogWarning("[CorrenteRapida] O botão USAR não respondeu ao clique.");
                _state = St.Settle;
                _frames = 3;
                break;
            }

            case St.Settle:
                if (--_frames > 0) break;
                if (_closeAfter && _panelOpen)
                {
                    if (_closeBtn != null && Alive(_closeBtn)) Click(_closeBtn);
                    else if (OpenerVisible) Click(_opener!);
                }
                _state = St.Closing;
                _frames = 3;
                break;

            case St.Closing:
                if (--_frames > 0) break;
                ResetSwitch();
                break;
        }
    }

    void ApplyActive(int i)
    {
        foreach (var c in _cards) if (c != null && c.Status == Status.Active) c.Status = Status.Available;
        if (_cards[i] != null) _cards[i].Status = Status.Active;
    }

    void MarkSwitched(int i)
    {
        ScheduleHarvest();
        _cooldownUntil = Time.unscaledTime + Math.Max(0.2f, Plugin.S.MinInterval.Value);
        ApplyActive(i);
        _pendingActive = i;
        _ignoreStatusUntil = Time.unscaledTime + 4f;
    }

    void HidePanel()
    {
        if (!Alive(_root)) return;
        _cg = _root!.GetComponent<CanvasGroup>() ?? _root.gameObject.AddComponent<CanvasGroup>();
        _cgAlpha = _cg.alpha;
        _cg.alpha = 0f;
        _hid = true;
    }

    void ResetSwitch()
    {
        if (_hid && _cg != null && Alive(_cg)) _cg.alpha = _cgAlpha;
        _hid = false;
        _cg = null;
        _state = St.Idle;
        _req = -1;
    }

    static bool ClickButtonOnly(GameObject go)
    {
        var b = go.GetComponent<Button>();
        if (b == null) b = go.GetComponentInParent<Button>(true);
        if (b == null || !b.interactable) return false;
        b.onClick.Invoke();
        return true;
    }

    static bool Click(GameObject go)
    {
        var b = go.GetComponent<Button>();
        if (b == null) b = go.GetComponentInParent<Button>(true);
        if (b != null && b.interactable)
        {
            b.onClick.Invoke();
            return true;
        }

        var es = EventSystem.current;
        if (es == null) return false;
        var ped = new PointerEventData(es) { button = PointerEventData.InputButton.Left };
        Transform? t = go.transform;
        for (int k = 0; k < 6 && t != null; k++, t = t.parent)
            if (ExecuteEvents.Execute(t.gameObject, ped, ExecuteEvents.pointerClickHandler)) return true;
        return false;
    }

    // ───────────────────────────── nossa barra ─────────────────────────────

    void EnsureBar()
    {
        if (Alive(_canvasGo)) return;

        _canvasGo = new GameObject("CorrenteRapida_Canvas");
        UObject.DontDestroyOnLoad(_canvasGo);
        _canvas = _canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 60;
        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        _canvasGo.AddComponent<GraphicRaycaster>();

        var barGo = NewRect("Barra", _canvasGo.transform);
        _bar = barGo.GetComponent<RectTransform>();
        _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 0.5f);
        _bar.pivot = new Vector2(0f, 0.5f);

        for (int i = 0; i < N; i++) _slots[i] = MakeSlot(i, barGo.transform);
        LayoutSlots();
        barGo.SetActive(false);
    }

    SlotUI MakeSlot(int i, Transform parent)
    {
        var s = new SlotUI();
        s.Go = NewRect(Settings.Short[i], parent);
        s.Frame = s.Go.AddComponent<Image>();
        s.Frame.color = Hex("#3A2A1A");

        var fillGo = NewRect("Fundo", s.Go.transform);
        Stretch(fillGo, 2f);
        s.Fill = fillGo.AddComponent<Image>();
        s.Fill.color = Hex(Settings.Colors[i]);
        s.Fill.raycastTarget = false;

        var artGo = NewRect("Arte", s.Go.transform);
        Stretch(artGo, 2f);
        s.Art = artGo.AddComponent<RawImage>();
        s.Art.raycastTarget = false;
        s.Art.enabled = false;

        var lblGo = NewRect("Texto", s.Go.transform);
        Stretch(lblGo, 0f);
        s.Label = lblGo.AddComponent<TextMeshProUGUI>();
        s.Label.text = Settings.Short[i];
        s.Label.fontSize = 14;
        s.Label.alignment = TextAlignmentOptions.Center;
        s.Label.color = Color.white;
        s.Label.raycastTarget = false;
        s.Label.enableWordWrapping = false;

        var btn = s.Go.AddComponent<Button>();
        btn.targetGraphic = s.Frame;
        int idx = i;
        btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(() => OnSlotClicked(idx))));
        return s;
    }

    void BuildGem(SlotUI s, int i, GemStyle style)
    {
        var g = new GemIcon { Style = style };
        // Todas as texturas da joia num atlas só: a placa de vídeo desenha as camadas em lote.
        var ad = _atlases![i];
        // O atlas sobe para a placa de vídeo uma vez só; se a barra for recriada (troca de cena), reaproveita.
        if (!Alive(_atlasTex[i]))
        {
            if (ad.Bytes == null) { if (!_building) StartBuild(true); return; }
            _atlasTex[i] = UploadAtlas(ad);
        }
        ad.Bytes = null!;   // já está na placa de vídeo: libera a cópia na memória
        g.Atlas = _atlasTex[i];
        g.Uv = ad.Uv; g.Px = ad.Px;
        g.Pool = new RawImage[PoolSize];
        g.Rts = new RectTransform[PoolSize];
        g.CurTex = new int[PoolSize];
        g.On = new bool[PoolSize];
        g.Last = new Spr[PoolSize];

        // Anel de progresso: Image "preenchida" em círculo, desenhada atrás de tudo.
        var pgo = NewRect("Progresso", s.Go.transform);
        var prt = pgo.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        var pimg = pgo.AddComponent<Image>();
        pimg.sprite = Sprite.Create(g.Atlas, g.Px[style.Progress], new Vector2(0.5f, 0.5f));
        pimg.type = Image.Type.Filled;
        pimg.fillMethod = Image.FillMethod.Radial360;
        pimg.fillOrigin = (int)Image.Origin360.Top;
        pimg.fillClockwise = true;
        pimg.raycastTarget = false;
        g.ProgressImg = pimg;
        g.ProgressGo = pgo;

        // As camadas ficam num Canvas próprio: quando a arte anima, só esse pedaço é refeito,
        // não a interface inteira.
        var holder = NewRect("Arte", s.Go.transform);
        var hrt = holder.GetComponent<RectTransform>();
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
        hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.sizeDelta = Vector2.zero;
        holder.AddComponent<Canvas>();
        g.Holder = holder;
        for (int k = 0; k < PoolSize; k++)
        {
            var go = NewRect("Camada" + k, holder.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.texture = g.Atlas;
            img.enabled = false;
            g.Pool[k] = img;
            g.Rts[k] = rt;
            g.CurTex[k] = -1;
        }
        // Sem quadrado: a moldura fica invisível, mas continua recebendo o clique.
        s.Frame.color = new Color(0f, 0f, 0f, 0f);
        s.Fill.enabled = false;
        s.Art.enabled = false;
        s.Label.enabled = false;
        s.Gem = g;
    }

    /// <summary>
    /// Empacota as texturas de uma joia num atlas (prateleiras, com margem para os mipmaps não vazarem).
    /// </summary>
    /// <summary>Atlas pronto para subir para a placa de vídeo (montado em segundo plano, com os mipmaps).</summary>
    static readonly Texture2D?[] _atlasTex = new Texture2D?[N];

    internal sealed class AtlasData
    {
        public int W, H, Mips;
        public byte[] Bytes = null!;
        public Rect[] Uv = null!, Px = null!;
    }

    /// <summary>
    /// Empacota as texturas de uma joia num atlas (prateleiras, com margem para os mipmaps não vazarem)
    /// e já calcula os mipmaps. Roda fora do fio principal.
    /// </summary>
    internal static AtlasData BuildAtlasData(GemStyle style)
    {
        const int pad = 8, width = 2048, mips = 6;
        int n = style.Tex.Count;
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Array.Sort(order, (a, b) => style.Size[b].CompareTo(style.Size[a]));
        var pos = new (int x, int y)[n];
        int cx = pad, cy = pad, shelf = 0;
        foreach (int i in order)
        {
            int sz = style.Size[i];
            if (cx + sz + pad > width) { cx = pad; cy += shelf + pad; shelf = 0; }
            pos[i] = (cx, cy);
            cx += sz + pad;
            shelf = Math.Max(shelf, sz);
        }
        int h = (cy + shelf + pad + 31) / 32 * 32;
        var d = new AtlasData { W = width, H = h, Mips = mips, Uv = new Rect[n], Px = new Rect[n] };
        int total = 0;
        for (int m = 0; m < mips; m++) total += Math.Max(1, width >> m) * Math.Max(1, h >> m) * 4;
        var bytes = new byte[total];
        static byte B(float v) => (byte)Math.Clamp((int)(v * 255f + 0.5f), 0, 255);
        for (int i = 0; i < n; i++)
        {
            int sz = style.Size[i]; var px = style.Tex[i];
            var (ox, oy) = pos[i];
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    int s0 = (y * sz + x) * 4, o = ((oy + y) * width + ox + x) * 4;
                    bytes[o] = B(px[s0]); bytes[o + 1] = B(px[s0 + 1]); bytes[o + 2] = B(px[s0 + 2]); bytes[o + 3] = B(px[s0 + 3]);
                }
            d.Uv[i] = new Rect(ox / (float)width, oy / (float)h, sz / (float)width, sz / (float)h);
            d.Px[i] = new Rect(ox, oy, sz, sz);
        }
        // Mipmaps (média 2x2, cor pré-multiplicada pelo alfa para as bordas não escurecerem).
        int src = 0, sw = width, sh = h, dst = width * h * 4;
        for (int m = 1; m < mips; m++)
        {
            int w2 = Math.Max(1, sw >> 1), h2 = Math.Max(1, sh >> 1);
            for (int y = 0; y < h2; y++)
                for (int x = 0; x < w2; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int sx = Math.Min(sw - 1, x * 2 + dx), sy2 = Math.Min(sh - 1, y * 2 + dy);
                            int o = src + (sy2 * sw + sx) * 4; float al = bytes[o + 3];
                            r += bytes[o] * al; g += bytes[o + 1] * al; b += bytes[o + 2] * al; a += al;
                        }
                    int q = dst + (y * w2 + x) * 4;
                    if (a > 0f) { bytes[q] = (byte)(r / a); bytes[q + 1] = (byte)(g / a); bytes[q + 2] = (byte)(b / a); }
                    bytes[q + 3] = (byte)(a / 4f);
                }
            src = dst; dst += w2 * h2 * 4; sw = w2; sh = h2;
        }
        d.Bytes = bytes;
        return d;
    }

    /// <summary>Sobe o atlas já pronto (rápido: sem recalcular mipmaps no fio principal).</summary>
    static Texture2D UploadAtlas(AtlasData d)
    {
        var tex = new Texture2D(d.W, d.H, TextureFormat.RGBA32, d.Mips, false);
        tex.filterMode = FilterMode.Trilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.LoadRawTextureData(d.Bytes);
        tex.Apply(false, true);
        return tex;
    }

    static Texture2D MakeTex(float[] px, int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
        tex.filterMode = FilterMode.Trilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        var cols = new Color32[n * n];
        static byte B(float v) => (byte)Math.Clamp((int)(v * 255f + 0.5f), 0, 255);
        for (int i = 0; i < cols.Length; i++)
            cols[i] = new Color32(B(px[i * 4]), B(px[i * 4 + 1]), B(px[i * 4 + 2]), B(px[i * 4 + 3]));
        tex.SetPixels32(cols);
        tex.Apply(true, true);
        return tex;
    }

    void AnimateGem(SlotUI s, int idx, Status st, bool preview = false)
    {
        var g = s.Gem!;
        float now = Time.unscaledTime;
        int level = preview ? Usage.MaxLevel : _usage.Level(idx);
        if (preview) { st = Status.Active; g.Settled = false; }
        else if (g.LastLevel >= 0 && level > g.LastLevel)
        {
            g.FlashUntil = now + FlashTime;
            g.Settled = false;
            var nomes = LevelNames;
            Plugin.L.LogInfo($"[CorrenteRapida] {Settings.Titles[idx]} subiu para {nomes[level]}!");
        }
        if (!preview)
        {
            if (level != g.LastLevel) g.Settled = false;
            g.LastLevel = level;
        }
        float flash = Math.Max(0f, (g.FlashUntil - now) / FlashTime);

        // Anel de progresso.
        if (g.ProgressImg != null)
        {
            bool show = Plugin.S.ShowProgress.Value && level < Usage.MaxLevel && !preview;
            if (g.ProgressImg.enabled != show) g.ProgressImg.enabled = show;
            if (show)
            {
                float inner0 = Plugin.S.ButtonSize.Value - 4f;
                var prt = g.ProgressImg.rectTransform;
                prt.sizeDelta = new Vector2(inner0 * 1.12f, inner0 * 1.12f);
                g.ProgressImg.fillAmount = _usage.Progress(idx);
                g.ProgressImg.color = new Color(1f, 0.86f, 0.5f, st == Status.Active ? 0.85f : 0.45f);
            }
        }
        float target = st == Status.Active ? 1f : 0f;
        float step = Time.unscaledDeltaTime * 6f;
        g.T = g.T < target ? Math.Min(target, g.T + step) : Math.Max(target, g.T - step);
        float e = g.T * g.T * (3f - 2f * g.T); // suaviza começo e fim

        // A Corrente ativa vai para trás das outras, para a aura passar por trás dos vizinhos.
        if (target > 0f && !g.WasActive) s.Go.transform.SetAsFirstSibling();
        g.WasActive = target > 0f;

        // Parada e encaixada: nada se mexe, não precisa redesenhar.
        if (e <= 0f && g.Settled && st == g.LastStatus && flash <= 0f && now - _absorbAt[idx] >= GemStyle.KillAbsorbDur) return;
        g.Settled = e <= 0f && level < GemStyle.Radiante && flash <= 0f && !g.Style.AlwaysAnimate; // Radiante+ e cristais com brilho se mexem sempre
        g.LastStatus = st;

        // Limite de quadros: a joia ativa (ou em transição) anima a ~40 fps, as encaixadas a ~15 fps.
        bool busy = e > 0f || flash > 0f || preview || g.T != target || now - _absorbAt[idx] < GemStyle.KillAbsorbDur;
        float fps = busy ? Plugin.S.ActiveFps.Value : Plugin.S.IdleFps.Value;
        if (fps > 0f)
        {
            if (now < g.NextDraw && g.LastStatus == st) return;
            g.NextDraw = now + 1f / fps;
        }

        g.Style.KillAge = now - _absorbAt[idx];
        g.Style.Frame(e, now, level, flash, g.Buf);
        float inner = Plugin.S.ButtonSize.Value - 4f;
        float dim = st == Status.Locked ? 0.35f : 1f;
        if (dim != g.LastDim) { g.LastDim = dim; for (int q = 0; q < PoolSize; q++) g.Last[q].Tex = -1; }
        int k = 0, used = 0;
        float eps = 0.25f / Math.Max(1f, inner);
        for (; k < g.Buf.Count && used < PoolSize; k++)
        {
            var sp = g.Buf[k];
            if (sp.A < 0.01f) continue;                       // invisível: nem ocupa camada
            int u = used++;
            var img = g.Pool[u];
            var rt = g.Rts[u];
            ref var last = ref g.Last[u];
            if (!g.On[u]) { img.enabled = true; g.On[u] = true; last.Tex = -1; }
            if (g.CurTex[u] != sp.Tex) { img.uvRect = g.Uv[sp.Tex]; g.CurTex[u] = sp.Tex; }
            // Só mexe no que mudou o suficiente para aparecer na tela (1/4 de pixel, 0,4°, 1% de opacidade):
            // cada propriedade alterada obriga o Canvas a refazer a malha.
            bool fresh = last.Tex < 0;
            if (fresh || MathF.Abs(last.X - sp.X) > eps || MathF.Abs(last.Y - sp.Y) > eps)
            { rt.anchoredPosition = new Vector2(sp.X * inner, sp.Y * inner); last.X = sp.X; last.Y = sp.Y; }
            if (fresh || MathF.Abs(last.W - sp.W) > eps || MathF.Abs(last.H - sp.H) > eps)
            { rt.sizeDelta = new Vector2(sp.W * inner, sp.H * inner); last.W = sp.W; last.H = sp.H; }
            if (fresh || MathF.Abs(Mathf.DeltaAngle(last.Rot, sp.Rot)) > 0.4f)
            { rt.localRotation = Quaternion.Euler(0f, 0f, sp.Rot); last.Rot = sp.Rot; }
            if (fresh || MathF.Abs(last.A - sp.A) > 0.01f)
            { img.color = new Color(dim, dim, dim, sp.A); last.A = sp.A; }
            last.Tex = sp.Tex;
        }
        for (k = used; k < PoolSize; k++)
            if (g.On[k]) { g.Pool[k].enabled = false; g.On[k] = false; }
    }

    // ───────── prévia: segurar Alt com o mouse sobre uma bola ─────────

    TextMeshProUGUI? _legend;
    static readonly string[] LevelNames = { "Bruta", "Lapidada", "Brônzea", "Prateada", "Dourada", "Lustrosa", "Radiante", "Lendária" };

    /// <summary>Índice da bola sob o mouse enquanto Alt está pressionado, ou -1.</summary>
    int HoverPreview()
    {
        int idx = -1;
        try
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                var mp = Input.mousePosition;
                var p = new Vector2(mp.x, mp.y);
                for (int i = 0; i < N; i++)
                {
                    var rt = _slots[i].Go.GetComponent<RectTransform>();
                    if (RectTransformUtility.RectangleContainsScreenPoint(rt, p, null)) { idx = i; break; }
                }
            }
        }
        catch { idx = -1; }
        UpdateLegend(idx);
        return idx;
    }

    TextMeshProUGUI? _notice;
    float _noticeUntil = -1f;
    string? _noticeShown;

    /// <summary>Aviso de atualização (vindo do Updater) embaixo da barra por alguns segundos.</summary>
    void ShowUpdateNotice(float now)
    {
        var msg = Updater.Notice;
        if (msg != null && msg != _noticeShown && _font != null && _bar != null)
        {
            _noticeShown = msg;
            _noticeUntil = now + (Updater.CanInstall ? 120f : 25f);   // o convite para instalar fica mais tempo
            if (_notice == null)
            {
                var go = NewRect("Aviso", _bar.transform);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(420f, 22f);
                _notice = go.AddComponent<TextMeshProUGUI>();
                _notice.fontSize = 13;
                _notice.enableWordWrapping = false;
                _notice.raycastTarget = true;
                var nb = go.AddComponent<Button>();
                nb.transition = Selectable.Transition.None;
                nb.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(() =>
                {
                    if (Updater.CanInstall) Updater.InstallPending();
                })));
                _notice.color = new Color(1f, 0.85f, 0.45f, 1f);
                _notice.outlineWidth = 0.2f;
            }
            _notice.font = _font; _notice.fontSharedMaterial = _font.material;
            _notice.text = msg;
        }
        if (_notice == null) return;
        bool show = now < _noticeUntil;
        if (_notice.enabled != show) _notice.enabled = show;
        if (show) _notice.rectTransform.anchoredPosition = new Vector2(0f, -Plugin.S.ButtonSize.Value / 2f - 26f);
    }

    void UpdateLegend(int idx)
    {
        if (_legend == null && _bar != null)
        {
            var go = NewRect("Legenda", _bar.transform);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(320f, 22f);
            _legend = go.AddComponent<TextMeshProUGUI>();
            _legend.fontSize = 13;
            _legend.alignment = TextAlignmentOptions.Center;
            _legend.enableWordWrapping = false;
            _legend.raycastTarget = false;
            _legend.color = new Color(1f, 0.9f, 0.7f, 1f);
            _legend.outlineWidth = 0.2f;
            _legend.enabled = false;
        }
        if (_legend == null) return;
        bool show = idx >= 0 && _font != null;
        if (_legend.enabled != show) _legend.enabled = show;
        if (!show) return;
        if (_legend.font != _font) { _legend.font = _font; _legend.fontSharedMaterial = _font!.material; }

        int lv = _usage.Level(idx);
        string nome = Settings.Titles[idx].Replace("CORRENTE ", "");
        nome = nome.Substring(0, 1) + nome.Substring(1).ToLowerInvariant();
        string txt;
        if (lv >= Usage.MaxLevel) txt = $"{nome} — {LevelNames[Usage.MaxLevel]} ({_usage.Hours(idx):0.0} h)";
        else txt = $"{nome} — {LevelNames[lv]} — {_usage.Hours(idx):0.0} h / {_usage.NextHours(idx):0} h até {LevelNames[lv + 1]}";
        _legend.text = txt;

        float size = Plugin.S.ButtonSize.Value, gap = Gap;
        _legend.rectTransform.anchoredPosition = new Vector2(idx * (size + gap) + size / 2f, -size / 2f - 4f);
        _legend.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Espaço entre os botões: o configurado ou, no automático, o necessário para a aura mais larga
    /// (Lendária ativa) não encostar na joia vizinha encaixada.
    /// </summary>
    static float Gap
    {
        get
        {
            float size = Plugin.S.ButtonSize.Value, gap = Plugin.S.Spacing.Value;
            var st = _styles;
            if (!Plugin.S.AutoSpacing.Value || st == null) return gap;
            float inner = size - 4f, need = 0f;
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                    if (i != j) need = MathF.Max(need, st[i].ExtActive + st[j].ExtIdle);
            return MathF.Max(gap, inner * need - size + 2f);
        }
    }

    bool _laidOutWithStyles;

    void LayoutSlots()
    {
        _laidOutWithStyles = _styles != null;
        float size = Plugin.S.ButtonSize.Value, gap = Gap;
        _bar!.sizeDelta = new Vector2(N * size + (N - 1) * gap, size);
        for (int i = 0; i < N; i++)
        {
            var rt = _slots[i].Go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(i * (size + gap), 0f);
        }
    }

    void UpdateBar(float now)
    {
        bool visible = OpenerVisible;
        SetBarVisible(visible);
        if (!visible) return;

        if (_font == null && now >= _nextFontLookup)
        {
            _nextFontLookup = now + 1f;
            var any = UObject.FindObjectOfType(Il2CppType.Of<TMP_Text>())?.TryCast<TMP_Text>();
            if (any != null && any.font != null) _font = any.font;
        }

        PositionBar();
        ShowUpdateNotice(now);
        UpdateMenu();
        if (_pending != null) SwapStyles();
        int previewIdx = HoverPreview();
        bool builtThisFrame = false;      // monta no máximo uma joia por quadro (evita engasgo)

        for (int i = 0; i < N; i++)
        {
            var s = _slots[i];
            var card = _cards[i];
            var st = card?.Status ?? Status.Unknown;

            if (_font != null && s.Label.font != _font)
            {
                s.Label.font = _font;
                s.Label.fontSharedMaterial = _font.material;
                s.Label.SetAllDirty();
            }

            if (!_laidOutWithStyles && _styles != null) LayoutSlots();
            if (s.Gem == null && _styles != null && !builtThisFrame) { BuildGem(s, i, _styles[i]); builtThisFrame = true; }
            if (s.Gem != null)
            {
                bool prev = i == previewIdx;
                if (prev != s.WasPreview) { s.WasPreview = prev; if (s.Gem != null) s.Gem.Settled = false; }
                AnimateGem(s, i, st, prev);
                continue;
            }

            if (!s.HasArt && card?.ArtTex != null && Plugin.S.UseCardArt.Value)
            {
                s.Art.texture = card.ArtTex;
                s.Art.uvRect = card.ArtUv;
                s.Art.enabled = true;
                s.Label.enabled = false;
                s.HasArt = true;
            }

            s.Frame.color = st switch
            {
                Status.Active => Hex("#F2C14E"),
                Status.Locked => Hex("#222222"),
                _ => Hex("#5A4126"),
            };
            float dim = st == Status.Locked ? 0.3f : 1f;
            var baseCol = Hex(Settings.Colors[i]);
            s.Fill.color = new Color(baseCol.r * dim, baseCol.g * dim, baseCol.b * dim, 1f);
            s.Art.color = new Color(dim, dim, dim, 1f);
            s.Label.color = st == Status.Locked ? new Color(0.5f, 0.5f, 0.5f, 1f) : Color.white;
        }
    }

    bool AnyActive()
    {
        for (int i = 0; i < N; i++) if (_cards[i]?.Status == Status.Active) return true;
        return false;
    }

    void UpdateKills(float now)
    {
        _killWatch.Update();
        var canvasRt = _canvasGo?.GetComponent<RectTransform>();
        if (canvasRt == null) { _killWatch.Kills.Clear(); return; }
        while (_killWatch.Kills.Count > 0)
        {
            var (wp, vblood) = _killWatch.Kills.Dequeue();
            int slot = -1;
            for (int i = 0; i < N; i++) if (_cards[i]?.Status == Status.Active && _slots[i].Gem != null) { slot = i; break; }
            if (slot < 0 || _fx.Count >= (vblood ? 12 : 4)) continue;
            Vector2 screen = new(Screen.width * 0.5f, Screen.height * 0.5f);
            var cam = Camera.main;
            if (cam != null)
            {
                var sp = cam.WorldToScreenPoint(wp);
                if (sp.z < 0f) { sp.x = Screen.width - sp.x; sp.y = 0f; }
                screen = new Vector2(Mathf.Clamp(sp.x, 0f, Screen.width), Mathf.Clamp(sp.y, 0f, Screen.height));
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out var local);
            if (!vblood) { _fx.Add(new KillFx { From = local, Slot = slot, T0 = now }); continue; }
            // V Blood: três levas de energia (de pontos um pouco diferentes do corpo) e uma explosão na joia
            // quando a última chega.
            var st = _slots[slot].Gem!.Style;
            float u0 = Plugin.S.ButtonSize.Value;
            for (int w = 0; w < 3; w++)
                _fx.Add(new KillFx { From = local + new Vector2((w - 1) * u0 * 0.9f, (w == 1 ? 0.5f : -0.2f) * u0), Slot = slot, T0 = now + w * 0.28f });
            _fx.Add(new KillFx { Slot = slot, T0 = now + 2 * 0.28f + st.KillArriveT, Blast = true });
        }
        if (_fx.Count == 0 && _fxImgs == null) return;
        if (_fxImgs == null)
        {
            var root = NewRect("Abates", _canvasGo!.transform);
            var rrt = root.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = Vector2.zero;
            root.AddComponent<Canvas>();
            _fxImgs = new RawImage[FxPool]; _fxRts = new RectTransform[FxPool];
            for (int k = 0; k < FxPool; k++)
            {
                var go = NewRect("Gota" + k, root.transform);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img = go.AddComponent<RawImage>();
                img.raycastTarget = false; img.enabled = false;
                _fxImgs[k] = img; _fxRts[k] = rt;
            }
        }
        float unit = Plugin.S.ButtonSize.Value - 4f;
        int used = 0;
        for (int f = _fx.Count - 1; f >= 0; f--)
        {
            var fx = _fx[f];
            var gem = _slots[fx.Slot].Gem;
            if (gem == null) { _fx.RemoveAt(f); continue; }
            var style = gem.Style;
            float p = now - fx.T0;
            if (p < 0f) continue;
            if (fx.Blast)
            {
                if (p > GemStyle.VBloodBlastDur) { _fx.RemoveAt(f); continue; }
                if (!fx.Absorbed) { fx.Absorbed = true; _absorbAt[fx.Slot] = now; gem.Settled = false; }
                var brt = _slots[fx.Slot].Go.GetComponent<RectTransform>();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, RectTransformUtility.WorldToScreenPoint(null, brt.position), null, out var bto);
                _fxBuf.Clear();
                style.VBloodBlast(p, now, bto.x, bto.y, unit, _fxBuf);
                foreach (var sp in _fxBuf)
                {
                    if (used >= FxPool || sp.A < 0.01f || sp.Tex < 0) continue;
                    var img = _fxImgs[used]; var rt = _fxRts![used]; used++;
                    if (img.texture != gem.Atlas) img.texture = gem.Atlas;
                    img.uvRect = gem.Uv[sp.Tex];
                    rt.anchoredPosition = new Vector2(sp.X, sp.Y);
                    rt.sizeDelta = new Vector2(sp.W, sp.H);
                    rt.localRotation = Quaternion.Euler(0f, 0f, sp.Rot);
                    img.color = new Color(1f, 1f, 1f, sp.A);
                    if (!img.enabled) img.enabled = true;
                }
                continue;
            }
            if (!fx.Absorbed && p >= style.KillArriveT) { fx.Absorbed = true; _absorbAt[fx.Slot] = now; gem.Settled = false; }
            if (p > style.KillDur + 0.1f) { _fx.RemoveAt(f); continue; }
            var srt = _slots[fx.Slot].Go.GetComponent<RectTransform>();
            var sScreen = RectTransformUtility.WorldToScreenPoint(null, srt.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sScreen, null, out var to);
            _fxBuf.Clear();
            style.KillTravel(p, now, fx.From.x, fx.From.y, to.x, to.y, unit, _fxBuf);
            foreach (var sp in _fxBuf)
            {
                if (used >= FxPool || sp.A < 0.01f || sp.Tex < 0) continue;
                var img = _fxImgs[used]; var rt = _fxRts![used]; used++;
                if (img.texture != gem.Atlas) img.texture = gem.Atlas;
                img.uvRect = gem.Uv[sp.Tex];
                rt.anchoredPosition = new Vector2(sp.X, sp.Y);
                rt.sizeDelta = new Vector2(sp.W, sp.H);
                rt.localRotation = Quaternion.Euler(0f, 0f, sp.Rot);
                img.color = new Color(1f, 1f, 1f, sp.A);
                if (!img.enabled) img.enabled = true;
            }
        }
        for (int k = used; k < FxPool; k++) if (_fxImgs[k].enabled) _fxImgs[k].enabled = false;
    }

    // ───────── Menu de aparência (engrenagem ao lado da barra) ─────────
    RectTransform? _gear, _menu;
    TextMeshProUGUI? _optColor, _optStatus;
    static readonly string[] LuzColors = { "Diamante", "TopazioDourado", "Citrino", "Champanhe", "SafiraClara" };
    static readonly string[] LuzColorNames = { "Diamante", "Topázio dourado", "Citrino", "Champanhe", "Safira clara" };

    void UpdateMenu()
    {
        if (_bar == null) return;
        if (_gear == null)
        {
            var gg = NewRect("Aparencia", _bar);
            _gear = gg.GetComponent<RectTransform>();
            _gear.anchorMin = _gear.anchorMax = new Vector2(0f, 0.5f);
            _gear.pivot = new Vector2(0.5f, 0.5f);
            _gear.sizeDelta = new Vector2(20f, 20f);
            var raw = gg.AddComponent<RawImage>();
            raw.texture = GearTexture();
            var b = gg.AddComponent<Button>();
            b.targetGraphic = raw;
            b.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(() => { if (_menu != null) _menu.gameObject.SetActive(!_menu.gameObject.activeSelf); })));

            var mg = NewRect("MenuAparencia", _bar);
            _menu = mg.GetComponent<RectTransform>();
            _menu.anchorMin = _menu.anchorMax = new Vector2(0f, 0.5f);
            _menu.pivot = new Vector2(1f, 0f);
            _menu.sizeDelta = new Vector2(220f, 64f);
            var bg = mg.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.04f, 0.06f, 0.92f);
            _optColor = MenuButton(_menu, 0, () =>
            {
                int k = Array.IndexOf(LuzColors, Plugin.S.LuzColor.Value);
                Plugin.S.LuzColor.Value = LuzColors[(k + 1) % LuzColors.Length];
                Rebuild();
            });
            _optStatus = MenuButton(_menu, 1, () => { });
            mg.SetActive(false);
        }
        float size = Plugin.S.ButtonSize.Value, gap = Gap;
        float right = N * size + (N - 1) * gap;
        _gear.anchoredPosition = new Vector2(right + 18f, 0f);
        _menu!.anchoredPosition = new Vector2(right + 28f, size * 0.5f + 6f);
        if (!_menu.gameObject.activeSelf) return;
        foreach (var t in new[] { _optColor, _optStatus })
            if (t != null && _font != null && t.font != _font) { t.font = _font; t.fontSharedMaterial = _font.material; }
        int ci = Math.Max(0, Array.IndexOf(LuzColors, Plugin.S.LuzColor.Value));
        _optColor!.text = "Cor da Luz: <b>" + LuzColorNames[ci] + "</b>";
        _optStatus!.text = _building ? "<size=85%>Gerando as joias…</size>" : "<size=80%>clique para trocar</size>";
    }

    TextMeshProUGUI MenuButton(RectTransform parent, int row, Action onClick)
    {
        var go = NewRect("Opcao" + row, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-12f, 26f);
        rt.anchoredPosition = new Vector2(0f, -6f - row * 28f);
        var img = go.AddComponent<Image>();
        img.color = row < 1 ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0f);
        var t = NewRect("Texto", go.transform);
        Stretch(t, 4f);
        var tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 14; tmp.color = new Color(1f, 0.92f, 0.78f, 1f);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        if (row < 1)
        {
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(new Action(() => { if (!_building) onClick(); })));
        }
        return tmp;
    }

    void Rebuild()
    {
        if (_building) return;
        Plugin.L.LogInfo($"[CorrenteRapida] Gerando joias: cor da Luz {Plugin.S.LuzColor.Value}.");
        StartBuild(true);
    }

    /// <summary>Troca as joias pelas recém-geradas (no fio principal).</summary>
    void SwapStyles()
    {
        var a = _pending; _pending = null;
        if (a == null) return;
        _fx.Clear();
        if (_fxImgs != null) foreach (var im in _fxImgs) { im.enabled = false; im.texture = null; }
        for (int i = 0; i < N; i++)
        {
            if (Alive(_atlasTex[i])) UObject.Destroy(_atlasTex[i]);
            _atlasTex[i] = null;
            var g = _slots[i]?.Gem;
            if (g == null) continue;
            if (g.Holder != null) UObject.Destroy(g.Holder);
            if (g.ProgressGo != null) UObject.Destroy(g.ProgressGo);
            _slots[i].Gem = null;
        }
        _atlases = _pendingAtlases;
        _styles = a;
        _laidOutWithStyles = false;
    }

    static Texture2D? _gearTex;
    static Texture2D GearTexture()
    {
        if (_gearTex != null) return _gearTex;
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var c = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = MathF.Sqrt(u * u + v * v), a = MathF.Atan2(v, u);
                float teeth = MathF.Abs(MathF.Cos(a * 4f)) > 0.6f ? 0.92f : 0.72f;
                float on = r < teeth && r > 0.32f ? 1f : 0f;
                float edge = Math.Clamp((teeth - r) * n * 0.5f, 0f, 1f) * Math.Clamp((r - 0.32f) * n * 0.5f, 0f, 1f);
                c[y * n + x] = new Color32(235, 215, 180, (byte)(255 * edge * on + 255 * edge * (1 - on) * 0));
            }
        t.SetPixels32(c); t.Apply(); t.hideFlags = HideFlags.HideAndDontSave;
        return _gearTex = t;
    }

    void PositionBar()
    {
        var rt = _opener!.GetComponent<RectTransform>();
        if (rt == null || _canvas == null) return;
        var srcCanvas = _opener.GetComponentInParent<Canvas>();
        Camera? cam = null;
        if (srcCanvas != null)
        {
            var rc = srcCanvas.rootCanvas;
            if (rc.renderMode != RenderMode.ScreenSpaceOverlay) cam = rc.worldCamera;
        }

        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        rt.GetWorldCorners(corners);
        Vector2 top = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        Vector2 bottom = RectTransformUtility.WorldToScreenPoint(cam, corners[3]);
        var screen = new Vector2(top.x, (top.y + bottom.y) * 0.5f);

        float sf = _canvas.scaleFactor <= 0f ? 1f : _canvas.scaleFactor;
        var local = new Vector2((screen.x - Screen.width * 0.5f) / sf, (screen.y - Screen.height * 0.5f) / sf);
        _bar!.anchoredPosition = local + new Vector2(Plugin.S.OffsetX.Value, Plugin.S.OffsetY.Value);
    }

    void SetBarVisible(bool v)
    {
        if (_bar != null && Alive(_bar) && _bar.gameObject.activeSelf != v) _bar.gameObject.SetActive(v);
    }

    // ───────────────────────────── utilidades ─────────────────────────────

    static GameObject NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(GameObject go, float inset)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        float c(int i) => int.Parse(hex.Substring(i, 2), NumberStyles.HexNumber) / 255f;
        return new Color(c(0), c(2), c(4), 1f);
    }

    static string Norm(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = Tags.Replace(s, "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        bool space = false;
        foreach (char ch in s)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(ch)) { space = sb.Length > 0; continue; }
            if (space) { sb.Append(' '); space = false; }
            sb.Append(char.ToUpperInvariant(ch));
        }
        return sb.ToString();
    }

    static int CountTitles(Transform t)
    {
        int n = 0;
        var seen = new bool[N];
        foreach (var x in t.GetComponentsInChildren<TMP_Text>(true))
        {
            int i = Array.IndexOf(Settings.Titles, Norm(x.text));
            if (i >= 0 && !seen[i]) { seen[i] = true; n++; }
        }
        return n;
    }

    static bool IsUnder(Transform t, Transform ancestor)
    {
        for (var c = t; c != null; c = c.parent) if (c == ancestor) return true;
        return false;
    }

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var c = t; c != null; c = c.parent) parts.Insert(0, c.name);
        return "/" + string.Join("/", parts);
    }

    static bool Alive(UObject? o)
    {
        try { return o != null && !o.WasCollected && o.GetInstanceID() != 0 && o; }
        catch { return false; }
    }

    void Dump()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Botão de abrir: {Plugin.S.OpenerPath.Value}");
            sb.AppendLine($"Raiz da janela: {PathOf(_root!)}");
            sb.AppendLine($"Botão de fechar: {(_closeBtn != null ? PathOf(_closeBtn.transform) : "(não achado)")}");
            for (int i = 0; i < N; i++)
            {
                var c = _cards[i];
                sb.AppendLine($"{Settings.Titles[i]}: status={c?.Status}, clique={(c?.ClickTarget != null ? PathOf(c.ClickTarget.transform) : "-")}");
            }
            sb.AppendLine();
            DumpNode(_root!, 0, sb);
            File.WriteAllText(Path.Combine(Paths.ConfigPath, "CorrenteRapida_dump.txt"), sb.ToString());
        }
        catch (Exception e) { Plugin.L.LogWarning($"[CorrenteRapida] dump falhou: {e.Message}"); }
    }

    static void DumpNode(Transform t, int depth, StringBuilder sb)
    {
        if (depth > 25) return;
        sb.Append(' ', depth * 2).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (inativo)").Append(" [");
        foreach (var c in t.GetComponents<Component>()) sb.Append(c.GetIl2CppType().Name).Append(' ');
        sb.Append(']');
        var txt = t.GetComponent<TMP_Text>();
        if (txt != null) sb.Append(" \"").Append(Tags.Replace(txt.text ?? "", "")).Append('"');
        sb.AppendLine();
        for (int i = 0; i < t.childCount; i++) DumpNode(t.GetChild(i), depth + 1, sb);
    }
}
