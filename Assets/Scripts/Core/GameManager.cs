using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Runs the bakery: the title screen, each workday from opening to closing, money, upgrades and the 3-day campaign.
//
// Flow:  title -> dayIntro -> playing -> dayResults -> shop -> dayIntro (next day) ... -> victory
//        (failing a day goes from dayResults back to dayIntro for the same day)
public class GameManager : MonoBehaviour {
    static public GameManager S { get; private set; }

    public const float BASE_MIX_SECONDS = 1.5f;

    [Header( "Inscribed: Days" )]
    public DaySettings[]    days;
    [Tooltip( "Real seconds from opening (8 AM) to closing (4 PM)" )]
    public float            dayLengthSeconds = 150f;
    public float            openHour = 8f;
    public float            lastCallHour = 15.5f;
    public float            closeHour = 16f;
    public float            rushHour = 11.5f;

    [Header( "Inscribed: Shop" )]
    public UpgradeDef[]     upgrades;

    [Header( "Inscribed: Scene references" )]
    public Baker            baker;
    public Transform        bakerStart;
    public CustomerSpawner  spawner;
    public BakeryCat        cat;
    public Oven             thirdOven;
    public IngredientCrate  berryCrate;
    public MenuScreens      menus;

    [Header( "Dynamic" )]
    public eGameState       state = eGameState.title;
    public int              dayIndex;
    public int              savings;
    public float            dayTimer;
    public int              streak;
    public DayStats         stats = new DayStats();
    public List<DayStats>   history = new List<DayStats>();
    public bool             lastDayPassed;

    private HashSet<eUpgrade> owned = new HashSet<eUpgrade>();
    private bool            rushAnnounced, lastCallAnnounced, closingAnnounced;
    private float           emptyShopTimer;
    private float           inputGraceUntil;
    private Station[]       stations;

    // ------------------------------------------------------------------ handy properties

    public bool         IsPlaying   { get { return state == eGameState.playing; } }
    public DaySettings  Day         { get { return days[Mathf.Clamp( dayIndex, 0, days.Length - 1 )]; } }
    public bool         IsLastDay   { get { return dayIndex >= days.Length - 1; } }
    public float        Hour        { get { return Mathf.Min( closeHour, openHour + ( closeHour - openHour ) * dayTimer / dayLengthSeconds ); } }
    public float        Day01       { get { return Mathf.Clamp01( dayTimer / dayLengthSeconds ); } }
    public bool         DoorsOpen   { get { return Hour < lastCallHour; } }
    public bool         IsRush      { get { return Hour >= rushHour && Hour < 13.25f; } }
    public bool         InputGraceActive { get { return Time.unscaledTime < inputGraceUntil; } }

    // Upgrade effects
    public bool  Has( eUpgrade u )     { return owned.Contains( u ); }
    public float OvenSpeed             { get { return Has( eUpgrade.turboOven ) ? 1.35f : 1f; } }
    public float MixSeconds            { get { return Has( eUpgrade.quickWhisk ) ? BASE_MIX_SECONDS * 0.5f : BASE_MIX_SECONDS; } }
    public float MoveSpeedMultiplier   { get { return Has( eUpgrade.runningShoes ) ? 1.2f : 1f; } }
    public float PatienceMultiplier    { get { return Has( eUpgrade.comfyChairs ) ? 1.25f : 1f; } }
    public float TipMultiplier         { get { return Has( eUpgrade.tipJar ) ? 1.4f : 1f; } }

    public string ClockText {
        get {
            float h = Hour;
            int hour = Mathf.FloorToInt( h );
            // The clock ticks in 15-minute steps like a real shop clock
            int minutes = Mathf.FloorToInt( ( h - hour ) * 4 ) * 15;
            string ampm = ( hour >= 12 ) ? "PM" : "AM";
            int hour12 = ( hour % 12 == 0 ) ? 12 : hour % 12;
            return hour12 + ":" + minutes.ToString( "00" ) + " " + ampm;
        }
    }

    // ------------------------------------------------------------------ Unity

    void Awake() {
        S = this;
        stations = FindObjectsByType<Station>();
        Application.targetFrameRate = 60;
    }

