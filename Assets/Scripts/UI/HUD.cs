using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The bar across the top during a workday (day, clock, money vs goal, streak),
// the action prompt at the bottom, and the big banners ("Lunch Rush!").
public class HUD : MonoBehaviour {
    static private HUD S;

    [Header( "Inscribed" )]
    public TextMeshProUGUI  dayText;
    public TextMeshProUGUI  clockText;
    public Image            clockFill;      // fills up as the day goes on
    public TextMeshProUGUI  moneyText;
    public Image            goalFill;
    public TextMeshProUGUI  goalText;
    public TextMeshProUGUI  streakText;
    public TextMeshProUGUI  servedText;
    public RectTransform    moneyGroup;     // pops when money comes in
    public TextMeshProUGUI  promptText;
    public Image            promptBackground;
    public RectTransform    banner;
    public TextMeshProUGUI  bannerText;
    public CanvasGroup      bannerGroup;

    private string          prompt = "";
    private Coroutine       bannerRoutine;
    private float           moneyPop;
    private int             shownMoney;

    void Awake() {
        S = this;
        bannerGroup.alpha = 0;
    }

    void Update() {
        GameManager gm = GameManager.S;
        if ( gm == null ) return;

        dayText.text = gm.Day.title;
        clockText.text = gm.ClockText;
        clockFill.fillAmount = gm.Day01;

        // Count the money up quickly instead of jumping
        int target = gm.stats.earnings;
        if ( shownMoney != target ) {
            int step = Mathf.Max( 1, Mathf.Abs( target - shownMoney ) / 6 );
            shownMoney = ( int ) Mathf.MoveTowards( shownMoney, target, step );
        }
        moneyText.text = "$" + shownMoney;
        goalText.text = "Goal $" + gm.Day.goal;
        float frac = Mathf.Clamp01( (float) shownMoney / gm.Day.goal );
        goalFill.fillAmount = frac;
        goalFill.color = ( frac >= 1 ) ? new Color( 1f, 0.85f, 0.25f ) : new Color( 0.45f, 0.8f, 0.4f );

        streakText.text = ( gm.streak >= 2 ) ? "Streak x" + gm.streak : "";
        servedText.text = "Served " + gm.stats.served + "   Lost " + gm.stats.lost;

        moneyPop = Mathf.MoveTowards( moneyPop, 0, Time.unscaledDeltaTime * 3f );
        moneyGroup.localScale = Vector3.one * ( 1 + moneyPop * 0.25f );
    }

    // LateUpdate runs after the Baker's Update has set this frame's prompt
    void LateUpdate() {
        promptText.text = prompt;
        promptBackground.enabled = !string.IsNullOrEmpty( prompt );
        prompt = "";    // whoever wants a prompt shown has to set it again next frame
    }

    // ------------------------------------------------------------------ static helpers

    static public void PROMPT( string text ) {
        if ( S != null ) S.prompt = text;
    }

    static public void MONEY_POP() {
        if ( S != null ) S.moneyPop = 1;
    }

    static public void LOST_POP() {
        if ( S != null && S.isActiveAndEnabled ) S.StartCoroutine( S.FlashLost() );
    }

    static public void BANNER( string text, Color color ) {
        if ( S == null || !S.isActiveAndEnabled ) return;   // coroutines can't run while the HUD is hidden
        if ( S.bannerRoutine != null ) S.StopCoroutine( S.bannerRoutine );
        S.bannerRoutine = S.StartCoroutine( S.ShowBanner( text, color ) );
    }

    // Resets the counting-up animation at the start of a day
    static public void RESET() {
        if ( S != null ) S.shownMoney = 0;
    }

    private IEnumerator ShowBanner( string text, Color color ) {
        bannerText.text = text;
        bannerText.color = color;
        // Swoop in, hold, fade out
        for ( float t = 0; t < 2.6f; t += Time.unscaledDeltaTime ) {
            float inT = Mathf.Clamp01( t / 0.25f );
            float outT = Mathf.Clamp01( ( t - 2.1f ) / 0.5f );
            bannerGroup.alpha = inT * ( 1 - outT );
            float overshoot = 1 + Mathf.Sin( inT * Mathf.PI ) * 0.15f;
            banner.localScale = Vector3.one * overshoot;
            yield return null;
        }
        bannerGroup.alpha = 0;
    }

    private IEnumerator FlashLost() {
        for ( float t = 0; t < 0.6f; t += Time.unscaledDeltaTime ) {
            servedText.color = Color.Lerp( new Color( 1f, 0.35f, 0.3f ), new Color( 0.35f, 0.22f, 0.15f ), t / 0.6f );
            yield return null;
        }
        servedText.color = new Color( 0.35f, 0.22f, 0.15f );
    }
}
