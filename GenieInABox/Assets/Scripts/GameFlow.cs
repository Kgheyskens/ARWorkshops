using System.Collections;
using UnityEngine;

// State machine die de fases regelt.
public class GameFlow : MonoBehaviour
{
    private enum State { Menu, Scan, Dust, Puzzle, Unlock, Maze, Escape, Wishes, Farewell, End, Lost, Battle, BattleWon }

    private State state = State.Menu;
    private GenieBoxRig rig;
    private DustCover dust;
    private KeyPuzzle puzzle;
    private HollowBox box;
    private MazeGame maze;
    private Genie genie;
    private WishEffects wishFx;
    private MicBlow mic;
    private TiltInput tilt;
    private SoundFx sfx;
    private GameUI ui;
    private SecretPortal portal;
    private GuardBattle battle;

    private int mazesSolved, wishesLeft = 3, totalFalls;
    private MazeDifficulty difficulty = MazeDifficulty.Normaal;
    private float totalTime, stateTime;
    private bool busy, firstEscape = true, secretDone;
    private bool infiniteWishes; // na de wens "Reuzengeest"
    private bool freeRoam;       // feniks / reuzengeest: je mag rondkijken zonder dat het spel pauzeert
    private Vector3 battleUp = Vector3.up;

    private void Start()
    {
        rig = Require<GenieBoxRig>();
        dust = Require<DustCover>();
        puzzle = Require<KeyPuzzle>();
        box = Require<HollowBox>();
        maze = Require<MazeGame>();
        genie = Require<Genie>();
        wishFx = Require<WishEffects>();
        mic = Require<MicBlow>();
        tilt = Require<TiltInput>();
        sfx = Require<SoundFx>();
        ui = Require<GameUI>();
        portal = Require<SecretPortal>();
        battle = Require<GuardBattle>();
        if (ui == null) return;

        if (portal != null) portal.Entered += OnPortalEntered;
        if (battle != null)
        {
            battle.Speech += t => ui.SayAs("DE WACHTER", t);
            battle.PlayerHit += () => ui.Flash(new Color(1f, 0.1f, 0.1f, 0.5f), 0.45f);
            battle.Finished += win => StartCoroutine(AfterBattle(win));
        }

        ui.StartPressed += OnStart;
        ui.RestartPressed += OnRestart;
        ui.MenuPressed += OnMenu;
        ui.WishPressed += OnWish;
        ui.DifficultyPressed += OnDifficulty;
        difficulty = (MazeDifficulty)Mathf.Clamp(PlayerPrefs.GetInt("mazeDifficulty", 1), 0, 3);
        ui.SetDifficulty(difficulty.ToString(), MazeGame.Describe(difficulty));
        if (genie != null) genie.Speech += ui.Say;
        if (maze != null) maze.Toast += t => ui.Toast(t);

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        if (sfx != null) sfx.SetMusic(1f);
    }

    // Zoekt een onderdeel of maakt het aan.
    private T Require<T>() where T : Component
    {
        var c = FindFirstObjectByType<T>();
        if (c != null) return c;

        bool boxPart = typeof(T) == typeof(DustCover) || typeof(T) == typeof(KeyPuzzle) || typeof(T) == typeof(HollowBox)
                    || typeof(T) == typeof(MazeGame) || typeof(T) == typeof(Genie) || typeof(T) == typeof(WishEffects);
        if (boxPart && rig == null)
        {
            Debug.LogError($"[GameFlow] {typeof(T).Name} ontbreekt en er is geen GenieBoxRig om het onder te hangen.");
            return null;
        }
        if (typeof(T) == typeof(GenieBoxRig))
        {
            Debug.LogError("[GameFlow] GenieBoxRig ontbreekt: hang een object met GenieBoxRig onder je Multi Target.");
            return null;
        }
        var go = new GameObject(typeof(T).Name);
        if (boxPart) go.transform.SetParent(rig.transform, false);
        Debug.LogWarning($"[GameFlow] {typeof(T).Name} ontbrak in de scène en is automatisch aangemaakt.");
        return go.AddComponent<T>();
    }

