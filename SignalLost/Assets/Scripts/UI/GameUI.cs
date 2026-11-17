using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Bouwt de volledige UI in code, in de kleuren van de gekozen radio.
public class GameUI : MonoBehaviour
{
    [Header("Optioneel")]
    [Tooltip("Eigen TextMeshPro-font; leeg = standaard TMP-font")]
    public TMP_FontAsset font;
    [Tooltip("Klein paneel met GPS-info tijdens het spelen (handig voor de uitlegvideo)")]
    public bool showDebugInfo = false;

    [HideInInspector] public GameManager game;

    public SignalMeter Meter { get; private set; }

    private RadioDefinition theme;
    private GameObject canvasGO, menuBackground, staticOverlayGO, debugPanel;
    private readonly List<Image> waveRings = new List<Image>();
    private RectTransform dialNeedle;
    private bool titleStyled;
    private RectTransform canvasRoot, safeRoot, column;
    private CanvasScaler scaler;
    private Vector2Int lastScreenSize;
    private Rect lastSafeArea;

    [Tooltip("Maximale breedte van de inhoud op brede schermen (tablet, pc), in UI-eenheden")]
    public float maxColumnWidth = 1200f;
    private RectTransform menu, setup, shop, help, loading, intro, hud, riddle, fragment, end;
    private RectTransform[] allPanels = new RectTransform[0];

    // Menu
    private TMP_Text menuParts, menuRadio, menuMessage, audioLabel, vibrateLabel, menuTitle;
    private bool legendOwned;
    private readonly List<RectTransform> sparkles = new List<RectTransform>();
    private readonly List<float> sparkleSpeeds = new List<float>();
    // Setup
    private readonly List<(Button button, int value)> lengthButtons = new List<(Button, int)>();
    private readonly List<(Button button, Difficulty value)> difficultyButtons = new List<(Button, Difficulty)>();
    private TMP_Text setupInfo;
    private int selectedLength = 6;
    private int selectedStoryIndex = -1; // -1 = willekeurig
    private TMP_Text storyLabel;
    private Difficulty selectedDifficulty = Difficulty.Medium;
    // Shop
    private RectTransform shopContent;
    private TMP_Text shopParts;
    // Laden
    private TMP_Text loadingTitle, loadingStatus;
    // Intro
    private TMP_Text introTitle, introStory, introInfo;
    private Action introCallback;
    // HUD
    private TMP_Text timerText, mastText, hintText, dangerText;
    private GameObject dangerBanner, tuneButton;
    private Image dangerBannerImage;
    private Button stopButton;
    private float stopConfirmUntil;
    // Raadsel
    private TMP_Text riddleTitle, riddleFrame, riddleQuestion, riddleFeedback;
    private readonly List<Button> answerButtons = new List<Button>();
    private Action<int> answerCallback;
    private bool answersLocked;
    // Fragment
    private TMP_Text fragmentTitle, fragmentText;
    private Action fragmentCallback;
    private Coroutine typewriter;
    private bool typing;
    // Einde
    private TMP_Text endTitle, endStory, endStats;

