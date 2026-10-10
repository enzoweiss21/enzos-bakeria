using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Shows the right panel for each game state and fills it in.
// Every button can be clicked, and every screen also has keyboard shortcuts (shown on the buttons).
public class MenuScreens : MonoBehaviour {
    [Header( "Inscribed: Panels" )]
    public GameObject       hud;
    public GameObject       tutorial;
    public GameObject       titlePanel;
    public GameObject       howToPanel;
    public GameObject       dayIntroPanel;
    public GameObject       pausePanel;
    public GameObject       resultsPanel;
    public GameObject       shopPanel;
    public GameObject       victoryPanel;
    public Image            dimmer;         // darkens the bakery behind a menu

    [Header( "Inscribed: Title" )]
    public Button           playButton;
    public Button           howToButton;
    public Button           quitButton;
    public RectTransform    logo;

    [Header( "Inscribed: How to play" )]
    public Button           howToBackButton;

    [Header( "Inscribed: Day intro" )]
    public TextMeshProUGUI  introTitle;
    public TextMeshProUGUI  introGoal;
    public TextMeshProUGUI  introBody;
    public Button           introStartButton;

    [Header( "Inscribed: Pause" )]
    public Button           resumeButton;
    public Button           restartButton;
    public Button           pauseMenuButton;

    [Header( "Inscribed: Results" )]
    public TextMeshProUGUI  resultsTitle;
    public TextMeshProUGUI  resultsBody;
    public TextMeshProUGUI  resultsTotal;
    public Image[]          resultStars;
    public Button           resultsContinueButton;
    public TextMeshProUGUI  resultsContinueLabel;
    public Button           resultsMenuButton;

    [Header( "Inscribed: Shop" )]
    public TextMeshProUGUI  shopSavings;
    public UpgradeCard[]    upgradeCards;
    public Button           shopContinueButton;
    public TextMeshProUGUI  shopContinueLabel;

    [Header( "Inscribed: Victory" )]
    public TextMeshProUGUI  victoryBody;
    public Button           victoryPlayAgainButton;
    public Button           victoryMenuButton;

    private float           shownAt;        // when the current screen appeared (keys are ignored for a moment)

    static private readonly Color STAR_ON = new Color( 1f, 0.82f, 0.2f );
    static private readonly Color STAR_OFF = new Color( 0.3f, 0.25f, 0.22f, 0.6f );

    void Start() {
        GameManager gm = GameManager.S;
        Wire( playButton, gm.StartNewGame );
        Wire( howToButton, gm.ShowHowToPlay );
        Wire( quitButton, gm.QuitGame );
        Wire( howToBackButton, gm.GoToTitle );
        Wire( introStartButton, gm.BeginDay );
        Wire( resumeButton, gm.Resume );
        Wire( restartButton, gm.RestartDay );
        Wire( pauseMenuButton, gm.GoToTitle );
        Wire( resultsContinueButton, gm.ContinueFromResults );
        Wire( resultsMenuButton, gm.GoToTitle );
        Wire( shopContinueButton, gm.NextDay );
        Wire( victoryPlayAgainButton, gm.StartNewGame );
        Wire( victoryMenuButton, gm.GoToTitle );
        for ( int i = 0; i < upgradeCards.Length; i++ ) {
            UpgradeCard card = upgradeCards[i];
            card.buyButton.onClick.AddListener( () => Buy( card.upgrade ) );
        }
    }

    // Every button plays the same click sound
    static void Wire( Button b, UnityEngine.Events.UnityAction action ) {
        if ( b == null ) return;
        b.onClick.AddListener( () => SFX.PLAY( eSfx.uiClick, 0.7f ) );
        b.onClick.AddListener( action );
    }

    void Buy( eUpgrade id ) {
        if ( GameManager.S.BuyUpgrade( id ) ) RefreshShop();
    }

    // ------------------------------------------------------------------ showing panels

    public void Show( eGameState state ) {
        titlePanel.SetActive( state == eGameState.title );
        howToPanel.SetActive( state == eGameState.howToPlay );
        dayIntroPanel.SetActive( state == eGameState.dayIntro );
        pausePanel.SetActive( state == eGameState.paused );
        resultsPanel.SetActive( state == eGameState.dayResults );
        shopPanel.SetActive( state == eGameState.shop );
        victoryPanel.SetActive( state == eGameState.victory );
        hud.SetActive( state == eGameState.playing || state == eGameState.paused || state == eGameState.dayIntro );
        tutorial.SetActive( state == eGameState.playing );
        dimmer.enabled = state != eGameState.playing;
        dimmer.color = new Color( 0.08f, 0.04f, 0.03f, state == eGameState.title ? 0.35f : 0.6f );

        switch ( state ) {
        case eGameState.dayIntro:   FillDayIntro(); break;
        case eGameState.dayResults: FillResults(); break;
        case eGameState.shop:       RefreshShop(); break;
        case eGameState.victory:    FillVictory(); break;
        }
        shownAt = Time.unscaledTime;
        // Clear the UI selection so a stray Space press doesn't click a button twice
        if ( EventSystem.current != null ) EventSystem.current.SetSelectedGameObject( null );
    }