    // ================================================================== Elk frame

    private void Update()
    {
        if (ui == null) return;
        stateTime += Time.deltaTime;

        // Doos kwijt? Dan alles pauzeren (behalve in menu's)
        // In het Djinn-rijk (of op weg naar het portaal) kijk je weg van de doos: daar niet pauzeren
        bool awayFromBox = freeRoam || state == State.Battle || state == State.BattleWon || (state == State.Wishes && portal != null && portal.IsOpen);
        bool inGame = state != State.Menu && state != State.End && state != State.Scan && !awayFromBox;
        bool tracked = rig != null && rig.IsTracked;
        Time.timeScale = inGame && !tracked ? 0f : 1f;
        ui.ShowLost(inGame && !tracked);
        if (inGame && !tracked && sfx != null) sfx.StopLoops();

        switch (state)
        {
            case State.Scan:
                if (tracked) BeginDust();
                break;

            case State.Dust:
                puzzle.CheckReleases(dust);
                ui.SetAim(!dust.Finished, dust.AimOnBox);
                ui.SetMeter(true, "BLAASKRACHT", mic.Strength, $"Stof weg: {dust.Cleared * 100f:0}%");
                ui.SetStatus($"Brokstukken gevonden: {puzzle.Released}/{puzzle.Total}");
                if (stateTime > 3f && stateTime < 3.1f && !mic.HasMicrophone)
                    ui.Toast("Geen microfoon gevonden: houd de SPATIEBALK in om te blazen", 4f);
                if (dust.Finished && puzzle.AllReleased) BeginPuzzle();
                break;

            case State.Puzzle:
                // Geen hulp: enkel de brokstukken verkleuren (blauw -> goud) naarmate je juister kijkt
                if (puzzle.Solved) StartCoroutine(UnlockSequence());
                break;

            case State.Maze:
                string partText = maze.PartCount > 1 ? $"   ·   Deel {maze.PartIndex + 1}/{maze.PartCount}" : "";
                string poseHint = tilt != null && tilt.BoxPoseStale ? "\n<size=70%><color=#FFD36B>Houd de hele doos in beeld, anders voelt de game het kantelen niet.</color></size>" : "";
                ui.SetStatus($"Levens {maze.Lives}/{maze.MaxLives}   ·   Lampjes {maze.Lamps}/{MazeGame.LampsNeeded}{partText}   ·   {FormatTime(maze.Elapsed)}{poseHint}");
                if (maze.Escaped) StartCoroutine(EscapeSequence());
                else if (maze.Failed) StartCoroutine(LossSequence());
                break;

            case State.Battle:
                ui.SetBattle(battle.Hearts, battle.GuardHealth01);
                ui.SetBattleWarning(battle.Warning, battle.InDanger);
                break;
        }
    }

    private void SetState(State s)
    {
        state = s;
        stateTime = 0f;
    }

    // ================================================================== Fases

    private void BeginDust()
    {
        SetState(State.Dust);
        string extra = Application.isEditor ? " (Editor: spatiebalk)" : "";
        ui.ShowStep(1, 4, "Blaas het stof weg", "Richt het cirkeltje op de doos en blaas zachtjes in de microfoon van je gsm" + extra + ".");
        dust.Active = true;
        mic.StartListening();
        sfx.SetMusic(0.8f);
        sfx.Play("rumble", 0.6f);
        ui.Toast("Er zit iets in die doos...", 2.5f);
    }

    private void BeginPuzzle()
    {
        SetState(State.Puzzle);
        dust.Active = false;
        mic.StopListening();
        sfx.SetWind(0f);
        ui.ShowStep(2, 4, "Vorm de sleutel", "De brokstukken zweven willekeurig... of niet? Stap rond de doos tot ze samen één sleutel vormen.");
        box.ShowLock(true);
        puzzle.Solving = true;
        ui.Toast("Een slot! Waar is de sleutel?", 2.5f);
    }