    // Kleuren
    private Color Bg => theme.background;
    private Color Panel => Color.Lerp(theme.background, Color.white, 0.10f);
    private Color Primary => theme.primary;
    private Color Accent => theme.accent;
    private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f);
    private static readonly Color Muted = new Color(1f, 1f, 1f, 0.6f);
    private static readonly Color Good = new Color(0.25f, 0.8f, 0.35f);
    private static readonly Color Bad = new Color(0.9f, 0.22f, 0.18f);

    // ================================================================ Opbouw

    public void Build(RadioDefinition radio)
    {
        theme = radio;
        if (canvasGO != null) Destroy(canvasGO);
        lengthButtons.Clear();
        difficultyButtons.Clear();
        answerButtons.Clear();
        EnsureEventSystem();

        canvasGO = new GameObject("GameUI Canvas", typeof(RectTransform));
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        canvasGO.AddComponent<GraphicRaycaster>();
        canvasRoot = (RectTransform)canvasGO.transform;

        menuBackground = BuildBackground().gameObject;

        var staticRt = CreateRect("StaticOverlay", canvasRoot);
        Stretch(staticRt);
        var staticImage = staticRt.gameObject.AddComponent<RawImage>();
        staticImage.raycastTarget = false;
        staticImage.color = new Color(1f, 1f, 1f, 0f);
        staticOverlayGO = staticRt.gameObject;

        safeRoot = CreateRect("SafeArea", canvasRoot);

        // Kolom in het midden: op een gsm de volle breedte, op brede schermen begrensd
        column = CreateRect("Column", safeRoot);
        column.anchorMin = new Vector2(0.5f, 0f);
        column.anchorMax = new Vector2(0.5f, 1f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.anchoredPosition = Vector2.zero;
        column.sizeDelta = new Vector2(1080f, 0f);

        UpdateResponsiveLayout(true);

        BuildMenu();
        BuildSetup();
        BuildShop();
        BuildHelp();
        BuildLoading();
        BuildIntro();
        BuildHud(staticImage);
        BuildRiddle();
        BuildFragment();
        BuildEnd();

        allPanels = new[] { menu, setup, shop, help, loading, intro, hud, riddle, fragment, end };
    }

    // Menu-achtergrond: kleurverloop, signaalgolven, scanlijnen en vignet.
    private RectTransform BuildBackground()
    {
        var bg = CreateRect("MenuBackground", canvasRoot);
        Stretch(bg);
        var gradient = bg.gameObject.AddComponent<RawImage>();
        gradient.texture = UIStyle.Gradient(Color.Lerp(Bg, Primary, 0.14f), Color.Lerp(Bg, Color.black, 0.45f));
        gradient.raycastTarget = false;

        var rings = CreateRect("Signaalgolven", bg);
        rings.anchorMin = rings.anchorMax = new Vector2(0.5f, 0.75f);
        rings.sizeDelta = Vector2.zero;
        waveRings.Clear();
        for (int i = 0; i < 5; i++)
        {
            var r = CreateRect("Golf", rings);
            r.sizeDelta = new Vector2(100f, 100f);
            var img = AddImage(r, Primary);
            img.sprite = UIStyle.Ring;
            img.raycastTarget = false;
            waveRings.Add(img);
        }

        var scan = CreateRect("Scanlijnen", bg);
        Stretch(scan);
        var scanImage = scan.gameObject.AddComponent<RawImage>();
        scanImage.texture = UIStyle.Scanlines;
        scanImage.uvRect = new Rect(0f, 0f, 1f, 480f);
        scanImage.raycastTarget = false;

        var vig = CreateRect("Vignet", bg);
        Stretch(vig);
        var vigImage = vig.gameObject.AddComponent<RawImage>();
        vigImage.texture = UIStyle.Vignette;
        vigImage.raycastTarget = false;
        return bg;
    }

    // Afstemschaal met zoekende naald.
    private void BuildDial(RectTransform parent)
    {
        var dial = CreateRect("Afstemschaal", parent);
        SetHeight(dial, 130);
        var img = AddImage(dial, new Color(0f, 0f, 0f, 0.4f));
        MakeRounded(img);
        AddBorder(dial, new Color(Primary.r, Primary.g, Primary.b, 0.35f));

        for (int i = 0; i <= 40; i++)
        {
            float x = Mathf.Lerp(0.06f, 0.94f, i / 40f);
            bool major = i % 5 == 0;
            var tick = CreateRect("Streepje", dial);
            tick.anchorMin = new Vector2(x, 0.52f);
            tick.anchorMax = new Vector2(x, major ? 0.86f : 0.70f);
            tick.sizeDelta = new Vector2(major ? 4f : 2f, 0f);
            AddImage(tick, major ? new Color(1f, 1f, 1f, 0.75f) : new Color(1f, 1f, 1f, 0.35f)).raycastTarget = false;
        }
        for (int f = 88; f <= 108; f += 4)
        {
            float x = Mathf.Lerp(0.06f, 0.94f, (f - 88) / 20f);
            var label = CreateText(dial, f.ToString(), 26, Muted);
            Place(label.rectTransform, new Vector2(x, 0.08f), new Vector2(x, 0.48f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 0f));
        }
        var fm = CreateText(dial, "FM", 24, Primary, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(fm.rectTransform, new Vector2(0f, 0.55f), new Vector2(0.06f, 0.95f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), Vector2.zero);

        dialNeedle = CreateRect("Naald", dial);
        dialNeedle.anchorMin = new Vector2(0.5f, 0.06f);
        dialNeedle.anchorMax = new Vector2(0.5f, 0.94f);
        dialNeedle.sizeDelta = new Vector2(18f, 0f);
        var glow = AddImage(dialNeedle, new Color(Primary.r, Primary.g, Primary.b, 0.25f));
        MakeRounded(glow);
        glow.raycastTarget = false;
        var core = CreateRect("Kern", dialNeedle);
        Stretch(core);
        core.offsetMin = new Vector2(6f, 0f);
        core.offsetMax = new Vector2(-6f, 0f);
        AddImage(core, Primary).raycastTarget = false;
    }

    private void BuildMenu()
    {
        menu = CreateScreen("Menu");
        legendOwned = SaveSystem.OwnsRadio(RadioCatalog.LegendId);
        sparkles.Clear();
        sparkleSpeeds.Clear();
        if (legendOwned) BuildLegendBackdrop(menu);

        Spacer(menu, 60);
        menuTitle = CreateText(menu, "SIGNAL\nLOST", 160, legendOwned ? LegendGold : Primary, TextAlignmentOptions.Center, FontStyles.Bold);
        menuTitle.characterSpacing = 6f;
        titleStyled = false;
        if (legendOwned)
        {
            CreateText(menu, "~ LEGENDARISCHE LUISTERAAR ~", 44, LegendGold, TextAlignmentOptions.Center, FontStyles.Bold);
            CreateText(menu, $"alle frequenties ontcijferd  |  {SaveSystem.StoriesCompleted} verhalen voltooid", 32, Muted);
        }
        else
        {
            CreateText(menu, "een radiojacht in de echte wereld", 40, Muted);
        }
        Spacer(menu, 20);
        BuildDial(menu);
        Spacer(menu, 20);
        menuRadio = CreateText(menu, "", 40, Accent);
        menuParts = CreateText(menu, "", 44, TextColor);
        menuMessage = CreateText(menu, "", 36, Bad);
        Spacer(menu, 20);
        CreateButton(menu, "VERHAAL STARTEN", ShowSetup, Primary, Bg, 160, 58);
        CreateButton(menu, "RADIO'S", ShowShop, Panel, Primary);
        CreateButton(menu, "HOE SPEEL JE?", ShowHelp, Panel, Primary);
        var row = CreateRow(menu, 120, 24);
        audioLabel = Label(CreateButton(row, "", () => { game.ToggleAudio(); RefreshMenu(); }, Panel, Primary, 120, 40));
        vibrateLabel = Label(CreateButton(row, "", () => { game.ToggleVibration(); RefreshMenu(); }, Panel, Primary, 120, 40));
        FlexSpacer(menu);
        CreateText(menu, "Mobile Development  |  AR Geolocation", 28, Muted);
    }

    private static readonly Color LegendGold = new Color(1f, 0.85f, 0.35f);

    // Gouden startscherm voor wie de Station Echo Zender heeft.
    private void BuildLegendBackdrop(RectTransform parent)
    {
        var backdrop = CreateRect("LegendBackdrop", parent);
        var le = backdrop.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
        Stretch(backdrop);

        // Gouden gloed bovenaan
        var glow = CreateRect("Gloed", backdrop);
        Place(glow, new Vector2(0f, 0.55f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        var glowImage = AddImage(glow, new Color(1f, 0.75f, 0.2f, 0.12f));
        glowImage.raycastTarget = false;

        // Glinsters (kleine gedraaide vierkantjes)
        for (int i = 0; i < 28; i++)
        {
            var sp = CreateRect("Glinster", backdrop);
            float size = UnityEngine.Random.Range(8f, 22f);
            sp.anchorMin = sp.anchorMax = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
            sp.sizeDelta = new Vector2(size, size);
            sp.localEulerAngles = new Vector3(0f, 0f, 45f);
            var img = AddImage(sp, new Color(1f, 0.9f, 0.5f, UnityEngine.Random.Range(0.2f, 0.8f)));
            img.raycastTarget = false;
            sparkles.Add(sp);
            sparkleSpeeds.Add(UnityEngine.Random.Range(0.01f, 0.04f));
        }
    }

    private void AnimateLegendMenu()
    {
        if (!legendOwned || menu == null || !menu.gameObject.activeSelf) return;

        if (menuTitle != null)
            menuTitle.color = Color.Lerp(LegendGold, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 2f));

        for (int i = 0; i < sparkles.Count; i++)
        {
            var sp = sparkles[i];
            Vector2 a = sp.anchorMin;
            a.y += sparkleSpeeds[i] * Time.deltaTime;
            if (a.y > 1.05f) { a.y = -0.05f; a.x = UnityEngine.Random.value; }
            sp.anchorMin = sp.anchorMax = a;
            float twinkle = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f + i);
            var img = sp.GetComponent<Image>();
            if (img != null) { var c = img.color; c.a = 0.2f + 0.6f * twinkle; img.color = c; }
        }
    }

    private void BuildSetup()
    {
        setup = CreateScreen("Setup");
        CreateText(setup, "NIEUW VERHAAL", 80, Primary, TextAlignmentOptions.Center, FontStyles.Bold);
        Spacer(setup, 20);

        CreateText(setup, "Verhaal", 44, TextColor);
        var storyRow = CreateRow(setup, 130, 16);
        storyRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
        var prev = CreateButton(storyRow, "<", () => CycleStory(-1), Panel, Primary, 130, 60);
        var mid = CreateButton(storyRow, "", () => CycleStory(1), Primary, Bg, 130, 44);
        var next = CreateButton(storyRow, ">", () => CycleStory(1), Panel, Primary, 130, 60);
        SetWidth(prev, 130f, 0f);
        SetWidth(mid, 300f, 1f);
        SetWidth(next, 130f, 0f);
        storyLabel = Label(mid);

        Spacer(setup, 20);
        CreateText(setup, "Aantal zendmasten", 44, TextColor);
        var lengthRow = CreateRow(setup, 140, 20);
        foreach (int n in new[] { 4, 6, 8, 10 })
        {
            int value = n;
            var b = CreateButton(lengthRow, value.ToString(), () => { selectedLength = value; RefreshSetup(); }, Panel, Primary, 140, 60);
            lengthButtons.Add((b, value));
        }

        Spacer(setup, 30);
        CreateText(setup, "Moeilijkheid", 44, TextColor);
        var diffRow = CreateRow(setup, 140, 20);
        AddDifficultyButton(diffRow, "MAKKELIJK", Difficulty.Easy);
        AddDifficultyButton(diffRow, "GEMIDDELD", Difficulty.Medium);
        AddDifficultyButton(diffRow, "MOEILIJK", Difficulty.Hard);

        Spacer(setup, 30);
        setupInfo = CreateText(setup, "", 38, Muted);
        FitText(setupInfo, 22f, 120f);
        CreateButton(setup, "START", () => game.StartStory(selectedLength, selectedDifficulty, SelectedStoryId), Primary, Bg, 160, 60);
        CreateButton(setup, "TERUG", () => ShowMenu(), Panel, Primary);
    }

    private void CycleStory(int direction)
    {
        int count = StoryData.All.Count;
        selectedStoryIndex += direction;
        if (selectedStoryIndex >= count) selectedStoryIndex = -1;
        if (selectedStoryIndex < -1) selectedStoryIndex = count - 1;
        RefreshSetup();
    }

    private static void SetWidth(Button b, float preferred, float flexible)
    {
        var le = b.GetComponent<LayoutElement>();
        le.preferredWidth = preferred;
        le.minWidth = Mathf.Min(preferred, 100f);
        le.flexibleWidth = flexible;
    }

    private void AddDifficultyButton(RectTransform row, string label, Difficulty d)
    {
        var b = CreateButton(row, label, () => { selectedDifficulty = d; RefreshSetup(); }, Panel, Primary, 140, 40);
        difficultyButtons.Add((b, d));
    }

    private void BuildShop()
    {
        shop = CreateScreen("Shop");
        CreateText(shop, "RADIO'S", 80, Primary, TextAlignmentOptions.Center, FontStyles.Bold);
        shopParts = CreateText(shop, "", 44, TextColor);

        var scrollRt = CreateRect("Scroll", shop);
        var scrollLe = scrollRt.gameObject.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1f;
        scrollLe.minHeight = 400f;
        var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 30f;

        var viewport = CreateRect("Viewport", scrollRt);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        AddImage(viewport, new Color(0f, 0f, 0f, 0f)); // vangt sleepbewegingen op

        shopContent = CreateRect("Content", viewport);
        shopContent.anchorMin = new Vector2(0f, 1f);
        shopContent.anchorMax = new Vector2(1f, 1f);
        shopContent.pivot = new Vector2(0.5f, 1f);
        shopContent.offsetMin = Vector2.zero;
        shopContent.offsetMax = Vector2.zero;
        AddVLayout(shopContent, 24, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
        var fitter = shopContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = shopContent;

        CreateButton(shop, "TERUG", () => ShowMenu(), Panel, Primary);
    }

    private void BuildHelp()
    {
        help = CreateScreen("Help");
        CreateText(help, "HOE SPEEL JE?", 80, Primary, TextAlignmentOptions.Center, FontStyles.Bold);
        Spacer(help, 20);
        var helpBody = CreateText(help,
            "- Kies een lengte en moeilijkheid. De zendmasten komen bij echte plekken in je buurt (OpenStreetMap).\n\n" +
            "- Er is geen pijl: kijk en luister naar je radio.\n" +
            "   Veel ruis = verkeerde kant op.\n   Minder ruis = je komt dichterbij.\n   Geen ruis = je bent er.\n" +
            "   Hoe sneller en hoger de piepjes, hoe dichter je bent.\n\n" +
            "- Dichtbij verschijnt de mast in AR. Tik erop (of op AFSTEMMEN) en los het raadsel op.\n\n" +
            "- Rode glitch-pilaren zijn storingsvelden: erin lopen kost tijd en je verliest je signaal.\n\n" +
            "- Haal alle fragmenten voor de klok op nul staat en verdien radio-onderdelen voor nieuwe radio's.\n\n" +
            "Let op het verkeer en kijk regelmatig op van je scherm!",
            38, TextColor, TextAlignmentOptions.TopLeft);
        FitText(helpBody, 20f, 300f);
        CreateButton(help, "TERUG", () => ShowMenu(), Panel, Primary);
    }

    private void BuildLoading()
    {
        loading = CreateScreen("Loading");
        FlexSpacer(loading);
        loadingTitle = CreateText(loading, "AFSTEMMEN", 90, Primary, TextAlignmentOptions.Center, FontStyles.Bold);
        loadingStatus = CreateText(loading, "", 40, Muted);
        FlexSpacer(loading);
    }

    private void BuildIntro()
    {
        intro = CreateScreen("Intro");
        introTitle = CreateText(intro, "", 76, Primary, TextAlignmentOptions.Center, FontStyles.Bold);
        Spacer(intro, 20);
        introStory = CreateText(intro, "", 42, TextColor, TextAlignmentOptions.TopLeft, FontStyles.Italic);
        FitText(introStory, 24f, 250f);
        Spacer(intro, 20);
        introInfo = CreateText(intro, "", 36, Accent);
        Spacer(intro, 10);
        CreateText(intro, "Houd je gsm rechtop voor je als je op BEGIN drukt (kompas-kalibratie).\nLet op het verkeer!", 32, Muted);
        CreateButton(intro, "BEGIN", () => introCallback?.Invoke(), Primary, Bg, 160, 60);
    }

    private void BuildHud(RawImage staticImage)
    {
        hud = CreateRect("HUD", column);
        Stretch(hud);
        hud.gameObject.SetActive(false);

        // --- Bovenbalk: klok, mastteller, hint ---
        var top = CreateRect("TopBar", hud);
        Place(top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, 196f));
        MakeRounded(AddImage(top, new Color(0f, 0f, 0f, 0.55f)));
        AddBorder(top, new Color(Primary.r, Primary.g, Primary.b, 0.3f));
        timerText = CreateText(top, "00:00", 96, TextColor, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(timerText.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(-40f, 0f));
        mastText = CreateText(top, "", 46, Primary, TextAlignmentOptions.Right, FontStyles.Bold);
        Place(mastText.rectTransform, new Vector2(0.4f, 0.5f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(-40f, 0f));
        hintText = CreateText(top, "", 30, Muted, TextAlignmentOptions.Right);
        Place(hintText.rectTransform, new Vector2(0.3f, 0f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(-40f, 0f));

        // --- Waarschuwing storingsveld ---
        var banner = CreateRect("DangerBanner", hud);
        Place(banner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -225f), new Vector2(-80f, 100f));
        dangerBannerImage = AddImage(banner, Bad);
        MakeRounded(dangerBannerImage);
        dangerText = CreateText(banner, "", 42, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(dangerText.rectTransform);
        dangerBanner = banner.gameObject;
        dangerBanner.SetActive(false);

        // --- Debug-info ---
        {
            var dbg = CreateText(hud, "", 26, Muted, TextAlignmentOptions.TopLeft);
            Place(dbg.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(-80f, 220f));
            dbg.gameObject.AddComponent<DebugHUD>().text = dbg;
            debugPanel = dbg.gameObject;
            debugPanel.SetActive(showDebugInfo);
        }

        // --- Onderpaneel: enkel statusmeldingen + knoppen (de speler zoekt op ruis en geluid) ---
        var bottom = CreateRect("SignalPanel", hud);
        Place(bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 420f));
        AddVLayout(bottom, 12, new RectOffset(50, 50, 20, 30), TextAnchor.LowerCenter);

        var statusText = CreateText(bottom, "", 72, TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
        SetHeight(statusText.rectTransform, 100);
        var hintLine = CreateText(bottom, "", 40, TextColor);
        SetHeight(hintLine.rectTransform, 50);

        tuneButton = CreateButton(bottom, "AFSTEMMEN", () => game.OnTunePressed(), Primary, Bg, 120, 52).gameObject;
        tuneButton.SetActive(false);

        var row = CreateRow(bottom, 90, 16);
        stopButton = CreateButton(row, "STOPPEN", OnStopClicked, Panel, Primary, 100, 32);

        Meter = hud.gameObject.AddComponent<SignalMeter>();
        Meter.levelText = statusText;
        Meter.trendText = hintLine;
        Meter.staticOverlay = staticImage;
        Meter.showDistance = theme.showDistance;
    }

    private void BuildRiddle()
    {
        riddle = CreateOverlayPanel("Riddle");
        riddleTitle = CreateText(riddle, "", 38, Accent, TextAlignmentOptions.Center, FontStyles.Bold);
        FixedText(riddleTitle, 100f, 22f);
        riddleFrame = CreateText(riddle, "", 38, Primary, TextAlignmentOptions.Center, FontStyles.Italic);
        FixedText(riddleFrame, 55f, 22f);
        riddleQuestion = CreateText(riddle, "", 52, TextColor);
        FitText(riddleQuestion, 26f, 140f);
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            answerButtons.Add(CreateButton(riddle, "", () => OnAnswerClicked(index), Panel, TextColor, 120, 38));
        }
        riddleFeedback = CreateText(riddle, "", 38, TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
        FixedText(riddleFeedback, 60f, 22f);
    }

    private void BuildFragment()
    {
        fragment = CreateOverlayPanel("Fragment");
        fragmentTitle = CreateText(fragment, "", 46, Accent, TextAlignmentOptions.Center, FontStyles.Bold);
        FixedText(fragmentTitle, 70f, 24f);
        Spacer(fragment, 30);
        fragmentText = CreateText(fragment, "", 52, Primary, TextAlignmentOptions.TopLeft, FontStyles.Italic);
        FitText(fragmentText, 26f, 200f);
        CreateButton(fragment, "VERDER", OnFragmentContinue, Primary, Bg, 150, 54);
    }

    private void BuildEnd()
    {
        end = CreateScreen("End");
        Spacer(end, 40);
        endTitle = CreateText(end, "", 96, Primary, TextAlignmentOptions.Center, FontStyles.Bold);
        FixedText(endTitle, 240f, 40f);
        Spacer(end, 30);
        endStory = CreateText(end, "", 42, TextColor, TextAlignmentOptions.Center, FontStyles.Italic);
        FitText(endStory, 24f, 200f);
        Spacer(end, 40);
        endStats = CreateText(end, "", 40, TextColor);
        FixedText(endStats, 300f, 22f);
        CreateButton(end, "NIEUW VERHAAL", ShowSetup, Primary, Bg, 150, 54);
        CreateButton(end, "MENU", () => ShowMenu(), Panel, Primary);
    }

    // ================================================================ Schermen tonen

    private void ShowOnly(params RectTransform[] visible)
    {
        foreach (var p in allPanels)
        {
            if (p == null) continue;
            bool show = Array.IndexOf(visible, p) >= 0;
            bool wasVisible = p.gameObject.activeSelf;
            p.gameObject.SetActive(show);
            if (show && !wasVisible) StartCoroutine(FadeIn(p));
        }

        bool playing = hud != null && hud.gameObject.activeSelf;
        menuBackground.SetActive(!playing);
        staticOverlayGO.SetActive(playing);
    }

    public void ShowMenu(string message = null)
    {
        ShowOnly(menu);
        RefreshMenu();
        menuMessage.text = message ?? "";
    }

    public void ShowSetup()
    {
        ShowOnly(setup);
        RefreshSetup();
    }

    public void ShowShop()
    {
        ShowOnly(shop);
        RefreshShop();
    }

    public void ShowHelp() => ShowOnly(help);

    public void ShowLoading(string status)
    {
        ShowOnly(loading);
        loadingStatus.text = status;
    }

    public void SetLoadingStatus(string status) => loadingStatus.text = status;

    public void ShowIntro(string title, string story, string info, Action onBegin)
    {
        ShowOnly(intro);
        introTitle.text = title.ToUpper();
        introStory.text = story;
        introInfo.text = info;
        introCallback = onBegin;
    }

    public void ShowHUD()
    {
        ShowOnly(hud);
        stopConfirmUntil = 0f;
        Label(stopButton).text = "STOPPEN";
    }

    public void ShowRiddle(Riddle r, int number, int total, string place, string frame, Action<int> onAnswer)
    {
        ShowOnly(hud, riddle);
        riddleTitle.text = $"MAST {number}/{total}\n{place}";
        riddleFrame.text = frame;
        riddleQuestion.text = r.question;
        riddleFeedback.text = "";
        answerCallback = onAnswer;
        answersLocked = false;

        for (int i = 0; i < answerButtons.Count; i++)
        {
            bool used = i < r.options.Length;
            answerButtons[i].gameObject.SetActive(used);
            if (!used) continue;
            answerButtons[i].interactable = true;
            SetButtonColors(answerButtons[i], Panel, TextColor);
            Label(answerButtons[i]).text = $"{(char)('A' + i)}.  {r.options[i]}";
        }
    }

    public void MarkAnswer(int index, bool correct, string feedback)
    {
        riddleFeedback.text = feedback;
        riddleFeedback.color = correct ? Good : Bad;
        SetButtonColors(answerButtons[index], correct ? Good : Bad, Color.white);
        if (correct) answersLocked = true;
        else answerButtons[index].interactable = false;
    }

    private void OnAnswerClicked(int index)
    {
        if (answersLocked) return;
        answerCallback?.Invoke(index);
    }

    public void ShowFragment(string label, int number, int total, string text, Action onDone)
    {
        ShowOnly(hud, fragment);
        fragmentTitle.text = $"{label} {number}/{total} ONTVANGEN";
        fragmentCallback = onDone;
        if (typewriter != null) StopCoroutine(typewriter);
        typewriter = StartCoroutine(Typewrite(fragmentText, text));
    }

    private IEnumerator Typewrite(TMP_Text target, string text)
    {
        typing = true;
        target.text = text;
        target.maxVisibleCharacters = 0;
        target.ForceMeshUpdate();
        int total = target.textInfo.characterCount;
        for (int i = 0; i <= total; i++)
        {
            target.maxVisibleCharacters = i;
            yield return new WaitForSeconds(0.025f);
        }
        typing = false;
    }

    private void OnFragmentContinue()
    {
        if (typing)
        {
            // Eerste tik: meteen alle tekst tonen
            if (typewriter != null) StopCoroutine(typewriter);
            fragmentText.maxVisibleCharacters = 99999;
            typing = false;
            return;
        }
        fragmentCallback?.Invoke();
    }

    public void ShowEnd(string title, bool win, string story, string stats)
    {
        ShowOnly(end);
        endTitle.text = title;
        endTitle.color = win ? Primary : Bad;
        endStory.text = story;
        endStats.text = stats;
    }

    public void UpdateHUD(float timeLeft, int index, int total, string hint, bool inDanger, bool nearDanger, bool canTune)
    {
        if (hud == null || !hud.gameObject.activeSelf) return;

        timerText.text = GameManager.FormatTime(timeLeft);
        bool blink = timeLeft < 60f && Mathf.Repeat(Time.time, 1f) < 0.5f;
        timerText.color = blink ? Bad : TextColor;

        mastText.text = $"MAST {Mathf.Min(index + 1, total)}/{total}";
        hintText.text = hint;

        dangerBanner.SetActive(inDanger || nearDanger);
        if (inDanger)
        {
            dangerText.text = "IN STORINGSVELD!  tijd loopt weg";
            dangerBannerImage.color = Color.Lerp(Bad, Color.black, Mathf.PingPong(Time.time * 3f, 0.5f));
        }
        else if (nearDanger)
        {
            dangerText.text = "storingsveld in de buurt";
            dangerBannerImage.color = new Color(0.9f, 0.5f, 0.1f, 0.85f);
        }

        if (tuneButton.activeSelf != canTune) tuneButton.SetActive(canTune);
    }

    // ================================================================ Verversen

    private string SelectedStoryId => selectedStoryIndex < 0 ? null : StoryData.All[selectedStoryIndex].id;

    private void RefreshMenu()
    {
        menuParts.text = $"Radio-onderdelen: {SaveSystem.Parts}";
        menuRadio.text = $"Radio: {theme.displayName}";
        audioLabel.text = SaveSystem.AudioOn ? "GELUID: AAN" : "GELUID: UIT";
        vibrateLabel.text = SaveSystem.VibrationOn ? "TRILLEN: AAN" : "TRILLEN: UIT";
    }

    private void RefreshSetup()
    {
        foreach (var item in lengthButtons)
        {
            bool sel = item.value == selectedLength;
            SetButtonColors(item.button, sel ? Primary : Panel, sel ? Bg : Primary);
        }
        foreach (var item in difficultyButtons)
        {
            bool sel = item.value == selectedDifficulty;
            SetButtonColors(item.button, sel ? Primary : Panel, sel ? Bg : Primary);
        }

        string diffText;
        switch (selectedDifficulty)
        {
            case Difficulty.Easy: diffText = "Makkelijke raadsels, weinig storingsvelden, ruim de tijd.\nJe ziet de naam van de plek waar de mast staat."; break;
            case Difficulty.Medium: diffText = "Gemiddelde raadsels, een storingsveld per traject.\nJe weet enkel wat voor plek het is (kerk, park, ...)."; break;
            default: diffText = "Pittige raadsels, veel storingsvelden, krappe tijd.\nGeen hints: enkel je radio."; break;
        }

        string storyLine;
        if (selectedStoryIndex < 0)
        {
            storyLabel.text = "WILLEKEURIG";
            storyLine = $"Elke keer een ander verhaal ({StoryData.All.Count} in totaal).";
        }
        else
        {
            var st = StoryData.All[selectedStoryIndex];
            bool done = SaveSystem.IsStoryCompleted(st.id);
            storyLabel.text = st.title.ToUpper();
            storyLine = st.subtitle + (done ? "  (voltooid)" : "");
        }

        setupInfo.text = $"{storyLine}\n\n{diffText}\n\nBeloning: ±{game.EstimateReward(selectedLength, selectedDifficulty)} onderdelen + tijdsbonus";
    }

    private void RefreshShop()
    {
        shopParts.text = $"Radio-onderdelen: {SaveSystem.Parts}";

        foreach (Transform child in shopContent) Destroy(child.gameObject);

        foreach (var r in RadioCatalog.All)
        {
            bool owned = SaveSystem.OwnsRadio(r.id);
            bool equipped = SaveSystem.SelectedRadioId == r.id;

            var card = CreateRect("Card_" + r.id, shopContent);
            MakeRounded(AddImage(card, Color.Lerp(r.background, Color.white, 0.08f)));
            AddVLayout(card, 8, new RectOffset(36, 36, 28, 28), TextAnchor.UpperLeft);
            AddBorder(card, equipped ? r.primary : new Color(r.primary.r, r.primary.g, r.primary.b, 0.25f));

            CreateText(card, r.displayName, 54, r.primary, TextAlignmentOptions.Left, FontStyles.Bold);
            CreateText(card, r.description, 34, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Left);
            CreateText(card, "> " + r.PerkText, 32, r.accent, TextAlignmentOptions.Left, FontStyles.Italic);
            if (r.id == RadioCatalog.LegendId)
                CreateText(card, owned ? "LEGENDARISCH - je hebt de game uitgespeeld!" : "LEGENDARISCH - verandert je startscherm", 32, LegendGold, TextAlignmentOptions.Left, FontStyles.Bold);

            string label = equipped ? "IN GEBRUIK" : owned ? "GEBRUIKEN" : $"KOPEN  ({r.price} onderdelen)";
            var btn = CreateButton(card, label, () => game.BuyOrEquip(r),
                                   equipped ? Color.Lerp(r.background, Color.white, 0.15f) : r.primary,
                                   equipped ? r.primary : r.background, 110, 40);
            btn.interactable = !equipped && (owned || SaveSystem.Parts >= r.price);
        }
    }

    // ================================================================ Bevestigingsknoppen

    private void OnStopClicked()
    {
        if (Time.unscaledTime < stopConfirmUntil)
        {
            stopConfirmUntil = 0f;
            game.StopToMenu();
        }
        else
        {
            stopConfirmUntil = Time.unscaledTime + 3f;
            Label(stopButton).text = "ZEKER?";
        }
    }

    void Update()
    {
        UpdateResponsiveLayout(false);
        AnimateLegendMenu();
        AnimateMenuBackground();

        // Debug-paneel volgt live het vinkje 'Show Debug Info' (ook tijdens Play)
        if (debugPanel != null && debugPanel.activeSelf != showDebugInfo) debugPanel.SetActive(showDebugInfo);

        if (loading != null && loading.gameObject.activeSelf)
            loadingTitle.text = "AFSTEMMEN" + new string('.', 1 + (int)(Time.time * 2f) % 3);

        if (stopButton != null && stopConfirmUntil > 0f && Time.unscaledTime > stopConfirmUntil)
        {
            stopConfirmUntil = 0f;
            Label(stopButton).text = "STOPPEN";
        }
    }

    // ================================================================ Responsive

    // Schaalt de UI per schermformaat en houdt alles binnen de safe area.
    private void UpdateResponsiveLayout(bool force)
    {
        if (scaler == null || safeRoot == null || column == null) return;

        var size = new Vector2Int(Screen.width, Screen.height);
        Rect safe = Screen.safeArea;
        if (!force && size == lastScreenSize && safe == lastSafeArea)
        {
            column.sizeDelta = new Vector2(Mathf.Min(safeRoot.rect.width, maxColumnWidth), 0f);
            return;
        }
        lastScreenSize = size;
        lastSafeArea = safe;

        float aspect = (float)Mathf.Max(1, size.x) / Mathf.Max(1, size.y);
        float referenceAspect = scaler.referenceResolution.x / scaler.referenceResolution.y;
        scaler.matchWidthOrHeight = aspect > referenceAspect ? 1f : 0f;

        ApplySafeArea(safeRoot);
        column.sizeDelta = new Vector2(Mathf.Min(safeRoot.rect.width, maxColumnWidth), 0f);
    }

    // ================================================================ Bouwstenen

    private RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void SetHeight(RectTransform rt, float height)
    {
        var le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
    }

    private static void ApplySafeArea(RectTransform rt)
    {
        Rect sa = Screen.safeArea;
        float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
        rt.anchorMin = new Vector2(sa.xMin / w, sa.yMin / h);
        rt.anchorMax = new Vector2(sa.xMax / w, sa.yMax / h);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Image AddImage(RectTransform rt, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static VerticalLayoutGroup AddVLayout(RectTransform rt, float spacing, RectOffset padding, TextAnchor alignment)
    {
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = padding;
        v.childAlignment = alignment;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return v;
    }

    // Volledig scherm met verticale layout.
    private RectTransform CreateScreen(string name)
    {
        var rt = CreateRect(name, column);
        Stretch(rt);
        AddVLayout(rt, 24, new RectOffset(70, 70, 80, 70), TextAnchor.UpperCenter);
        rt.gameObject.SetActive(false);
        return rt;
    }

    // Paneel over de HUD, onder de bovenbalk.
    private RectTransform CreateOverlayPanel(string name)
    {
        var rt = CreateRect(name, column);
        Stretch(rt);
        rt.offsetMin = new Vector2(16f, 16f);
        rt.offsetMax = new Vector2(-16f, -222f);
        var bgColor = Bg;
        bgColor.a = 0.96f;
        MakeRounded(AddImage(rt, bgColor));
        AddBorder(rt, new Color(Primary.r, Primary.g, Primary.b, 0.35f));
        AddVLayout(rt, 22, new RectOffset(60, 60, 50, 60), TextAnchor.UpperCenter);
        rt.gameObject.SetActive(false);
        return rt;
    }

    private RectTransform CreateRow(Transform parent, float height, float spacing)
    {
        var rt = CreateRect("Row", parent);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;
        SetHeight(rt, height);
        return rt;
    }

    // Tekst die de vrije ruimte vult en krimpt als ze niet past.
    private static void FitText(TMP_Text t, float minFontSize, float minHeight)
    {
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = minFontSize;
        var le = t.GetComponent<LayoutElement>();
        if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
        le.minHeight = minHeight;
        le.preferredHeight = minHeight;
        le.flexibleHeight = 1f;
    }

    // Tekst met vaste hoogte die krimpt als ze niet past.
    private static void FixedText(TMP_Text t, float height, float minFontSize)
    {
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = minFontSize;
        SetHeight(t.rectTransform, height);
    }

    private void Spacer(Transform parent, float height)
    {
        var rt = CreateRect("Spacer", parent);
        SetHeight(rt, height);
    }

    private void FlexSpacer(Transform parent)
    {
        var rt = CreateRect("FlexSpacer", parent);
        rt.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
    }

    private TMP_Text CreateText(Transform parent, string text, float size, Color color,
                                TextAlignmentOptions alignment = TextAlignmentOptions.Center,
                                FontStyles style = FontStyles.Normal)
    {
        var rt = CreateRect("Text", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = alignment;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal; // tekst laten afbreken binnen het kader
        return t;
    }

    private Button CreateButton(Transform parent, string label, Action onClick, Color background, Color foreground,
                                float height = 130f, float fontSize = 46f)
    {
        var rt = CreateRect("Button", parent);
        var img = AddImage(rt, background);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
        btn.colors = colors;
        SetHeight(rt, height);

        MakeRounded(img);
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(0f, -6f);
        AddBorder(rt, new Color(1f, 1f, 1f, 0.12f));

        var t = CreateText(rt, label, fontSize, foreground, TextAlignmentOptions.Center, FontStyles.Bold);
        t.enableAutoSizing = true;          // lange tekst krimpt i.p.v. uit de knop te lopen
        t.fontSizeMax = fontSize;
        t.fontSizeMin = fontSize * 0.5f;
        Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(20f, 0f);
        t.rectTransform.offsetMax = new Vector2(-20f, 0f);

        btn.onClick.AddListener(() =>
        {
            if (game != null) game.PlayClick();
            if (isActiveAndEnabled) StartCoroutine(Punch(rt));
            onClick?.Invoke();
        });
        return btn;
    }

    // Image afronden (9-sliced).
    private static Image MakeRounded(Image img)
    {
        img.sprite = UIStyle.Rounded;
        img.type = Image.Type.Sliced;
        return img;
    }

    // Dunne afgeronde rand, buiten de layout.
    private void AddBorder(RectTransform parent, Color color)
    {
        var border = CreateRect("Rand", parent);
        Stretch(border);
        border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var img = AddImage(border, color);
        img.sprite = UIStyle.RoundedOutline;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        border.SetAsFirstSibling();
    }

    // Kort indrukken van een knop.
    private IEnumerator Punch(RectTransform rt)
    {
        float t = 0f;
        while (t < 0.18f && rt != null)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.18f;
            rt.localScale = Vector3.one * (1f - 0.06f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    // Fade-in met lichte zoom.
    private IEnumerator FadeIn(RectTransform panel)
    {
        if (panel == null) yield break;
        var group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();
        float t = 0f;
        while (t < 0.25f)
        {
            if (panel == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 0.25f);
            group.alpha = k;
            panel.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, k);
            yield return null;
        }
        if (panel == null) yield break;
        group.alpha = 1f;
        panel.localScale = Vector3.one;
    }

    // Animaties van de menu-achtergrond.
    private void AnimateMenuBackground()
    {
        if (menuBackground == null || !menuBackground.activeSelf) return;

        for (int i = 0; i < waveRings.Count; i++)
        {
            if (waveRings[i] == null) continue;
            float k = Mathf.Repeat(Time.time * 0.16f + i / (float)waveRings.Count, 1f);
            float size = Mathf.Lerp(80f, 1600f, k);
            waveRings[i].rectTransform.sizeDelta = new Vector2(size, size);
            Color c = legendOwned ? LegendGold : Primary;
            c.a = (1f - k) * (1f - k) * 0.35f;
            waveRings[i].color = c;
        }

        if (dialNeedle != null)
        {
            // Naald "zoekt" naar een zender: trage golf + wat ruis
            float x = 0.5f + 0.38f * Mathf.Sin(Time.time * 0.35f) + 0.04f * (Mathf.PerlinNoise(Time.time * 1.5f, 0f) - 0.5f);
            dialNeedle.anchorMin = new Vector2(x, dialNeedle.anchorMin.y);
            dialNeedle.anchorMax = new Vector2(x, dialNeedle.anchorMax.y);
        }

        if (menuTitle != null && menu != null && menu.gameObject.activeInHierarchy)
        {
            if (!titleStyled) StyleTitle();
            // Af en toe een kleine "storing" in de titel, zoals een neonbord
            if (!legendOwned) menuTitle.alpha = Mathf.PerlinNoise(Time.time * 4f, 3f) < 0.18f ? 0.6f : 1f;
        }
    }

    // Gloed rond de titel (pas als de tekst actief is, anders crasht TMP).
    private void StyleTitle()
    {
        titleStyled = true;
        Material mat = menuTitle.fontMaterial;
        if (mat == null) return;
        Color glow = legendOwned ? LegendGold : Primary;
        glow.a = 0.6f;
        mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, glow);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.4f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.85f);
        menuTitle.UpdateMeshPadding();
    }

    private static TMP_Text Label(Button b) => b.GetComponentInChildren<TMP_Text>(true);

    private static void SetButtonColors(Button b, Color background, Color foreground)
    {
        ((Image)b.targetGraphic).color = background;
        Label(b).color = foreground;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }
}