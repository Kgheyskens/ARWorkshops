using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Volledige UI, in code opgebouwd.
public class GameUI : MonoBehaviour
{
    public float maxColumnWidth = 1100f;

    public event Action StartPressed, RestartPressed, MenuPressed, DifficultyPressed;
    public event Action<int> WishPressed;

    // Kleuren
    private static readonly Color Gold = new Color(1f, 0.8f, 0.32f);
    private static readonly Color Night = new Color(0.07f, 0.04f, 0.14f);
    private static readonly Color Card = new Color(0.1f, 0.06f, 0.2f, 0.82f);
    private static readonly Color Cyan = new Color(0.4f, 0.9f, 1f);
    private static readonly Color Soft = new Color(0.85f, 0.82f, 0.95f);

    private SoundFx sfx;
    private CanvasScaler scaler;
    private RectTransform canvasRoot, safeRoot, column;
    private Vector2Int lastScreen;
    private Rect lastSafe;

    private RectTransform menu, scan, hud, wishes, end, lost;
    private GameObject menuBackground;
    private TMP_Text menuTitle, soundLabel;
    private bool titleStyled;
    private readonly List<Image> stars = new List<Image>();

    // HUD
    private TMP_Text stepChip, stepTitle, stepText, toastText, meterLabel, meterValue, guideText, statusLine;
    private RectTransform meterPanel, guidePanel, toastRt, aimCross, guideArrow;
    private Image meterFill;
    // Spraak
    private RectTransform speechPanel;
    private TMP_Text speechText, speechHint;
    private bool waitingTap, tapped;
    private float waitStart;
    private Coroutine typing;
    // Wensen
    private TMP_Text wishTitle;
    private readonly List<Button> wishButtons = new List<Button>();
    // Keuzepaneel (moeilijkheid / makeover)
    private RectTransform choices, choiceList;
    private TMP_Text choiceTitle;
    private Action<int> choiceCallback;
    private TMP_Text difficultyLabel, difficultyDesc;
    // Eind
    private TMP_Text endStats, endTitle, speechName;
    // Gevecht
    private RectTransform battlePanel, heartsRow;
    private Image guardFill;
    private readonly List<Image> hearts = new List<Image>();
    private int shownHearts;
    private TMP_Text battleWarning;
    private Image dangerEdge;
    private bool dangerOn;
    private static Sprite heartSprite;
    private static readonly Color EmptyHeart = new Color(0.3f, 0.27f, 0.36f, 0.6f);
    private Image flash;
    private Coroutine flashRoutine;
    private Coroutine toastRoutine;

    private void Awake()
    {
        sfx = FindFirstObjectByType<SoundFx>();
        Build();
    }

    // ================================================================== Opbouw