    private IEnumerator UnlockSequence()
    {
        SetState(State.Unlock);
        ui.SetGuide(false, Vector3.zero, 0f);
        ui.SetMeter(false, "", 0f, "");
        ui.Toast("De sleutel is heel!", 2f);
        yield return puzzle.AssembleAndInsert(box);
        maze.Build(difficulty);
        yield return box.Open();
        BeginMaze();
    }

    private void BeginMaze()
    {
        SetState(State.Maze);
        string parts = maze.PartCount > 1 ? $" Dit doolhof heeft {maze.PartCount} delen: het portaal brengt je dieper. Val je, dan terug naar deel 1!" : "";
        ui.ShowStep(3, 4, mazesSolved == 0 ? "Bevrijd de geest" : $"Doolhof: {difficulty}",
            "Kantel de doos voorzichtig. Verzamel 3 lampjes en rol de geest naar de uitgang." + parts);
        sfx.SetMusic(0.8f);
        maze.Running = true;
    }

    private IEnumerator EscapeSequence()
    {
        SetState(State.Escape);
        mazesSolved++;
        totalTime += maze.Elapsed;
        totalFalls += maze.Falls;
        string key = "bestTime_" + difficulty;
        int best = PlayerPrefs.GetInt(key, int.MaxValue);
        if (maze.Elapsed < best) PlayerPrefs.SetInt(key, Mathf.RoundToInt(maze.Elapsed));

        ui.ShowStep(4, 4, "De geest ontsnapt!", "");
        yield return maze.BallRise(box.OpeningWorld(0.05f));
        yield return genie.Appear();
        sfx.SetMusic(0.8f);
        bool wasFirst = firstEscape;
        firstEscape = false;
        yield return Talk(wasFirst
            ? "EINDELIJK VRIJ! Duizend jaar zat ik in die cornflakesdoos... Als dank krijg je drie wensen!"
            : "Weer vrij! Jij bent een echte doolhofmeester. Kies je volgende wens!");

        if (maze.FoundStar && !secretDone && !portal.IsOpen)
        {
            yield return Talk("Wacht eens... is dat een STERRENSPLINTER?! Die opent de geheime poort naar het Djinn-rijk... Draai je eens om! Maar pas op voor de Wachter.");
            portal.Open(rig.transform.up, genie.transform.position + rig.transform.up * 0.15f);
            yield return new WaitForSeconds(3f);
        }
        ShowWishes();
    }

    // ================================================================== Verloren

    // Verloren: geest blijft gevangen.
    private IEnumerator LossSequence()
    {
        SetState(State.Lost);
        totalTime += maze.Elapsed;
        totalFalls += maze.Falls;
        ui.ShowStep(4, 4, "Geen levens meer...", "");
        sfx.SetMusic(0.4f);
        yield return new WaitForSeconds(0.8f);
        maze.Clear();
        puzzle.ClearAll();
        yield return box.Close();
        sfx.Play("rumble", 1f);

        if (mazesSolved == 0)
        {
            yield return Talk("Nee... nee, nee, nee. Ik was er zó dichtbij. Weer duizend jaar in deze donkere doos...");
            yield return Talk("Misschien komt er ooit iemand die het wél kan. Dag...");
        }
        else
        {
            ui.Flash(new Color(1f, 0.1f, 0.05f, 0.45f), 0.8f);
            string done = mazesSolved == 1 ? "Eén doolhof" : $"{mazesSolved} doolhoven";
            yield return Talk($"JIJ! Ik WAS al vrij! {done} had je voor me opgelost... en dan moest je zo nodig nog eens GOKKEN?!");
            ui.Flash(new Color(1f, 0.1f, 0.05f, 0.35f), 0.6f);
            sfx.Play("rumble", 0.8f);
            yield return Talk(wishesLeft > 0
                ? $"Nu zit ik hier weer voor DUIZEND JAAR. En je {wishesLeft} {(wishesLeft == 1 ? "wens" : "wensen")}? Die kan je vergeten!"
                : "Nu zit ik hier weer voor DUIZEND JAAR. Bedankt voor niets!");
        }
        ui.HideSpeech();
        SetState(State.End);
        mic.StopListening();
        sfx.UseGameMusic(false);
        sfx.SetMusic(1f);
        ui.ShowEnd(mazesSolved == 0 ? "DE GEEST\nBLIJFT GEVANGEN" : "DE GEEST\nIS WOEDEND",
                   $"Doolhoven opgelost: <b>{mazesSolved}</b>\nKeer in een gat gevallen: <b>{totalFalls}</b>\nTijd in de doolhoven: <b>{FormatTime(totalTime)}</b>\n\n<i>Probeer het nog eens... de geest rekent op je.</i>");
    }