    void Start() {
        GoToTitle();
    }

    void Update() {
        HandlePauseKey();
        if ( state != eGameState.playing ) return;

        dayTimer += Time.deltaTime;
        Announcements();

        // The day ends once the doors are closed and the last customer has left
        if ( !DoorsOpen && spawner.InShop == 0 ) {
            emptyShopTimer += Time.deltaTime;
            if ( emptyShopTimer > 1.25f ) EndDay();
        } else {
            emptyShopTimer = 0;
        }
    }

    void HandlePauseKey() {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if ( kb == null ) return;
        if ( kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame ) {
            if ( state == eGameState.playing ) Pause();
            else if ( state == eGameState.paused ) Resume();
        }
#endif
    }

    // Tabbing away from the game pauses it so nobody burns while you're gone
    void OnApplicationFocus( bool hasFocus ) {
        if ( !hasFocus && state == eGameState.playing && !AutoPlayer.ACTIVE ) Pause();
    }

    void Announcements() {
        float h = Hour;
        if ( !rushAnnounced && h >= rushHour ) {
            rushAnnounced = true;
            HUD.BANNER( "Lunch Rush!", new Color( 1f, 0.92f, 0.45f ) );
            SFX.PLAY( eSfx.jingleRush, 0.6f );
        }
        if ( !lastCallAnnounced && h >= lastCallHour ) {
            lastCallAnnounced = true;
            HUD.BANNER( "Last call! No new customers", Color.white );
            SFX.PLAY( eSfx.shopBell, 0.6f, 0.8f );
        }
        if ( !closingAnnounced && h >= closeHour && spawner.InShop > 0 ) {
            closingAnnounced = true;
            HUD.BANNER( "Closing time! Finish the last orders", Color.white );
        }
    }

    // ------------------------------------------------------------------ money

    // Called by a customer who just paid. Adds the streak bonus and shows the money popping up.
    public void RecordSale( int price, int tip, float patienceFrac, Vector3 at ) {
        // Happy customers (served with a decent amount of patience left) build a streak
        if ( patienceFrac >= 0.4f ) streak++;
        else streak = 0;
        int streakBonus = ( streak >= 3 ) ? Mathf.Min( streak - 2, 5 ) : 0;

        int total = price + tip + streakBonus;
        stats.earnings += total;
        stats.tips += tip + streakBonus;
        stats.served++;
        stats.bestStreak = Mathf.Max( stats.bestStreak, streak );

        FX.TEXT( "+$" + total, at, new Color( 0.5f, 1f, 0.45f ), 1.2f, 1.1f );
        if ( tip > 0 ) FX.TEXT( "tip $" + tip, at + new Vector3( 0.9f, -0.35f, 0 ), new Color( 1f, 0.85f, 0.3f ), 0.75f, 0.9f );
        if ( streakBonus > 0 ) FX.TEXT( "Streak x" + streak + "!", at + new Vector3( -1f, -0.35f, 0 ), new Color( 1f, 0.6f, 0.25f ), 0.8f, 0.9f );

        SFX.PLAY( eSfx.cashRegister, 0.8f );
        if ( tip > 0 ) SFX.PLAY( eSfx.tipJar, 0.6f, 1f + Mathf.Min( streak, 6 ) * 0.04f );
        HUD.MONEY_POP();
    }

    public void RecordLostCustomer( Vector3 at ) {
        stats.lost++;
        if ( streak >= 3 ) FX.TEXT( "Streak lost", at + Vector3.down * 0.4f, new Color( 0.8f, 0.8f, 0.8f ), 0.7f );
        streak = 0;
        HUD.LOST_POP();
    }

    // ------------------------------------------------------------------ flow

    public void GoToTitle() {
        Time.timeScale = 1;
        state = eGameState.title;
        PrepareKitchen();
        menus.Show( eGameState.title );
    }

    public void ShowHowToPlay() {
        state = eGameState.howToPlay;
        menus.Show( eGameState.howToPlay );
    }

    public void StartNewGame() {
        dayIndex = 0;
        savings = 0;
        owned.Clear();
        history.Clear();
        ShowDayIntro();
    }