    void FillDayIntro() {
        GameManager gm = GameManager.S;
        DaySettings day = gm.Day;
        introTitle.text = day.title;
        introGoal.text = "Goal: $" + day.goal + " by closing time";
        StringBuilder sb = new StringBuilder( day.intro );
        if ( gm.savings > 0 ) sb.Append( "\n\nSavings: $" + gm.savings );
        introBody.text = sb.ToString();
    }

    void FillResults() {
        GameManager gm = GameManager.S;
        DayStats s = gm.stats;
        DaySettings day = gm.Day;
        bool passed = gm.lastDayPassed;

        resultsTitle.text = passed ? day.title + " complete!" : "Not quite...";
        // <pos> lines the numbers up in a column
        StringBuilder sb = new StringBuilder();
        Line( sb, "Customers served", s.served.ToString() );
        Line( sb, "Customers lost", s.lost.ToString() );
        Line( sb, "Perfect bakes", s.perfectBakes.ToString() );
        Line( sb, "Burnt", s.burnt.ToString() );
        Line( sb, "Tips and streak bonus", "$" + s.tips );
        Line( sb, "Best streak", s.bestStreak.ToString() );
        if ( s.catPets > 0 ) Line( sb, "Biscuit pets", s.catPets.ToString() );
        if ( s.criticReview > 0 ) Line( sb, "Food critic", s.criticReview + " / 5 stars" );
        resultsBody.text = sb.ToString();

        resultsTotal.text = "Earned $" + s.earnings + "  (goal $" + day.goal + ")";
        if ( !passed ) resultsTotal.text += "\nYou needed $" + ( day.goal - s.earnings ) + " more. Try again!";

        int stars = gm.Stars( s, day.goal );
        for ( int i = 0; i < resultStars.Length; i++ ) resultStars[i].color = ( i < stars ) ? STAR_ON : STAR_OFF;

        if ( !passed ) resultsContinueLabel.text = "[Space] Try again";
        else if ( gm.IsLastDay ) resultsContinueLabel.text = "[Space] Finish";
        else resultsContinueLabel.text = "[Space] Visit the shop";
    }

    static void Line( StringBuilder sb, string label, string value ) {
        sb.AppendLine( label + "<pos=68%>" + value );
    }

    void RefreshShop() {
        GameManager gm = GameManager.S;
        shopSavings.text = "Savings: $" + gm.savings;
        for ( int i = 0; i < upgradeCards.Length; i++ ) upgradeCards[i].Refresh( gm, i );
        shopContinueLabel.text = "[Space] Open Day " + ( gm.dayIndex + 2 );
    }

    void FillVictory() {
        GameManager gm = GameManager.S;
        int total = 0, served = 0, perfect = 0, stars = 0;
        for ( int i = 0; i < gm.history.Count; i++ ) {
            DayStats s = gm.history[i];
            total += s.earnings;
            served += s.served;
            perfect += s.perfectBakes;
            stars += gm.Stars( s, gm.days[Mathf.Min( i, gm.days.Length - 1 )].goal );
        }
        victoryBody.text = "Enzo's Bakery is the talk of the town!\n\n" +
            "Total earned:   $" + total + "\n" +
            "Customers served:   " + served + "\n" +
            "Perfect bakes:   " + perfect + "\n" +
            "Stars:   " + stars + " / " + ( gm.days.Length * 3 );
    }

    // ------------------------------------------------------------------ keyboard shortcuts

    void Update() {
        AnimateTitle();
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if ( kb == null || Time.unscaledTime - shownAt < 0.3f ) return;
        bool confirm = kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
        GameManager gm = GameManager.S;

        switch ( gm.state ) {
        case eGameState.title:
            if ( confirm ) Click( playButton );
            else if ( kb.hKey.wasPressedThisFrame ) Click( howToButton );
            break;
        case eGameState.howToPlay:
            if ( confirm || kb.escapeKey.wasPressedThisFrame ) Click( howToBackButton );
            break;
        case eGameState.dayIntro:
            if ( confirm ) Click( introStartButton );
            break;
        case eGameState.paused:
            if ( kb.rKey.wasPressedThisFrame ) Click( restartButton );
            else if ( kb.qKey.wasPressedThisFrame ) Click( pauseMenuButton );
            break;
        case eGameState.dayResults:
            if ( confirm ) Click( resultsContinueButton );
            break;
        case eGameState.shop:
            if ( confirm ) Click( shopContinueButton );
            for ( int i = 0; i < upgradeCards.Length; i++ ) {
                if ( kb[Key.Digit1 + i].wasPressedThisFrame ) Buy( upgradeCards[i].upgrade );
            }
            break;
        case eGameState.victory:
            if ( confirm ) Click( victoryPlayAgainButton );
            break;
        }
#endif
    }

    static void Click( Button b ) {
        if ( b != null && b.interactable && b.gameObject.activeInHierarchy ) b.onClick.Invoke();
    }

    // The logo on the title screen gently bobs and tilts
    void AnimateTitle() {
        if ( logo == null || !titlePanel.activeSelf ) return;
        float t = Time.unscaledTime;
        logo.localRotation = Quaternion.Euler( 0, 0, Mathf.Sin( t * 1.3f ) * 2f );
        logo.localScale = Vector3.one * ( 1 + Mathf.Sin( t * 2.1f ) * 0.02f );
    }
}