    // ================================================================== Geheim: Djinn-rijk

    private void OnPortalEntered()
    {
        if (state == State.Wishes && !busy) StartCoroutine(EnterDjinnWorld());
        else if (state == State.BattleWon) StartCoroutine(ReturnFromBattle(true));
    }

    private IEnumerator EnterDjinnWorld()
    {
        SetState(State.Battle);
        busy = true;
        battleUp = rig.transform.up;
        ui.ShowWishes(false, wishesLeft);
        ui.HideChoices();
        ui.ShowStep("GEHEIM", "Het Djinn-rijk", "Versla de Wachter!");
        ui.Flash(Color.white, 1.2f);
        SetBoxWorld(false);
        sfx.SetMusic(0.5f);
        battle.Begin(battleUp);
        ui.ShowBattle(true, battle.MaxHearts);
        yield return null;
    }

    private IEnumerator AfterBattle(bool win)
    {
        secretDone = true;
        ui.ShowBattle(false, 0);
        if (win)
        {
            wishesLeft++;
            WishEffects.LegendaryUnlocked = true;
            ui.Toast("De Wachter is verslagen!  +1 wens  +  legendarische makeover", 4f);
            yield return new WaitForSeconds(1.5f);
            SetState(State.BattleWon);
            ui.ShowStep("GEHEIM", "Overwinning!", "Een portaal opent achter je. Loop erdoor om terug te keren naar de geest.");
            portal.Open(battleUp);
        }
        else
        {
            // Verslagen: kiezen tussen opnieuw proberen of vluchten naar de geest
            ui.Flash(new Color(0.6f, 0f, 0.05f, 0.7f), 1.2f);
            ui.ShowStep("GEHEIM", "Verslagen...", "De Wachter was te sterk. Wat doe je?");
            ui.ShowChoices("Je bent verslagen!",
                new[] { "Opnieuw proberen", "Vluchten" },
                new[] { "Vecht nog een keer tegen de Wachter", "Terug naar de geest (je wensen blijven)" },
                pick =>
                {
                    ui.HideChoices();
                    if (pick == 0) StartCoroutine(RetryBattle());
                    else StartCoroutine(ReturnFromBattle(false));
                });
        }
    }

    private IEnumerator RetryBattle()
    {
        ui.Flash(Color.white, 0.8f);
        ui.ShowStep("GEHEIM", "Het Djinn-rijk", "Versla de Wachter!");
        battle.Begin(battleUp);
        ui.ShowBattle(true, battle.MaxHearts);
        yield return null;
    }

    private IEnumerator ReturnFromBattle(bool win)
    {
        ui.Flash(Color.white, 1.2f);
        battle.End();
        portal.Close();
        SetBoxWorld(true);
        ui.ShowBattle(false, 0);
        ui.ShowStep(4, 4, "Terug bij de geest", "");
        sfx.SetMusic(0.8f);
        busy = false;
        yield return Talk(win
            ? "Je hebt de WACHTER verslagen?! Ongelooflijk! Daarvoor krijg je een extra wens... en een heel speciale makeover: het Djinn-paleis!"
            : "Gevlucht voor de Wachter? Hihi, slim van je. Geen zorgen, je wensen heb je nog.");
        if (infiniteWishes || wishesLeft > 0) ShowWishes();
        else StartCoroutine(Farewell());
    }

    // Doos-onderdelen tonen/verbergen.
    private void SetBoxWorld(bool visible)
    {
        if (rig == null) return;
        foreach (Transform child in rig.transform) child.gameObject.SetActive(visible);
    }

    private void ShowWishes()
    {
        SetState(State.Wishes);
        ui.HideSpeech();
        ui.ShowWishes(true, infiniteWishes ? -1 : wishesLeft);
    }