    private void Build()
    {
        EnsureEventSystem();
        var canvasGO = new GameObject("GenieUI Canvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        canvasGO.AddComponent<GraphicRaycaster>();
        canvasRoot = (RectTransform)canvasGO.transform;

        menuBackground = BuildBackground().gameObject;
        safeRoot = NewRect("SafeArea", canvasRoot);
        column = NewRect("Column", safeRoot);
        column.anchorMin = new Vector2(0.5f, 0f);
        column.anchorMax = new Vector2(0.5f, 1f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.sizeDelta = new Vector2(1080f, 0f);
        UpdateLayout(true);

        BuildMenu();
        BuildScan();
        BuildHud();
        BuildWishes();
        BuildEnd();
        BuildLost();
        BuildBattle();
        ShowMenu();
    }

    private RectTransform BuildBackground()
    {
        var bg = NewRect("MenuBackground", canvasRoot);
        Stretch(bg);
        var grad = bg.gameObject.AddComponent<RawImage>();
        grad.texture = UIStyle.Gradient(new Color(0.03f, 0.02f, 0.08f), new Color(0.22f, 0.08f, 0.32f));
        grad.raycastTarget = false;
        // Sterretjes
        var rnd = new System.Random(5);
        for (int i = 0; i < 70; i++)
        {
            var s = NewRect("Star", bg);
            float x = (float)rnd.NextDouble(), y = (float)rnd.NextDouble();
            s.anchorMin = s.anchorMax = new Vector2(x, y);
            float size = 3f + (float)rnd.NextDouble() * 7f;
            s.sizeDelta = new Vector2(size, size);
            var img = s.gameObject.AddComponent<Image>();
            img.sprite = UIStyle.Circle;
            img.color = new Color(1f, 0.95f, 0.8f, 0.6f);
            img.raycastTarget = false;
            stars.Add(img);
        }
        var vig = NewRect("Vignette", bg);
        Stretch(vig);
        var v = vig.gameObject.AddComponent<RawImage>();
        v.texture = UIStyle.Vignette;
        v.raycastTarget = false;
        return bg;
    }

    private void BuildMenu()
    {
        menu = MakeScreen("Menu");
        Flex(menu);
        menuTitle = MakeText(menu, "GENIE\nIN A BOX", 190, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        menuTitle.lineSpacing = -25f;
        Fixed(menuTitle, 420, 80);
        var sub = MakeText(menu, "Er zit iets in je cornflakesdoos...\nen het wil eruit.", 40, Soft, TextAlignmentOptions.Center, FontStyles.Italic);
        Fixed(sub, 130, 26);
        Flex(menu);
        var steps = MakeText(menu,
            "<b>1</b>  Leg de doos plat op tafel\n<b>2</b>  Blaas het stof weg\n<b>3</b>  Stap rond tot de sleutel heel is\n<b>4</b>  Kantel de doos en bevrijd de geest",
            36, Soft, TextAlignmentOptions.Left);
        steps.lineSpacing = 25f;
        Fixed(steps, 260, 22);
        Space(menu, 30);
        MakeButton(menu, "START", () => StartPressed?.Invoke(), Gold, Night, 150, 60);
        var diff = MakeButton(menu, "DOOLHOF: NORMAAL", () => DifficultyPressed?.Invoke(), new Color(0.55f, 0.25f, 0.8f, 0.9f), Color.white, 110, 38);
        difficultyLabel = LabelOf(diff);
        difficultyDesc = MakeText(menu, "", 28, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center, FontStyles.Italic);
        Fixed(difficultyDesc, 44, 18);
        var sound = MakeButton(menu, "GELUID: AAN", ToggleSound, new Color(1f, 1f, 1f, 0.1f), Soft, 110, 38);
        soundLabel = LabelOf(sound);
        Space(menu, 10);
        var credit = MakeText(menu, "Kayn Gheyskens", 26, new Color(1f, 1f, 1f, 0.45f));
        Fixed(credit, 50, 18);
    }

    private void BuildScan()
    {
        scan = NewRect("Scan", column);
        Stretch(scan);
        // Kaderhoeken in het midden
        var frame = NewRect("Frame", scan);
        frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.55f);
        frame.sizeDelta = new Vector2(760f, 560f);
        foreach (var corner in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) })
        {
            var h = NewRect("CornerH", frame); h.anchorMin = h.anchorMax = corner; h.pivot = corner; h.sizeDelta = new Vector2(110f, 12f);
            h.gameObject.AddComponent<Image>().color = Gold;
            var v = NewRect("CornerV", frame); v.anchorMin = v.anchorMax = corner; v.pivot = corner; v.sizeDelta = new Vector2(12f, 110f);
            v.gameObject.AddComponent<Image>().color = Gold;
        }
        var card = NewRect("ScanCard", scan);
        Place(card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(-80f, 300f));
        Rounded(card.gameObject.AddComponent<Image>()).color = Card;
        VLayout(card, 10, new RectOffset(50, 50, 40, 40), TextAnchor.MiddleCenter);
        var t1 = MakeText(card, "Zoek de doos", 54, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        Fixed(t1, 80, 30);
        var t2 = MakeText(card, "Leg je cornflakesdoos plat op tafel (voorkant naar boven) en richt je camera erop.", 34, Soft);
        Fixed(t2, 130, 20);
        scan.gameObject.SetActive(false);
    }

    private void BuildHud()
    {
        hud = NewRect("Hud", column);
        Stretch(hud);

        // ---- Bovenkaart met stap + uitleg
        var top = NewRect("TopCard", hud);
        Place(top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(-48f, 250f));
        Rounded(top.gameObject.AddComponent<Image>()).color = Card;
        VLayout(top, 4, new RectOffset(44, 44, 26, 26), TextAnchor.UpperLeft);
        stepChip = MakeText(top, "STAP 1/4", 28, Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
        stepChip.characterSpacing = 6f;
        Fixed(stepChip, 40, 18);
        stepTitle = MakeText(top, "", 52, Gold, TextAlignmentOptions.Left, FontStyles.Bold);
        Fixed(stepTitle, 70, 28);
        stepText = MakeText(top, "", 32, Soft, TextAlignmentOptions.Left);
        Fixed(stepText, 90, 20);

        // Geluid aan/uit (klein, rechtsboven onder de kaart)
        var mute = MakeButton(hud, "GELUID", ToggleSound, new Color(0f, 0f, 0f, 0.35f), Soft, 90, 30);
        var muteRt = (RectTransform)mute.transform;
        Place(muteRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -292f), new Vector2(190f, 80f));
        mute.GetComponent<LayoutElement>().ignoreLayout = true;

        // ---- Statusregel (lampjes / tijd / level)
        statusLine = MakeText(hud, "", 36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(statusLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(-260f, 70f));
        statusLine.outlineWidth = 0f;

        // ---- Richtkruisje in het midden
        aimCross = NewRect("Aim", hud);
        aimCross.anchorMin = aimCross.anchorMax = new Vector2(0.5f, 0.5f);
        aimCross.sizeDelta = new Vector2(70f, 70f);
        var ai = aimCross.gameObject.AddComponent<Image>();
        ai.sprite = UIStyle.Ring;
        ai.color = new Color(1f, 1f, 1f, 0.8f);
        ai.raycastTarget = false;

        // ---- Richtingspijl (sleutelpuzzel)
        guidePanel = NewRect("Guide", hud);
        guidePanel.anchorMin = guidePanel.anchorMax = new Vector2(0.5f, 0.42f);
        guidePanel.sizeDelta = new Vector2(700f, 330f);
        guideArrow = NewRect("Arrow", guidePanel);
        guideArrow.anchorMin = guideArrow.anchorMax = new Vector2(0.5f, 0.62f);
        guideArrow.sizeDelta = new Vector2(150f, 150f);
        var arr = guideArrow.gameObject.AddComponent<Image>();
        arr.sprite = ArrowSprite();
        arr.color = Gold;
        arr.raycastTarget = false;
        guideText = MakeText(guidePanel, "", 40, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(guideText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 90f));
        guideText.enableAutoSizing = true; guideText.fontSizeMax = 40; guideText.fontSizeMin = 22;

        // ---- Meter linksonder (blaaskracht / sleutelbeeld)
        meterPanel = NewRect("Meter", hud);
        Place(meterPanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(-48f, 150f));
        Rounded(meterPanel.gameObject.AddComponent<Image>()).color = Card;
        meterLabel = MakeText(meterPanel, "", 30, Cyan, TextAlignmentOptions.Left, FontStyles.Bold);
        Place(meterLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(0f, 1f), new Vector2(40f, -18f), new Vector2(0f, 50f));
        meterValue = MakeText(meterPanel, "", 30, Soft, TextAlignmentOptions.Right, FontStyles.Bold);
        Place(meterValue.rectTransform, new Vector2(0.4f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -18f), new Vector2(0f, 50f));
        var barBg = NewRect("BarBg", meterPanel);
        Place(barBg, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(-80f, 38f));
        Rounded(barBg.gameObject.AddComponent<Image>()).color = new Color(1f, 1f, 1f, 0.12f);
        var fill = NewRect("Fill", barBg);
        Stretch(fill);
        meterFill = Rounded(fill.gameObject.AddComponent<Image>());
        meterFill.color = Gold;

        // ---- Melding (toast)
        toastRt = NewRect("Toast", hud);
        Place(toastRt, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 120f));
        Rounded(toastRt.gameObject.AddComponent<Image>()).color = new Color(0f, 0f, 0f, 0.6f);
        toastText = MakeText(toastRt, "", 42, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(toastText.rectTransform);
        toastText.rectTransform.offsetMin = new Vector2(30f, 10f);
        toastText.rectTransform.offsetMax = new Vector2(-30f, -10f);
        toastText.enableAutoSizing = true; toastText.fontSizeMax = 42; toastText.fontSizeMin = 22;
        toastRt.gameObject.AddComponent<CanvasGroup>().alpha = 0f;

        // ---- Tekstballon van de geest
        speechPanel = NewRect("Speech", hud);
        Place(speechPanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(-48f, 300f));
        Rounded(speechPanel.gameObject.AddComponent<Image>()).color = new Color(0.98f, 0.95f, 1f, 0.95f);
        var name = MakeText(speechPanel, "DE GEEST", 28, new Color(0.5f, 0.2f, 0.7f), TextAlignmentOptions.Left, FontStyles.Bold);
        speechName = name;
        name.characterSpacing = 6f;
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(44f, -20f), new Vector2(-88f, 44f));
        speechText = MakeText(speechPanel, "", 38, Night, TextAlignmentOptions.TopLeft);
        Place(speechText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(-88f, -90f));
        speechText.enableAutoSizing = true; speechText.fontSizeMax = 38; speechText.fontSizeMin = 22;
        // Tik op het tekstvak om verder te gaan (eerste tik = tekst meteen helemaal tonen)
        var speechBtn = speechPanel.gameObject.AddComponent<Button>();
        speechBtn.targetGraphic = speechPanel.GetComponent<Image>();
        speechBtn.transition = Selectable.Transition.None;
        speechBtn.onClick.AddListener(OnSpeechTap);
        speechHint = MakeText(speechPanel, "tik om verder te gaan  »", 26, new Color(0.5f, 0.2f, 0.7f), TextAlignmentOptions.Right, FontStyles.Italic);
        Place(speechHint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 14f), new Vector2(-60f, 40f));
        speechHint.raycastTarget = false;
        speechHint.gameObject.SetActive(false);
        speechPanel.gameObject.SetActive(false);

        hud.gameObject.SetActive(false);
    }

