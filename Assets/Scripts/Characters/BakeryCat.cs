using UnityEngine;

public enum eCatState { walking, sitting, napping }

// Biscuit, the bakery cat. Wanders around the kitchen floor and sometimes sits right where you need to walk.
// Press E next to Biscuit for a quick pet: every waiting customer melts a little and gets some patience back.
// After a pet Biscuit naps for a while, so you have to pick the right moment.
public class BakeryCat : Interactable {
    [Header( "Inscribed: Cat" )]
    public Transform        visual;
    public SpriteRenderer   sRend;
    public Vector2          roamMin = new Vector2( -9.2f, -3.9f );
    public Vector2          roamMax = new Vector2( 9.2f, -3.2f );
    public float            walkSpeed = 1.3f;
    public float            napSeconds = 22f;
    [Range( 0, 1 )]
    public float            patienceGift = 0.18f;

    [Header( "Dynamic: Cat" )]
    public eCatState        state = eCatState.sitting;
    public float            stateTimer = 2f;

    private Vector2         destination;
    private float           nextMeow;
    private float           nextZzz;

    public bool IsNapping { get { return state == eCatState.napping; } }

    // Only pettable with empty hands, so Biscuit never "steals" a Space press meant for a counter
    public override bool Targetable { get { return Baker.S == null || Baker.S.Held == null; } }

    void Start() {
        nextMeow = Time.time + Random.Range( 15f, 30f );
    }

    void Update() {
        // Biscuit wanders on the title screen too, but freezes while the game is paused
        if ( GameManager.S == null ) return;
        eGameState gs = GameManager.S.state;
        if ( gs == eGameState.paused ) return;

        stateTimer -= Time.deltaTime;
        switch ( state ) {
        case eCatState.walking:
            Vector2 pos = transform.position;
            Vector2 next = Vector2.MoveTowards( pos, destination, walkSpeed * Time.deltaTime );
            if ( Mathf.Abs( next.x - pos.x ) > 0.0001f ) sRend.flipX = next.x < pos.x;
            transform.position = next;
            visual.localPosition = new Vector3( 0, Mathf.Abs( Mathf.Sin( Time.time * 10f ) ) * 0.05f, 0 );
            if ( ( next - destination ).sqrMagnitude < 0.0004f ) SetState( eCatState.sitting, Random.Range( 3f, 7f ) );
            break;

        case eCatState.sitting:
            visual.localPosition = Vector3.zero;
            visual.localScale = new Vector3( 1, 1 + Mathf.Sin( Time.time * 3f ) * 0.02f, 1 );
            if ( stateTimer <= 0 ) StartWalking();
            break;

        case eCatState.napping:
            // Curl up flatter and puff out a "z" now and then
            visual.localScale = new Vector3( 1.08f, 0.82f + Mathf.Sin( Time.time * 1.5f ) * 0.02f, 1 );
            if ( Time.time >= nextZzz ) {
                FX.ZZZ( (Vector2) transform.position + new Vector2( 0.3f, 0.8f ) );
                nextZzz = Time.time + 1.2f;
            }
            if ( stateTimer <= 0 ) {
                visual.localScale = Vector3.one;
                SetState( eCatState.sitting, 1.5f );
            }
            break;
        }

        if ( gs == eGameState.playing && state != eCatState.napping && Time.time >= nextMeow ) {
            SFX.PLAY( eSfx.catMeow, 0.35f );
            nextMeow = Time.time + Random.Range( 25f, 45f );
        }
    }

    void StartWalking() {
        destination = new Vector2( Random.Range( roamMin.x, roamMax.x ), Random.Range( roamMin.y, roamMax.y ) );
        SetState( eCatState.walking, 10f );
    }

    void SetState( eCatState newState, float duration ) {
        state = newState;
        stateTimer = duration;
    }

    public override void Use( Baker baker ) {
        if ( IsNapping ) {
            FX.TEXT( "Shh... Biscuit is napping", transform.position + Vector3.up * 1.1f, new Color( 0.8f, 0.85f, 1f ), 0.7f, 0.4f );
            return;
        }
        // A pet makes the whole bakery happier
        SFX.PLAY( eSfx.catPurr, 0.8f );
        SFX.PLAY( eSfx.catMeow, 0.6f, 1.1f );
        FX.HEARTS( transform.position + Vector3.up * 0.8f, 5 );
        FX.TEXT( "Purrr!", transform.position + Vector3.up * 1.2f, new Color( 1f, 0.75f, 0.85f ), 0.9f );
        if ( CustomerSpawner.S != null ) {
            foreach ( Customer c in CustomerSpawner.S.customers ) if ( c != null ) c.Cheer( patienceGift );
        }
        GameManager.S.stats.catPets++;
        SetState( eCatState.napping, napSeconds );
    }

    public override void Grab( Baker baker ) {
        // Space near the cat does nothing special, but make sure it doesn't feel ignored
        if ( baker.Held == null ) Use( baker );
    }

    public override string Prompt( Baker baker ) {
        return IsNapping ? "Biscuit is napping" : "[E] Pet Biscuit (customers get more patient)";
    }

    public void ResetForNewDay() {
        visual.localScale = Vector3.one;
        SetState( eCatState.sitting, 2f );
    }
}