    // Geest praat, wacht op tik.
    private IEnumerator Talk(string text)
    {
        genie.Say(text);
        yield return ui.WaitForTap();
    }

    // Verbruikt een wens.
    private void UseWish()
    {
        if (!infiniteWishes) wishesLeft--;
    }

    private void OnWish(int index)
    {
        if (state != State.Wishes || busy) return;
        switch (index)
        {
            case 0:
                StartCoroutine(DoFireworks());
                break;
            case 1:
                ui.ShowWishes(false, wishesLeft);
                genie.Say("Een makeover? Zeg maar wat je doos moet worden!");
                ui.ShowChoices("Wat wordt je doos?", wishFx.AvailableMakeovers, null, pick =>
                {
                    if (pick < 0) ShowWishes();
                    else StartCoroutine(DoMakeover(pick));
                });
                break;
            case 2:
                ui.ShowWishes(false, wishesLeft);
                genie.Say("Nog een doolhof? Kies maar hoe moeilijk!");
                var names = new string[4];
                var descs = new string[4];
                for (int i = 0; i < 4; i++) { names[i] = ((MazeDifficulty)i).ToString(); descs[i] = MazeGame.Describe((MazeDifficulty)i); }
                ui.ShowChoices("Hoe moeilijk?", names, descs, pick =>
                {
                    if (pick < 0) ShowWishes();
                    else StartCoroutine(NewMaze((MazeDifficulty)pick));
                });
                break;
            case 3: StartCoroutine(DoCarpet()); break;
            case 4: StartCoroutine(DoFortune()); break;
            case 5: StartCoroutine(DoGiant()); break;
            case 6: StartCoroutine(infiniteWishes ? SlapEnding() : LockEnding()); break;
        }
    }

    // ---- Wens: vliegend tapijt
    private IEnumerator DoCarpet()
    {
        busy = true;
        UseWish();
        ui.ShowWishes(false, wishesLeft);
        genie.Cast();
        yield return Talk("Een vliegend tapijt?! Daar heb ik al duizend jaar niet meer op gezeten! Kijk goed rond, ik vlieg overal!");
        freeRoam = true;
        yield return genie.Disappear();
        ui.Toast("Kijk rond en volg het tapijt!", 3f);
        yield return wishFx.CarpetWish(genie);
        StartCoroutine(wishFx.CarpetVanish()); // de geest springt eraf, het tapijt verdwijnt
        yield return genie.Appear();
        freeRoam = false;
        yield return Talk("WIEEEE! Wat een rit! Mijn haar zit helemaal in de war... oh wacht, ik heb geen haar.");
        yield return AfterWish();
    }

    // ---- Wens: waarzegger
    private IEnumerator DoFortune()
    {
        busy = true;
        UseWish();
        ui.ShowWishes(false, wishesLeft);
        genie.Cast();
        StartCoroutine(wishFx.CrystalBall(12.5f));
        yield return Talk("Laat me eens in mijn kristallen bol kijken...");
        yield return Talk("Ooooh... ik zie... ik zie...");
        genie.Cast();
        yield return Talk(wishFx.NextFortune());
        yield return AfterWish();
    }

    // ---- Wens: reuzengeest (daarna: oneindig veel wensen)
    private IEnumerator DoGiant()
    {
        busy = true;
        UseWish();
        ui.ShowWishes(false, wishesLeft);
        bool first = !infiniteWishes;
        yield return Talk(first ? "Groter? GROTER?! Hou je maar vast!" : "Nog eens reuzegroot? Haha, met plezier!");
        freeRoam = true;
        ui.Toast("Kijk omhoog!", 2.5f);
        yield return genie.Resize(8f, 2.8f);
        yield return Talk("WAAAUW! Zo groot ben ik in duizend jaar niet geweest! Ik voel me FANTASTISCH!");
        if (first)
        {
            genie.Cast();
            infiniteWishes = true;
            ui.Flash(new Color(1f, 0.85f, 0.3f, 0.6f), 1f);
            sfx.Play("fanfare", 1f);
            yield return Talk("Weet je wat? Omdat je zo goed voor me bent... krijg je vanaf nu ONEINDIG veel wensen!");
        }
        yield return Talk("Oef, het is wel wat krap hier. Ik maak me weer klein.");
        yield return genie.Resize(1f, 1.6f);
        freeRoam = false;
        yield return AfterWish();
    }