    private void BuildWishes()
    {
        wishes = NewRect("Wishes", column);
        Place(wishes, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 360f), new Vector2(-48f, 650f));
        Rounded(wishes.gameObject.AddComponent<Image>()).color = Card;
        VLayout(wishes, 16, new RectOffset(34, 34, 30, 30), TextAnchor.UpperCenter);
        wishTitle = MakeText(wishes, "Kies een wens", 46, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        Fixed(wishTitle, 70, 26);
        // 0 = vuurwerk, 1 = makeover, 2 = nieuw doolhof, 3 = feniks, 4 = waarzegger, 5 = reuzengeest, 6 = meer wensen
        Color purple = new Color(0.55f, 0.25f, 0.8f);
        var r1 = Row(wishes, 104f, 14f);
        wishButtons.Add(MakeButton(r1, "Vuurwerk!", () => WishPressed?.Invoke(0), purple, Color.white, 104, 38));
        wishButtons.Add(MakeButton(r1, "Makeover", () => WishPressed?.Invoke(1), purple, Color.white, 104, 38));
        var r2 = Row(wishes, 104f, 14f);
        wishButtons.Add(MakeButton(r2, "Vliegend tapijt", () => WishPressed?.Invoke(3), new Color(0.85f, 0.35f, 0.1f), Color.white, 104, 38));
        wishButtons.Add(MakeButton(r2, "Waarzegger", () => WishPressed?.Invoke(4), new Color(0.3f, 0.3f, 0.8f), Color.white, 104, 38));
        var r3 = Row(wishes, 104f, 14f);
        wishButtons.Add(MakeButton(r3, "Reuzengeest", () => WishPressed?.Invoke(5), new Color(0.2f, 0.55f, 0.85f), Color.white, 104, 38));
        wishButtons.Add(MakeButton(r3, "MEER WENSEN!", () => WishPressed?.Invoke(6), new Color(0.75f, 0.12f, 0.18f), Color.white, 104, 38));
        wishButtons.Add(MakeButton(wishes, "Nieuw doolhof kiezen  <size=70%>(kost geen wens)</size>", () => WishPressed?.Invoke(2), new Color(1f, 1f, 1f, 0.14f), Gold, 96, 36));
        wishes.gameObject.SetActive(false);

        BuildChoices();
    }