    public void ShowDayIntro() {
        Time.timeScale = 1;
        state = eGameState.dayIntro;
        PrepareKitchen();
        menus.Show( eGameState.dayIntro );
    }

    // Opens the doors: the clock starts and customers begin to arrive
    public void BeginDay() {
        state = eGameState.playing;
        dayTimer = 0;
        streak = 0;
        stats = new DayStats();
        rushAnnounced = lastCallAnnounced = closingAnnounced = false;
        emptyShopTimer = 0;
        spawner.BeginDay( Day );
        inputGraceUntil = Time.unscaledTime + 0.25f;
        HUD.RESET();
        menus.Show( eGameState.playing );
        HUD.BANNER( Day.title + ": We're open!", new Color( 1f, 0.85f, 0.4f ) );
        SFX.PLAY( eSfx.jingleDayStart, 0.6f );
    }

    void EndDay() {
        state = eGameState.dayResults;
        lastDayPassed = stats.earnings >= Day.goal;
        if ( lastDayPassed ) {
            savings += stats.earnings;
            history.Add( stats );
        }
        SFX.PLAY( lastDayPassed ? eSfx.jingleWin : eSfx.jingleLose, 0.7f );
        menus.Show( eGameState.dayResults );
    }

    // 1 to 3 stars, based on how far past the goal the day went
    public int Stars( DayStats s, int goal ) {
        if ( s.earnings < goal ) return 0;
        if ( s.earnings >= goal * 1.5f && s.lost <= 1 ) return 3;
        if ( s.earnings >= goal * 1.25f ) return 2;
        return 1;
    }

    // The button on the results screen
    public void ContinueFromResults() {
        if ( !lastDayPassed ) {
            ShowDayIntro();             // try the same day again
        } else if ( IsLastDay ) {
            state = eGameState.victory;
            SFX.PLAY( eSfx.jingleVictory, 0.8f );
            menus.Show( eGameState.victory );
        } else {
            state = eGameState.shop;
            menus.Show( eGameState.shop );
        }
    }

    public bool BuyUpgrade( eUpgrade id ) {
        UpgradeDef def = GetUpgrade( id );
        if ( def == null || Has( id ) || savings < def.price ) {
            SFX.PLAY( eSfx.error, 0.6f );
            return false;
        }
        savings -= def.price;
        owned.Add( id );
        SFX.PLAY( eSfx.upgradeBuy, 0.8f );
        SFX.PLAY( eSfx.coins, 0.6f );
        return true;
    }

    public UpgradeDef GetUpgrade( eUpgrade id ) {
        foreach ( UpgradeDef u in upgrades ) if ( u.id == id ) return u;
        return null;
    }

    public void NextDay() {
        dayIndex = Mathf.Min( dayIndex + 1, days.Length - 1 );
        ShowDayIntro();
    }

    public void Pause() {
        if ( state != eGameState.playing ) return;
        state = eGameState.paused;
        Time.timeScale = 0;
        menus.Show( eGameState.paused );
    }

    public void Resume() {
        if ( state != eGameState.paused ) return;
        state = eGameState.playing;
        Time.timeScale = 1;
        inputGraceUntil = Time.unscaledTime + 0.25f;
        menus.Show( eGameState.playing );
    }

    public void RestartDay() {
        ShowDayIntro();
    }

    public void QuitGame() {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // Puts the kitchen back the way it is at 8 AM: empty counters, empty hands, the right things unlocked
    void PrepareKitchen() {
        foreach ( Station s in stations ) s.ResetForNewDay();
        baker.ClearHands();
        baker.PlaceAt( bakerStart.position );
        spawner.BeginDay( Day );
        dayTimer = 0;
        stats = new DayStats();
        streak = 0;
        HUD.RESET();
        if ( berryCrate != null ) berryCrate.SetLocked( !Day.berriesUnlocked );
        if ( thirdOven != null ) thirdOven.SetLocked( !Has( eUpgrade.thirdOven ) );
        if ( cat != null ) cat.ResetForNewDay();
    }
}