    // ---- "Meer wensen!" (zonder oneindige wensen): de geest sluit zichzelf op
    private IEnumerator LockEnding()
    {
        busy = true;
        ui.ShowWishes(false, wishesLeft);
        ui.HideChoices();
        portal.Close();
        genie.Angry(10f);
        ui.Flash(new Color(1f, 0.1f, 0.05f, 0.5f), 0.8f);
        yield return Talk("MEER wensen?! Ik geef je wensen... en het is nog niet GENOEG?!");
        ui.Flash(new Color(1f, 0.1f, 0.05f, 0.35f), 0.6f);
        yield return Talk("Weet je wat? Dan blijf ik nog liever opgesloten dan een ondankbaar mens te dienen!");
        ui.HideSpeech();
        yield return genie.Disappear();
        if (box.IsOpen) { maze.Clear(); yield return box.Close(); }
        sfx.Play("gate", 1f, 0.8f);
        yield return new WaitForSeconds(1f);
        SetState(State.End);
        mic.StopListening();
        sfx.UseGameMusic(false);
        sfx.SetMusic(1f);
        ui.ShowEnd("DE GEEST SLOOT\nZICHZELF OP",
                   $"<i>\"Liever duizend jaar in een doos dan één wens voor een ondankbaar mens.\"</i>\n\nWensen weggegooid: <b>{wishesLeft}</b>\nDoolhoven opgelost: <b>{mazesSolved}</b>");
    }