    // Keuzepaneel met knoppen + Terug.
    private void BuildChoices()
    {
        choices = NewRect("Choices", column);
        Place(choices, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(-48f, 1060f));
        Rounded(choices.gameObject.AddComponent<Image>()).color = new Color(0.08f, 0.05f, 0.16f, 0.94f);
        VLayout(choices, 16, new RectOffset(36, 36, 32, 32), TextAnchor.UpperCenter);
        choiceTitle = MakeText(choices, "", 46, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        Fixed(choiceTitle, 70, 26);
        choiceList = NewRect("List", choices);
        VLayout(choiceList, 14, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
        choiceList.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        MakeButton(choices, "Terug", () => PickChoice(-1), new Color(1f, 1f, 1f, 0.12f), Soft, 95, 36);
        choices.gameObject.SetActive(false);
    }

    private void BuildEnd()
    {
        end = MakeScreen("End");
        Flex(end);
        var t = MakeText(end, "DE GEEST\nIS VRIJ!", 140, Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        t.lineSpacing = -20f;
        Fixed(t, 340, 60);
        endTitle = t;
        endStats = MakeText(end, "", 40, Soft);
        Fixed(endStats, 300, 22);
        Flex(end);
        MakeButton(end, "OPNIEUW SPELEN", () => RestartPressed?.Invoke(), Gold, Night, 140, 52);
        MakeButton(end, "MENU", () => MenuPressed?.Invoke(), new Color(1f, 1f, 1f, 0.12f), Soft, 110, 40);
    }

    // Gevechts-HUD: balk Wachter + hartjes.
    private void BuildBattle()
    {
        battlePanel = NewRect("Battle", column);
        Stretch(battlePanel);
        var top = NewRect("GuardBar", battlePanel);
        Place(top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(-80f, 130f));
        Rounded(top.gameObject.AddComponent<Image>()).color = new Color(0.15f, 0.02f, 0.05f, 0.8f);
        var name = MakeText(top, "DE WACHTER", 34, new Color(1f, 0.6f, 0.4f), TextAlignmentOptions.Center, FontStyles.Bold);
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(0f, 48f));
        var barBg = NewRect("BarBg", top);
        Place(barBg, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(-60f, 40f));
        Rounded(barBg.gameObject.AddComponent<Image>()).color = new Color(1f, 1f, 1f, 0.15f);
        var fill = NewRect("Fill", barBg);
        Stretch(fill);
        guardFill = Rounded(fill.gameObject.AddComponent<Image>());
        guardFill.color = new Color(1f, 0.3f, 0.2f);

        heartsRow = NewRect("Hearts", battlePanel);
        Place(heartsRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 360f), new Vector2(600f, 80f));
        battleWarning = MakeText(battlePanel, "", 76, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Center, FontStyles.Bold);
        Place(battleWarning.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(-60f, 220f));
        battleWarning.outlineWidth = 0.25f;
        battleWarning.outlineColor = new Color32(90, 0, 10, 255);
        var hint = MakeText(battlePanel, "Tik = schieten  ·  stap opzij, BUK en blijf uit de rode tekens", 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 450f), new Vector2(-80f, 80f));
        battlePanel.gameObject.SetActive(false);

        // Rode rand rond het scherm: "hier sta je niet veilig!"
        var de = NewRect("Danger", canvasRoot);
        Stretch(de);
        dangerEdge = de.gameObject.AddComponent<Image>();
        dangerEdge.sprite = VignetteSprite();
        dangerEdge.color = new Color(1f, 0.1f, 0.1f, 0f);
        dangerEdge.raycastTarget = false;

        var fl = NewRect("Flash", canvasRoot);
        Stretch(fl);
        flash = fl.gameObject.AddComponent<Image>();
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;
    }

