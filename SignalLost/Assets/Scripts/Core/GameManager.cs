using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { Menu, Loading, Intro, Playing, Riddle, Fragment, Ended }

// Stuurt het spelverloop, de klok, beloningen en de shop.
public class GameManager : MonoBehaviour
{
    [Header("Tijd")]
    [Tooltip("Gemiddelde wandelsnelheid (m/s) om de klok te berekenen")]
    public float walkingSpeed = 1.1f;
    [Tooltip("Straten lopen niet in vogelvlucht: afstand × deze factor")]
    public float detourFactor = 1.4f;
    public float secondsPerRiddle = 45f;
    public float correctBonusSeconds = 10f;
    public float skipPenaltySeconds = 60f;

    [Header("Storingsvelden")]
    public float dangerEnterPenalty = 10f;
    [Tooltip("Extra seconden verlies per seconde in een storingsveld")]
    public float dangerDrainPerSecond = 2f;

    [Header("GPS")]
    public float gpsWaitTimeout = 30f;

    public GameState State { get; private set; } = GameState.Menu;
    public float TimeLeft { get; private set; }
    public RadioDefinition Radio { get; private set; }

    private PlayerLocation player;
    private WaypointManager waypoints;
    private SignalTracker tracker;
    private SignalAudio sfx;
    private DangerZoneManager zones;
    private RouteBuilder routeBuilder;
    private ARWorld arWorld;
    private GameUI ui;
    private RadioDevice radio;

    private int length;
    private Difficulty difficulty;
    private List<Riddle> riddles;
    private List<string> fragments;
    private StoryDefinition story;
    private string requestedStoryId;
    private float baseTrackerRange;
    private int wrongAnswers;
    private int skipped;

    void Awake()
    {
        // Elk onderdeel staat op zijn eigen GameObject in de scene (zoals in de workshop)
        player = Require<PlayerLocation>("PlayerLocation");
        waypoints = Require<WaypointManager>("RouteService");
        routeBuilder = Require<RouteBuilder>("RouteService");
        tracker = Require<SignalTracker>("NavigationService");
        zones = Require<DangerZoneManager>("NavigationService");
        sfx = Require<SignalAudio>("AudioManager");
        arWorld = Require<ARWorld>("ARWorld");
        ui = Require<GameUI>("UI");
        radio = FindFirstObjectByType<RadioDevice>(FindObjectsInactive.Include); // optioneel
    }

    private T Require<T>(string expectedObject) where T : Component
    {
        T found = FindFirstObjectByType<T>();
        if (found == null)
            Debug.LogError($"[GameManager] {typeof(T).Name} ontbreekt! Zet het op het object '{expectedObject}'.");
        return found;
    }

    void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep; // scherm niet laten uitgaan tijdens het wandelen
        Radio = RadioCatalog.Get(SaveSystem.SelectedRadioId);
        AudioListener.volume = SaveSystem.AudioOn ? 1f : 0f;

        if (ui == null)
        {
            Debug.LogError("[GameManager] Geen GameUI gevonden: maak een object 'UI' en zet het script GameUI erop.");
            return;
        }

        // Eerst de UI bouwen, zodat je altijd iets ziet (ook als er iets ontbreekt)
        ui.game = this;
        ui.Build(Radio);

        string missing = MissingComponents();
        if (missing.Length > 0)
        {
            ui.ShowMenu("Ontbrekende onderdelen in de scene:\n" + missing);
            return;
        }

        waypoints.autoAdvanceForTesting = false;
        waypoints.SetRoute(new List<Waypoint>());
        baseTrackerRange = tracker.maxRange;

        zones.Entered += OnDangerEntered;
        arWorld.BeaconTapped += OnTunePressed;