    // ---- "Meer wensen!" terwijl je al ONEINDIG veel wensen had: de klap... en het spel sluit af
    private IEnumerator SlapEnding()
    {
        busy = true;
        freeRoam = true;
        ui.ShowWishes(false, wishesLeft);
        ui.HideChoices();
        portal.Close();
        genie.Angry(15f);
        ui.Flash(new Color(1f, 0.05f, 0.05f, 0.6f), 0.8f);
        if (!genie.IsGiant) StartCoroutine(genie.Resize(7f, 1f));
        yield return Talk("MEER?! Ik gaf je ONEINDIG veel wensen... en dat is nog niet GENOEG?!");
        genie.Say("ONDANKBAAR MENS!"); // geen tik: meteen de klap
        yield return genie.Slap(Camera.main);
        sfx.Play("bang", 1f, 0.6f);
        sfx.Play("bump", 1f, 0.5f);
#if UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
        ui.HideSpeech();
        sfx.UseGameMusic(false);
        sfx.SetMusic(0f);
        yield return ui.SlapBlackout();
        yield return new WaitForSecondsRealtime(1.5f);
        QuitGame();
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator DoFireworks()
    {
        busy = true;
        UseWish();
        ui.ShowWishes(false, wishesLeft);
        genie.Cast();
        yield return Talk("Kijk omhoog!");
        ui.HideSpeech();
        yield return wishFx.Fireworks();
        yield return AfterWish();
    }

    private IEnumerator DoMakeover(int skin)
    {
        busy = true;
        UseWish();
        genie.Cast();
        genie.Say("Simsalabim!");
        puzzle.ClearAll();
        if (box.IsOpen) { maze.Clear(); yield return box.Close(); }
        yield return wishFx.Makeover(skin);
        yield return Talk($"Tadaa! Je doos is nu: {wishFx.CurrentSkin}. Mooi, he?");
        yield return AfterWish();
    }

    private IEnumerator AfterWish()
    {
        busy = false;
        if (infiniteWishes || wishesLeft > 0)
        {
            yield return new WaitForSeconds(0.6f);
            ShowWishes();
        }
        else StartCoroutine(Farewell());
    }

    // Nieuw doolhof (gratis).
    private IEnumerator NewMaze(MazeDifficulty d)
    {
        busy = true;
        SetDifficulty(d);
        yield return Talk(d >= MazeDifficulty.Moeilijk ? "Oei... ik verstop me heel diep. Veel succes!" : "Goed, ik verstop me opnieuw!");
        ui.HideSpeech();
        yield return genie.Disappear();
        wishFx.ClearSkin();
        maze.Build(difficulty);
        if (!box.IsOpen) yield return box.Open();
        busy = false;
        BeginMaze();
    }

    private void SetDifficulty(MazeDifficulty d)
    {
        difficulty = d;
        PlayerPrefs.SetInt("mazeDifficulty", (int)d);
        ui.SetDifficulty(d.ToString(), MazeGame.Describe(d));
    }

    private void OnDifficulty()
    {
        SetDifficulty((MazeDifficulty)(((int)difficulty + 1) % 4));
    }

    private IEnumerator Farewell()
    {
        SetState(State.Farewell);
        portal.Close();
        yield return Talk("Al je wensen zijn vervuld. Bedankt om me te bevrijden... Tot het volgende ontbijt!");
        ui.HideSpeech();
        yield return genie.Disappear();
        if (box.IsOpen) { maze.Clear(); yield return box.Close(); }
        ShowEnd();
    }

    private void ShowEnd()
    {
        SetState(State.End);
        mic.StopListening();
        sfx.StopLoops();
        sfx.UseGameMusic(false);
        sfx.SetMusic(1f);
        int best = PlayerPrefs.GetInt("bestTime_" + difficulty, int.MaxValue);
        string bestText = best == int.MaxValue ? "-" : FormatTime(best);
        ui.ShowEnd($"Tijd in de doolhoven: <b>{FormatTime(totalTime)}</b>\nDoolhoven opgelost: <b>{mazesSolved}</b>\nKeer in een gat gevallen: <b>{totalFalls}</b>\nBeste tijd ({difficulty}): <b>{bestText}</b>");
    }

    // ================================================================== Knoppen

    private void OnStart()
    {
        ResetWorld();
        SetState(State.Scan);
        ui.ShowScan();
        sfx.UseGameMusic(true);
        sfx.SetMusic(0.8f);
    }

    private void OnRestart()
    {
        ResetWorld();
        SetState(State.Scan);
        ui.ShowScan();
        sfx.UseGameMusic(true);
        sfx.SetMusic(0.8f);
    }

    private void OnMenu()
    {
        ResetWorld();
        SetState(State.Menu);
        ui.ShowMenu();
        sfx.UseGameMusic(false);
        sfx.SetMusic(1f);
    }

    private void ResetWorld()
    {
        StopAllCoroutines();
        busy = false;
        Time.timeScale = 1f;
        portal.Close();
        battle.End();
        SetBoxWorld(true);
        ui.ShowBattle(false, 0);
        secretDone = false;
        maze.Clear();
        box.ResetBox();
        puzzle.ResetPuzzle();
        dust.ResetDust();
        dust.Active = false;
        genie.Hide();
        wishFx.ClearSkin();
        wishFx.ClearCarpet();
        wishFx.ClearCrystalBall();
        infiniteWishes = false;
        freeRoam = false;
        mic.StopListening();
        sfx.StopLoops();
        ui.HideSpeech();
        mazesSolved = 0;
        wishesLeft = 3;
        totalTime = 0f;
        totalFalls = 0;
        firstEscape = true;
    }

    private static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 60}:{s % 60:00}";
    }

    // ================================================================== Editor-hulp

    [ContextMenu("Maak systeem-objecten aan (Audio, Microfoon, Kantelen, UI, Portaal, Gevecht)")]
    private void CreateSystems()
    {
        Make<SoundFx>("Audio");
        Make<MicBlow>("Microphone");
        Make<TiltInput>("Tilt");
        Make<GameUI>("UI");
        Make<SecretPortal>("SecretPortal");
        Make<GuardBattle>("GuardBattle");
    }

    private static void Make<T>(string name) where T : Component
    {
        if (FindFirstObjectByType<T>() != null) return;
        new GameObject(name).AddComponent<T>();
    }
}