    public void ShowBattle(bool show, int maxHearts)
    {
        battlePanel.gameObject.SetActive(show);
        dangerOn = false;
        if (dangerEdge != null && !show) dangerEdge.color = new Color(1f, 0.1f, 0.1f, 0f);
        if (!show) return;
        battleWarning.text = "";
        foreach (var h in hearts) Destroy(h.gameObject);
        hearts.Clear();
        for (int i = 0; i < maxHearts; i++)
        {
            var hr = NewRect("Heart", heartsRow);
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
            hr.sizeDelta = new Vector2(64f, 64f);
            hr.anchoredPosition = new Vector2((i - (maxHearts - 1) / 2f) * 84f, 0f);
            var img = hr.gameObject.AddComponent<Image>();
            img.sprite = HeartSprite();
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
            hearts.Add(img);
        }
        shownHearts = maxHearts;
    }

    public void SetBattle(int heartsLeft, float guard01)
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            hearts[i].color = i < heartsLeft ? Color.white : EmptyHeart;
            if (i >= heartsLeft && i < shownHearts) hearts[i].rectTransform.localScale = Vector3.one * 1.6f; // net verloren: "pop"
        }
        shownHearts = heartsLeft;
        guardFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(guard01), 1f);
        guardFill.enabled = guard01 > 0.01f;
    }

    // Klap van de geest: flits, PATS!, zwart.
    public IEnumerator SlapBlackout()
    {
        var overlay = NewRect("Slap", canvasRoot);
        Stretch(overlay);
        overlay.SetAsLastSibling();
        var white = overlay.gameObject.AddComponent<Image>();
        white.color = Color.white;
        white.raycastTarget = true; // niets meer aanklikbaar
        var pats = MakeText(overlay, "PATS!", 260, new Color(1f, 0.82f, 0.1f), TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(pats.rectTransform);
        pats.outlineWidth = 0.3f;
        pats.outlineColor = new Color32(120, 10, 10, 255);
        pats.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        var blackRt = NewRect("Black", overlay);
        Stretch(blackRt);
        var black = blackRt.gameObject.AddComponent<Image>();
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = false;

        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            float punch = 1f + 0.6f * Mathf.Exp(-t * 9f);
            pats.rectTransform.localScale = Vector3.one * punch;
            overlay.anchoredPosition = UnityEngine.Random.insideUnitCircle * 40f * Mathf.Exp(-t * 4f);
            white.color = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), t / 0.7f);
            yield return null;
        }
        overlay.anchoredPosition = Vector2.zero;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f)
        {
            black.color = new Color(0f, 0f, 0f, t);
            yield return null;
        }
        black.color = Color.black;
    }

    // Gevechtswaarschuwing + rode gevarenrand.
    public void SetBattleWarning(string text, bool danger)
    {
        if (battleWarning != null && battleWarning.text != text) battleWarning.text = text;
        dangerOn = danger;
    }

    // Scherm kort laten flitsen.
    public void Flash(Color color, float duration)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(color, duration));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        flash.transform.SetAsLastSibling();
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            flash.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
            yield return null;
        }
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    private void BuildLost()
    {
        lost = NewRect("Lost", canvasRoot);
        Stretch(lost);
        lost.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var t = MakeText(lost, "Waar is de doos?\n<size=60%>Richt je camera opnieuw op de cornflakesdoos</size>", 60, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(80f, 0f);
        t.rectTransform.offsetMax = new Vector2(-80f, 0f);
        lost.gameObject.SetActive(false);
    }

    // ================================================================== Publieke API

    public void ShowMenu()
    {
        SetPanels(menu);
        menuBackground.SetActive(true);
        lost.gameObject.SetActive(false);
    }

    public void ShowScan()
    {
        SetPanels(scan);
        menuBackground.SetActive(false);
    }

    // HUD tonen (stap, titel, uitleg).
    public void ShowStep(int step, int total, string title, string text) => ShowStep($"STAP {step}/{total}", title, text);

    public void ShowStep(string chip, string title, string text)
    {
        if (!hud.gameObject.activeSelf) { SetPanels(hud); StartCoroutine(FadeIn(hud)); }
        menuBackground.SetActive(false);
        stepChip.text = chip;
        stepTitle.text = title;
        stepText.text = text;
        SetAim(false);
        SetGuide(false, Vector3.zero, 0f);
        SetMeter(false, "", 0f, "");
        SetStatus("");
        HideSpeech();
    }

    public void SetAim(bool show, bool onTarget = true)
    {
        aimCross.gameObject.SetActive(show);
        if (show) aimCross.GetComponent<Image>().color = onTarget ? new Color(1f, 0.9f, 0.5f, 0.9f) : new Color(1f, 1f, 1f, 0.4f);
    }

    public void SetMeter(bool show, string label, float value, string valueText)
    {
        meterPanel.gameObject.SetActive(show);
        if (!show) return;
        meterLabel.text = label;
        meterValue.text = valueText;
        var rt = meterFill.rectTransform;
        rt.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        meterFill.enabled = value > 0.01f;
    }

    public void SetStatus(string text) => statusLine.text = text;

    // Richtingspijl (camera-ruimte).
    public void SetGuide(bool show, Vector3 move, float score)
    {
        guidePanel.gameObject.SetActive(show);
        if (!show) return;
        if (score > 0.88f)
        {
            guideArrow.gameObject.SetActive(false);
            guideText.text = "Bijna! Hou je gsm stil...";
            guideText.color = Gold;
            return;
        }
        guideArrow.gameObject.SetActive(true);
        Vector2 dir = new Vector2(move.x, move.y);
        string hint;
        if (dir.magnitude < 0.06f)
        {
            hint = move.z > 0f ? "Kom dichterbij" : "Ga wat verder weg";
            dir = move.z > 0f ? Vector2.up : Vector2.down;
        }
        else
        {
            string h = Mathf.Abs(move.x) > 0.04f ? (move.x > 0f ? "naar rechts" : "naar links") : "";
            string v = Mathf.Abs(move.y) > 0.04f ? (move.y > 0f ? "hoger" : "lager") : "";
            hint = Mathf.Abs(move.x) >= Mathf.Abs(move.y) ? "Stap " + h + (v != "" ? " en " + v : "") : "Hou je gsm " + v + (h != "" ? ", " + h : "");
        }
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        guideArrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        guideArrow.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f));
        guideArrow.GetComponent<Image>().color = Color.Lerp(new Color(0.4f, 0.6f, 1f), Gold, score);
        guideText.text = hint;
        guideText.color = Color.white;
    }

    public void Toast(string text, float duration = 2f)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(text, duration));
    }

    public void Say(string text) => SayAs("DE GEEST", text);

    public void SayAs(string speaker, string text)
    {
        speechName.text = speaker;
        speechPanel.gameObject.SetActive(true);
        meterPanel.gameObject.SetActive(false);
        if (typing != null) StopCoroutine(typing);
        typing = StartCoroutine(Type(text));
    }

    public void HideSpeech()
    {
        if (typing != null) StopCoroutine(typing);
        typing = null;
        waitingTap = false;
        speechHint.gameObject.SetActive(false);
        speechPanel.gameObject.SetActive(false);
    }

    // Wacht op tik op het tekstvak.
    public IEnumerator WaitForTap()
    {
        tapped = false;
        waitingTap = true;
        waitStart = Time.unscaledTime;
        speechHint.gameObject.SetActive(true);
        while (!tapped && speechPanel.gameObject.activeSelf) yield return null;
        waitingTap = false;
        HideSpeech();
        if (sfx != null) sfx.Play("click", 0.3f, 1.6f);
    }

    private void OnSpeechTap()
    {
        if (typing != null)
        {
            StopCoroutine(typing);
            typing = null;
            speechText.maxVisibleCharacters = 99999;
            return;
        }
        if (waitingTap && Time.unscaledTime - waitStart > 0.2f) tapped = true;
    }

    public void ShowWishes(bool show, int left)
    {
        wishes.gameObject.SetActive(show);
        if (!show) return;
        StartCoroutine(FadeIn(wishes));
        wishTitle.text = left < 0 ? "Kies een wens (oneindig!)" : left == 1 ? "Nog 1 wens!" : $"Kies een wens ({left} over)";
    }

    // Keuzelijst; onPick krijgt index of -1.
    public void ShowChoices(string title, string[] labels, string[] subtitles, Action<int> onPick)
    {
        foreach (Transform child in choiceList) Destroy(child.gameObject);
        choiceTitle.text = title;
        choiceCallback = onPick;
        bool twoColumns = labels.Length > 4;
        RectTransform row = null;
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            Transform parent = choiceList;
            if (twoColumns)
            {
                if (i % 2 == 0) row = Row(choiceList, 120f, 14f);
                parent = row;
            }
            string label = subtitles != null && i < subtitles.Length && !string.IsNullOrEmpty(subtitles[i])
                ? $"{labels[i]}\n<size=60%><i>{subtitles[i]}</i></size>" : labels[i];
            MakeButton(parent, label, () => PickChoice(idx), new Color(0.55f, 0.25f, 0.8f), Color.white, twoColumns ? 120 : 125, 40);
        }
        wishes.gameObject.SetActive(false);
        choices.gameObject.SetActive(true);
        StartCoroutine(FadeIn(choices));
    }

    public void HideChoices()
    {
        choiceCallback = null;
        choices.gameObject.SetActive(false);
    }

    private void PickChoice(int index)
    {
        choices.gameObject.SetActive(false);
        var cb = choiceCallback;
        choiceCallback = null;
        cb?.Invoke(index);
    }

    public void SetDifficulty(string name, string description)
    {
        difficultyLabel.text = "DOOLHOF: " + name.ToUpper();
        difficultyDesc.text = description;
    }

    public void ShowLost(bool show)
    {
        if (lost.gameObject.activeSelf != show) lost.gameObject.SetActive(show);
    }

    public void ShowEnd(string stats) => ShowEnd("DE GEEST\nIS VRIJ!", stats);

    public void ShowEnd(string title, string stats)
    {
        endTitle.text = title;
        SetPanels(end);
        menuBackground.SetActive(true);
        endStats.text = stats;
        StartCoroutine(FadeIn(end));
    }

    // ================================================================== Intern

    private void SetPanels(RectTransform active)
    {
        foreach (var p in new[] { menu, scan, hud, end })
            if (p != null) p.gameObject.SetActive(p == active);
        if (wishes != null) wishes.gameObject.SetActive(false);
        if (choices != null) choices.gameObject.SetActive(false);
        if (battlePanel != null) battlePanel.gameObject.SetActive(false);
    }

    private void Update()
    {
        AnimateHearts();
        if (waitingTap)
        {
            // hint knippert pas als de tekst helemaal staat
            bool done = typing == null;
            speechHint.alpha = done ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) OnSpeechTap();
        }
        UpdateLayout(false);

        if (menuBackground.activeSelf)
        {
            for (int i = 0; i < stars.Count; i++)
            {
                var c = stars[i].color;
                c.a = 0.25f + 0.55f * Mathf.PerlinNoise(Time.unscaledTime * 0.8f + i * 1.7f, i);
                stars[i].color = c;
            }
            if (menuTitle != null && menuTitle.gameObject.activeInHierarchy)
            {
                if (!titleStyled) StyleTitle();
                menuTitle.rectTransform.localScale = Vector3.one * (1f + 0.015f * Mathf.Sin(Time.unscaledTime * 1.6f));
            }
        }
    }

    private void ToggleSound()
    {
        if (sfx == null) return;
        sfx.muted = !sfx.muted;
        if (sfx.muted) sfx.StopLoops();
        soundLabel.text = sfx.muted ? "GELUID: UIT" : "GELUID: AAN";
        Toast(sfx.muted ? "Geluid uit" : "Geluid aan", 1f);
    }

    private IEnumerator ToastRoutine(string text, float duration)
    {
        toastText.text = text;
        var g = toastRt.GetComponent<CanvasGroup>();
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.2f)
        {
            g.alpha = t;
            toastRt.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, t);
            yield return null;
        }
        g.alpha = 1f;
        toastRt.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(duration);
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.3f) { g.alpha = t; yield return null; }
        g.alpha = 0f;
    }

    private IEnumerator Type(string text)
    {
        speechText.text = text;
        speechText.maxVisibleCharacters = 0;
        for (int i = 1; i <= text.Length; i++)
        {
            speechText.maxVisibleCharacters = i;
            if (sfx != null && i % 5 == 0 && char.IsLetter(text[i - 1])) sfx.Play("talk", 0.15f, UnityEngine.Random.Range(0.9f, 1.2f));
            yield return new WaitForSecondsRealtime(0.025f);
        }
        speechText.maxVisibleCharacters = 99999;
        typing = null;
    }

    private IEnumerator FadeIn(RectTransform panel)
    {
        var group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            group.alpha = k;
            panel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, k);
            yield return null;
        }
        group.alpha = 1f;
        panel.localScale = Vector3.one;
    }

    private void StyleTitle()
    {
        titleStyled = true;
        Material mat = menuTitle.fontMaterial;
        if (mat == null) return;
        mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(1f, 0.6f, 0.1f, 0.6f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.4f);
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.85f);
        menuTitle.UpdateMeshPadding();
    }

    // ================================================================== Responsive

    private void UpdateLayout(bool force)
    {
        var size = new Vector2Int(UnityEngine.Screen.width, UnityEngine.Screen.height);
        Rect safe = UnityEngine.Screen.safeArea;
        if (!force && size == lastScreen && safe == lastSafe)
        {
            column.sizeDelta = new Vector2(Mathf.Min(safeRoot.rect.width, maxColumnWidth), 0f);
            return;
        }
        lastScreen = size; lastSafe = safe;
        float aspect = (float)Mathf.Max(1, size.x) / Mathf.Max(1, size.y);
        scaler.matchWidthOrHeight = aspect > 1080f / 1920f ? 1f : 0f;
        float w = Mathf.Max(1, size.x), h = Mathf.Max(1, size.y);
        safeRoot.anchorMin = new Vector2(safe.xMin / w, safe.yMin / h);
        safeRoot.anchorMax = new Vector2(safe.xMax / w, safe.yMax / h);
        safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        column.sizeDelta = new Vector2(Mathf.Min(safeRoot.rect.width, maxColumnWidth), 0f);
    }

    // ================================================================== Bouwstenen

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    private RectTransform MakeScreen(string name)
    {
        var rt = NewRect(name, column);
        Stretch(rt);
        VLayout(rt, 20, new RectOffset(70, 70, 70, 60), TextAnchor.UpperCenter);
        rt.gameObject.SetActive(false);
        return rt;
    }

    private static void VLayout(RectTransform rt, float spacing, RectOffset pad, TextAnchor align)
    {
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing; v.padding = pad; v.childAlignment = align;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
    }

    private static RectTransform Row(Transform parent, float height, float spacing)
    {
        var rt = NewRect("Row", parent);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = true; h.childForceExpandHeight = true;
        SetHeight(rt, height);
        return rt;
    }

    private static void SetHeight(RectTransform rt, float h)
    {
        var le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = h; le.preferredHeight = h;
    }

    private static void Space(Transform parent, float h) => SetHeight(NewRect("Space", parent), h);

    private static void Flex(Transform parent) => NewRect("Flex", parent).gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

    private static void Fixed(TMP_Text t, float height, float minFont)
    {
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = minFont;
        SetHeight(t.rectTransform, height);
    }

    private static TMP_Text MakeText(Transform parent, string text, float size, Color color,
                                 TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
    {
        var rt = NewRect("Text", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    private static Image Rounded(Image img)
    {
        img.sprite = UIStyle.Rounded;
        img.type = Image.Type.Sliced;
        return img;
    }

    private Button MakeButton(Transform parent, string label, Action onClick, Color bg, Color fg, float height, float fontSize)
    {
        var rt = NewRect("Button", parent);
        var img = Rounded(rt.gameObject.AddComponent<Image>());
        img.color = bg;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        b.colors = colors;
        SetHeight(rt, height);
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(0f, -6f);
        var t = MakeText(rt, label, fontSize, fg, TextAlignmentOptions.Center, FontStyles.Bold);
        t.enableAutoSizing = true; t.fontSizeMax = fontSize; t.fontSizeMin = fontSize * 0.5f;
        Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(20f, 0f);
        t.rectTransform.offsetMax = new Vector2(-20f, 0f);
        b.onClick.AddListener(() =>
        {
            if (sfx != null) sfx.Play("bump", 0.4f, 1.8f);
            StartCoroutine(Punch(rt));
            onClick?.Invoke();
        });
        return b;
    }

    private static TMP_Text LabelOf(Button b) => b.GetComponentInChildren<TMP_Text>(true);

    private IEnumerator Punch(RectTransform rt)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
        {
            rt.localScale = Vector3.one * (1f - 0.06f * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // Hartjes-animatie.
    private void AnimateHearts()
    {
        if (battlePanel == null || !battlePanel.gameObject.activeSelf) return;
        float dt = Time.unscaledDeltaTime;
        // waarschuwing pulseert; rode rand zacht aan/uit
        if (battleWarning != null && battleWarning.text.Length > 0)
            battleWarning.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f)));
        if (dangerEdge != null)
        {
            float target = dangerOn ? 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 14f) : 0f;
            var c = dangerEdge.color;
            c.a = Mathf.Lerp(c.a, target, 1f - Mathf.Exp(-dt * 14f));
            dangerEdge.color = c;
        }
        for (int i = 0; i < hearts.Count; i++)
        {
            var rt = hearts[i].rectTransform;
            float target = 1f;
            if (shownHearts == 1 && i == 0)
            {
                // hartslag: twee snelle tikjes per seconde
                float t = Time.unscaledTime % 0.9f;
                target = 1f + 0.18f * Mathf.Exp(-t * 18f) + 0.12f * Mathf.Exp(-Mathf.Max(0f, t - 0.22f) * 18f) * (t > 0.22f ? 1f : 0f);
            }
            rt.localScale = Vector3.one * Mathf.Lerp(rt.localScale.x, target, 1f - Mathf.Exp(-dt * 12f));
        }
    }

    // Hartjestextuur.
    private static Sprite HeartSprite()
    {
        if (heartSprite != null) return heartSprite;
        const int s = 128, ss = 4;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "Heart" };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = ((x + (sx + 0.5f) / ss) / s - 0.5f) * 2.6f;
                        float v = ((y + (sy + 0.5f) / ss) / s - 0.5f) * 2.6f + 0.1f;
                        if (!InHeart(u, v)) continue;
                        bool inner = InHeart(u / 0.88f, (v - 0.05f) / 0.88f + 0.05f);
                        float t = (v + 1f) / 2.3f;
                        Color c = inner ? new Color(0.85f + 0.15f * t, 0.12f + 0.2f * t, 0.2f + 0.15f * t) : new Color(0.45f, 0.03f, 0.1f);
                        float hx = (u + 0.48f) / 0.24f, hy = (v - 0.55f) / 0.15f;
                        if (inner && hx * hx + hy * hy < 1f) c = new Color(1f, 0.85f, 0.88f);
                        r += c.r; g += c.g; b += c.b; a += 1f;
                    }
                px[y * s + x] = a > 0f
                    ? new Color32((byte)(r / a * 255f), (byte)(g / a * 255f), (byte)(b / a * 255f), (byte)(a / (ss * ss) * 255f))
                    : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        heartSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return heartSprite;
    }

    // Vignet: transparant midden, dekkende rand.
    private static Sprite VignetteSprite()
    {
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "Vignette", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / s - 0.5f) * 2f, v = Mathf.Abs((y + 0.5f) / s - 0.5f) * 2f;
                float d = Mathf.Max(u, v) * 0.6f + Mathf.Sqrt(u * u + v * v) * 0.4f;
                float a = Mathf.Clamp01((d - 0.62f) / 0.4f);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }

    // Klassieke hartkromme: (x² + y² - 1)³ - x²·y³ <= 0
    private static bool InHeart(float x, float y)
    {
        float q = x * x + y * y - 1f;
        return q * q * q - x * x * y * y * y <= 0f;
    }

    private static Sprite ArrowSprite()
    {
        int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "Arrow" };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s - 0.5f, v = (y + 0.5f) / s;
                bool head = v > 0.5f && Mathf.Abs(u) < (1f - v) * 0.95f;
                bool shaft = v <= 0.52f && v > 0.08f && Mathf.Abs(u) < 0.13f;
                px[y * s + x] = head || shaft ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }

    // EventSystem voor het nieuwe Input System.
    private static void EnsureEventSystem()
    {
        var es = FindFirstObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
        var old = es.GetComponent<StandaloneInputModule>();
        if (old != null) Destroy(old);
        if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
    }
}