        ApplyRadio();
        ui.ShowMenu();
    }

    private string MissingComponents()
    {
        var list = new List<string>();
        if (player == null) list.Add("PlayerLocation");
        if (waypoints == null) list.Add("WaypointManager");
        if (routeBuilder == null) list.Add("RouteBuilder");
        if (tracker == null) list.Add("SignalTracker");
        if (zones == null) list.Add("DangerZoneManager");
        if (sfx == null) list.Add("SignalAudio");
        if (arWorld == null) list.Add("ARWorld");
        return string.Join(", ", list);
    }

    private void ApplyRadio()
    {
        tracker.maxRange = baseTrackerRange * Radio.rangeMultiplier;
        sfx.ApplyRadio(Radio);
        arWorld.SetBeaconColor(Radio.primary);
        if (radio != null) radio.ApplyRadio(Radio);
    }

    // ================================================================ Verhaal starten

    // storyId: null = willekeurig verhaal.
    public void StartStory(int storyLength, Difficulty storyDifficulty, string storyId = null)
    {
        if (State == GameState.Loading) return;
        if (MissingComponents().Length > 0) { ui.ShowMenu("Ontbrekende onderdelen:\n" + MissingComponents()); return; }
        StopAllCoroutines();
        ResetRun();
        length = storyLength;
        requestedStoryId = storyId;
        difficulty = storyDifficulty;
        StartCoroutine(StartStoryRoutine());
    }

    private IEnumerator StartStoryRoutine()
    {
        State = GameState.Loading;
        ui.ShowLoading("Je locatie bepalen...");

        float waited = 0f;
        while (!player.IsReady && player.Error == PlayerLocation.LocationError.None && waited < gpsWaitTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (!player.IsReady)
        {
            State = GameState.Menu;
            string reason = player.Error == PlayerLocation.LocationError.None ? "time-out" : player.Error.ToString();
            ui.ShowMenu($"Geen GPS-signaal ({reason}).\nZet locatie aan en probeer opnieuw.");
            yield break;
        }

        ui.SetLoadingStatus("Zendmasten in de buurt opsporen...\n(openbare wegen en plekken via OpenStreetMap)");

        RouteResult result = null;
        routeBuilder.statusCallback = msg => ui.SetLoadingStatus(msg);
        yield return routeBuilder.Build(player.Latitude, player.Longitude, length, difficulty, r => result = r);

        if (result == null || result.failed)
        {
            State = GameState.Menu;
            ui.ShowMenu(result != null ? result.error : "Er ging iets mis bij het maken van de route.");
            yield break;
        }

        waypoints.network = result.network;
        waypoints.SetRoute(result.waypoints);
        zones.SetZones(result.zones);
        story = StoryData.All.Find(st => st.id == requestedStoryId) ?? StoryData.Random(SaveSystem.LastStoryId);
        SaveSystem.LastStoryId = story.id;
        fragments = story.GetFragments(length);
        riddles = RiddleBank.Pick(difficulty, length, story.id);
        TimeLeft = ComputeTime(result.routeLength * (result.walkingDistance ? 1.1f : detourFactor));

        string source = result.usedRealPlaces
            ? "Alle masten staan op openbare wegen en paden"
            : result.walkingDistance ? "Masten op straten en wandelpaden" : "Testroute (OpenStreetMap staat uit)";
        if (result.usedCache) source += "\n(kaart uit geheugen: geen internet nodig)";
        string info =
            $"{length} zendmasten  |  ±{result.routeLength * (result.walkingDistance ? 1f : detourFactor):F0} m wandelen\n" +
            $"{result.zones.Count} storingsvelden  |  tijd: {FormatTime(TimeLeft)}\n" +
            $"{source}";

        State = GameState.Intro;
        ui.ShowIntro(story.title, story.intro, info, BeginPlaying);
    }

    private float ComputeTime(float routeLength)
    {
        float walk = routeLength / walkingSpeed; // routeLength is al de (geschatte) wandelafstand
        float t = walk + length * secondsPerRiddle;
        float mult = difficulty == Difficulty.Easy ? 1.6f : difficulty == Difficulty.Medium ? 1.3f : 1.05f;
        t = t * mult + Radio.extraSeconds;
        return Mathf.Ceil(t / 30f) * 30f; // afronden op halve minuten
    }

    private void BeginPlaying()
    {
        arWorld.showDangerZones = difficulty != Difficulty.Hard; // op moeilijk zie je ze niet in AR
        arWorld.Calibrate();
        zones.active = true;
        arWorld.active = true;
        State = GameState.Playing;
        ui.ShowHUD();
    }

    // ================================================================ Tijdens het spel

    void Update()
    {
        if (State == GameState.Playing || State == GameState.Riddle)
        {
            TimeLeft -= Time.deltaTime;
            if (State == GameState.Playing && zones.IsInside)
                TimeLeft -= dangerDrainPerSecond * Radio.dangerPenaltyMultiplier * Time.deltaTime;

            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                EndGame(false);
                return;
            }
        }

        if (State == GameState.Playing || State == GameState.Riddle || State == GameState.Fragment)
        {
            ui.UpdateHUD(TimeLeft, waypoints.CurrentIndex, waypoints.route.Count, CurrentHint(),
                         zones.IsInside, zones.IsNear, CanTune);
        }
    }

    private bool CanTune =>
        State == GameState.Playing && waypoints.Current != null && waypoints.HasReachedCurrent &&
        waypoints.DistanceToCurrent <= waypoints.Current.reachRadius * 1.5f;

    // Hoeveel info je krijgt hangt af van de moeilijkheid.
    private string CurrentHint()
    {
        Waypoint w = waypoints.Current;
        if (w == null) return "";
        switch (difficulty)
        {
            case Difficulty.Easy: return w.name;
            case Difficulty.Medium: return "ergens bij een " + w.poiKind;
            default: return "locatie onbekend";
        }
    }

    private void OnDangerEntered()
    {
        if (State != GameState.Playing) return;
        TimeLeft -= dangerEnterPenalty * Radio.dangerPenaltyMultiplier;
        sfx.PlayAlarm();
        Haptics.Vibrate();
    }

    public void OnTunePressed()
    {
        if (!CanTune) return;
        State = GameState.Riddle;
        int i = waypoints.CurrentIndex;
        ui.ShowRiddle(riddles[i], i + 1, length, waypoints.Current.name, StoryRiddles.RandomIntro(story.id), OnAnswer);
    }

    private void OnAnswer(int choice)
    {
        if (State != GameState.Riddle) return;
        Riddle r = riddles[waypoints.CurrentIndex];

        if (choice == r.correctIndex)
        {
            TimeLeft += correctBonusSeconds;
            sfx.PlaySuccess();
            ui.MarkAnswer(choice, true, $"Juist! Fragment ontcijferd. +{correctBonusSeconds:F0} s");
            StartCoroutine(AfterDelay(1.0f, () => ShowFragment(false)));
        }
        else
        {
            float penalty = difficulty == Difficulty.Easy ? 10f : difficulty == Difficulty.Medium ? 20f : 30f;
            TimeLeft -= penalty;
            wrongAnswers++;
            sfx.PlayError();
            Haptics.Vibrate();
            ui.MarkAnswer(choice, false, $"Fout! Het signaal hapert. -{penalty:F0} s");
        }
    }

    // Mast overslaan, kost tijd.
    public void SkipMast()
    {
        if (State != GameState.Playing || waypoints.IsRouteComplete) return;
        TimeLeft -= skipPenaltySeconds;
        skipped++;
        sfx.PlayError();
        if (TimeLeft <= 0f) { TimeLeft = 0f; EndGame(false); return; }
        ShowFragment(true);
    }

    private void ShowFragment(bool damaged)
    {
        State = GameState.Fragment;
        int i = waypoints.CurrentIndex;
        string text = fragments[i];
        if (damaged) text = "[zwak signaal - mast overgeslagen]\n" + text;
        ui.ShowFragment(story.fragmentLabel, i + 1, length, text, OnFragmentDone);
    }

    private void OnFragmentDone()
    {
        if (State != GameState.Fragment) return;
        waypoints.Advance();
        if (waypoints.IsRouteComplete)
        {
            EndGame(true);
        }
        else
        {
            State = GameState.Playing;
            ui.ShowHUD();
        }
    }

    public void Calibrate()
    {
        arWorld.Calibrate();
        sfx.PlayClick();
    }

    // ================================================================ Einde

    private void EndGame(bool win)
    {
        if (State == GameState.Ended) return;
        State = GameState.Ended;
        zones.active = false;
        arWorld.active = false;

        int found = waypoints.CurrentIndex;
        string stats;

        if (win)
        {
            int earned = CalculateReward();
            SaveSystem.Parts += earned;
            SaveSystem.StoriesCompleted++;
            SaveSystem.MarkStoryCompleted(story.id);
            sfx.PlayWin();
            stats =
                $"Tijd over: {FormatTime(TimeLeft)}\n" +
                $"Foute antwoorden: {wrongAnswers}   Overgeslagen: {skipped}\n\n" +
                $"<size=130%><color=#{ColorUtility.ToHtmlStringRGB(Radio.primary)}>+{earned} radio-onderdelen</color></size>\n" +
                $"Totaal: {SaveSystem.Parts}";
        }
        else
        {
            sfx.PlayFail();
            stats = $"Masten gevonden: {found}/{length}\nGeen onderdelen verdiend.";
        }

        ui.ShowEnd(win ? story.winTitle : story.loseTitle, win, win ? story.finale : story.failure, stats);
        waypoints.SetRoute(new List<Waypoint>());
        zones.Clear();
        arWorld.ClearAll();
    }

    private float DifficultyRewardMultiplier =>
        difficulty == Difficulty.Easy ? 1f : difficulty == Difficulty.Medium ? 1.5f : 2f;

    private int CalculateReward()
    {
        float baseParts = length * 5f * DifficultyRewardMultiplier;
        float timeBonus = Mathf.Floor(TimeLeft / 60f); // 1 onderdeel per volle minuut over
        float total = (baseParts + timeBonus - skipped * 3f) * Radio.partsMultiplier;
        return Mathf.Max(5, Mathf.RoundToInt(total));
    }

    public int EstimateReward(int storyLength, Difficulty d)
    {
        float mult = d == Difficulty.Easy ? 1f : d == Difficulty.Medium ? 1.5f : 2f;
        return Mathf.RoundToInt(storyLength * 5f * mult * (Radio != null ? Radio.partsMultiplier : 1f));
    }

    public void StopToMenu()
    {
        StopAllCoroutines();
        ResetRun();
        ui.ShowMenu();
    }

    private void ResetRun()
    {
        State = GameState.Menu;
        waypoints.SetRoute(new List<Waypoint>());
        zones.Clear();
        zones.active = false;
        arWorld.active = false;
        arWorld.ClearAll();
        wrongAnswers = 0;
        skipped = 0;
    }

    // ================================================================ Menu-acties

    public void ToggleAudio()
    {
        SaveSystem.AudioOn = !SaveSystem.AudioOn;
        AudioListener.volume = SaveSystem.AudioOn ? 1f : 0f;
    }

    public void ToggleVibration()
    {
        SaveSystem.VibrationOn = !SaveSystem.VibrationOn;
        if (SaveSystem.VibrationOn) Haptics.Vibrate();
    }

    public void BuyOrEquip(RadioDefinition radio)
    {
        if (!SaveSystem.OwnsRadio(radio.id))
        {
            if (SaveSystem.Parts < radio.price) { sfx.PlayError(); return; }
            SaveSystem.Parts -= radio.price;
            SaveSystem.AddOwnedRadio(radio.id);
        }

        SaveSystem.SelectedRadioId = radio.id;
        Radio = radio;
        ApplyRadio();
        sfx.PlaySuccess();

        ui.Build(Radio); // hele UI opnieuw in de kleuren van de nieuwe radio
        ui.ShowShop();
    }

    public void PlayClick()
    {
        if (sfx == null) return;
        if (sfx != null) sfx.PlayClick();
    }

    // ================================================================ Hulpjes

    public static string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }

    private IEnumerator AfterDelay(float seconds, System.Action action)
    {
        yield return new WaitForSeconds(seconds);
        action?.Invoke();
    }

    [ContextMenu("Reset opgeslagen voortgang")]
    private void ResetSave()
    {
        SaveSystem.ResetAll();
        Debug.Log("Voortgang gewist.");
    }

    [ContextMenu("Geef 1000 onderdelen (test)")]
    private void CheatParts()
    {
        SaveSystem.Parts += 1000;
        Debug.Log("Onderdelen: " + SaveSystem.Parts);
    }
}
