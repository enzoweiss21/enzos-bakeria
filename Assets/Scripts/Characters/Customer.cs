using System.Collections.Generic;
using UnityEngine;

public enum eCustomerState { entering, waitingToOrder, waitingForFood, leaving }

public enum eCustomerType {
    regular,    // the usual
    hurried,    // on a lunch break: little patience, big tips
    grandma,    // all the time in the world, small tips
    critic      // the food critic: only perfect bakes impress them, but then they pay triple
}

// A customer walks in, waits to be greeted, orders, waits for their food, pays and leaves.
// If their patience runs out first, they storm out.
public class Customer : MonoBehaviour {
    [Header( "Inscribed" )]
    public CustomerLook     look;
    public OrderBubble      bubble;
    public Transform        bodyVisual;     // the paper doll; bobs while walking
    public Sprite           criticBadge;
    public Sprite           hurriedBadge;

    [Header( "Dynamic" )]
    public eCustomerType    type;
    public eCustomerState   state;
    public List<eItem>      order = new List<eItem>();
    public List<bool>       delivered = new List<bool>();
    public int              perfectCount;
    public float            patienceMax = 45;
    public float            patience;
    public float            walkSpeed = 2.4f;
    public float            voicePitch = 1;
    public ServiceSpot      spot;

    private List<Vector3>   path = new List<Vector3>();
    private int             pathIndex;
    private float           walkCycle;
    private float           fade = 1;
    private bool            angry;
    private bool            warned;     // already said "Hurry up!"

    public bool  IsAtCounter     { get { return state == eCustomerState.waitingToOrder || state == eCustomerState.waitingForFood; } }
    public float PatienceFrac    { get { return Mathf.Clamp01( patience / patienceMax ); } }
    public string OrderText {
        get {
            List<string> names = new List<string>();
            for ( int i = 0; i < order.Count; i++ ) if ( !delivered[i] ) names.Add( ItemBook.NAME( order[i] ) );
            return string.Join( " + ", names );
        }
    }

    // Called by the CustomerSpawner right after Instantiate
    public void Setup( eCustomerType newType, List<eItem> newOrder, ServiceSpot newSpot, List<Vector3> walkIn, float patienceSeconds ) {
        type = newType;
        order = newOrder;
        delivered = new List<bool>();
        foreach ( eItem e in order ) delivered.Add( false );
        spot = newSpot;
        spot.occupant = this;
        path = walkIn;
        pathIndex = 0;
        patienceMax = patience = patienceSeconds;
        state = eCustomerState.entering;
        fade = 0;

        switch ( type ) {
        case eCustomerType.hurried: walkSpeed = 3.4f; voicePitch = Random.Range( 1.3f, 1.5f ); break;
        case eCustomerType.grandma: walkSpeed = 1.5f; voicePitch = Random.Range( 0.75f, 0.9f ); break;
        case eCustomerType.critic:  walkSpeed = 2.0f; voicePitch = 0.7f; break;
        default:                    walkSpeed = 2.4f; voicePitch = Random.Range( 0.9f, 1.35f ); break;
        }
        look.Randomize( type );
        look.SetAlpha( 0 );
        bubble.SetBadge( type == eCustomerType.critic ? criticBadge : ( type == eCustomerType.hurried ? hurriedBadge : null ) );
        bubble.Hide();
    }

    void Update() {
        bool playing = GameManager.S != null && GameManager.S.IsPlaying;
        switch ( state ) {
        case eCustomerState.entering:
            // Fade in through the doorway, then walk to the counter
            fade = Mathf.Min( 1, fade + Time.deltaTime * 3f );
            look.SetAlpha( fade );
            if ( playing && WalkAlongPath() ) ArriveAtCounter();
            break;

        case eCustomerState.waitingToOrder:
        case eCustomerState.waitingForFood:
            if ( playing ) {
                // Being ignored at the counter is extra annoying, so patience drains faster before you greet them
                float drain = ( state == eCustomerState.waitingToOrder ) ? 1.4f : 1f;
                patience -= Time.deltaTime * drain;
                if ( !warned && PatienceFrac < 0.3f ) Grumble();
                if ( patience <= 0 ) StormOut();
            }
            bubble.SetPatience( PatienceFrac, PatienceFrac < 0.25f );
            break;

        case eCustomerState.leaving:
            if ( playing || GameManager.S.state == eGameState.dayResults ) {
                if ( WalkAlongPath() ) {
                    // Fade out through the door
                    if ( fade >= 1 ) SFX.PLAY( eSfx.doorClose, 0.25f );
                    fade -= Time.deltaTime * 4f;
                    look.SetAlpha( fade );
                    if ( fade <= 0 ) Destroy( gameObject );
                }
            }
            break;
        }
        Animate();
    }

    // Moves toward the next point on the path. Returns true once the end of the path is reached.
    bool WalkAlongPath() {
        if ( pathIndex >= path.Count ) return true;
        Vector3 goal = path[pathIndex];
        transform.position = Vector3.MoveTowards( transform.position, goal, walkSpeed * Time.deltaTime );
        if ( ( transform.position - goal ).sqrMagnitude < 0.0001f ) pathIndex++;
        return pathIndex >= path.Count;
    }

    void ArriveAtCounter() {
        state = eCustomerState.waitingToOrder;
        fade = 1;
        look.SetAlpha( 1 );
        bubble.ShowAlert();
        SFX.PLAY( eSfx.customerArrive, 0.7f );
        SFX.BABBLE( voicePitch, 2, 0.5f );
    }

    // ------------------------------------------------------------------ orders

    public void TakeOrder() {
        if ( state != eCustomerState.waitingToOrder ) return;
        state = eCustomerState.waitingForFood;
        bubble.ShowOrder( order, delivered );
        SFX.PLAY( eSfx.takeOrder, 0.8f );
        SFX.BABBLE( voicePitch, 3 + order.Count, 0.2f );
        string greeting = ( type == eCustomerType.critic ) ? "Impress me." : ( type == eCustomerType.grandma ? "No rush, dear" : "Hi!" );
        FX.TEXT( greeting, transform.position + Vector3.up * 2.65f, Color.white, 0.75f, 0.4f );
        GameManager.S.stats.ordersTaken++;
    }

    public void RemindOrder() {
        FX.POP( bubble.transform, 0.35f );
        SFX.BABBLE( voicePitch, 2, 0 );
    }

    public void TryServe( Baker baker ) {
        Item item = baker.Held;
        if ( item.type == eItem.burnt ) {
            Reject( "Ew, it's burnt!", 0.05f );
            return;
        }
        for ( int i = 0; i < order.Count; i++ ) {
            if ( !delivered[i] && order[i] == item.type ) {
                Accept( baker, i );
                return;
            }
        }
        if ( item.type == eItem.cupcake && order.Contains( eItem.frostedCupcake ) ) {
            Reject( "Where's the frosting?", 0.05f );
        } else {
            Reject( "That's not my order!", 0.1f );
        }
    }

    void Accept( Baker baker, int index ) {
        Item item = baker.Release();
        delivered[index] = true;
        if ( item.perfect ) perfectCount++;
        Destroy( item.gameObject );
        bubble.ShowOrder( order, delivered );
        SFX.PLAY( eSfx.putDown );

        if ( delivered.TrueForAll( d => d ) ) {
            PayAndLeave();
        } else {
            SFX.BABBLE( voicePitch, 2, 0.6f );
            FX.TEXT( "And the other one?", transform.position + Vector3.up * 2.65f, Color.white, 0.7f, 0.4f );
        }
    }

    void Reject( string complaint, float patienceCost ) {
        patience -= patienceMax * patienceCost;
        FX.TEXT( complaint, transform.position + Vector3.up * 2.65f, new Color( 1f, 0.6f, 0.5f ), 0.8f, 0.4f );
        SFX.PLAY( eSfx.error, 0.7f );
        SFX.BABBLE( voicePitch * 0.85f, 3, -0.6f );
        FX.POP( bodyVisual, 0.2f );
        GameManager.S.stats.wrongItems++;
    }

    void PayAndLeave() {
        int price = 0;
        foreach ( eItem e in order ) price += ItemBook.GET( e ).price;
        float frac = PatienceFrac;

        // Faster service and perfect bakes earn bigger tips
        float typeTip = ( type == eCustomerType.hurried ) ? 1.6f : ( type == eCustomerType.grandma ? 0.7f : 1f );
        int tip = Mathf.RoundToInt( price * 0.5f * frac * typeTip * GameManager.S.TipMultiplier ) + perfectCount * 2;

        Vector3 above = transform.position + Vector3.up * 2.1f;
        if ( type == eCustomerType.critic ) {
            if ( perfectCount == order.Count ) {
                price *= 3;
                FX.TEXT( "Five stars!", above + Vector3.up * 0.4f, new Color( 1f, 0.85f, 0.2f ), 1.1f );
                FX.STARS( above, 6 );
                SFX.PLAY( eSfx.sparkle );
                GameManager.S.stats.criticReview = 5;
            } else {
                tip = 0;
                FX.TEXT( "Hmm. Two stars.", above + Vector3.up * 0.4f, new Color( 0.8f, 0.8f, 0.9f ), 0.9f );
                GameManager.S.stats.criticReview = 2;
            }
        }

        GameManager.S.RecordSale( price, tip, frac, transform.position + Vector3.up * 1.6f );
        SFX.BABBLE( voicePitch, 3, 1f );
        FX.HEARTS( transform.position + Vector3.up * 1.3f, frac > 0.5f ? 3 : 1 );
        Leave( false );
    }

    // An audible warning that this customer is about to give up
    void Grumble() {
        warned = true;
        string line = ( type == eCustomerType.grandma ) ? "Is it ready, dear?" : ( type == eCustomerType.critic ? "I'm waiting..." : "Hurry up!" );
        FX.TEXT( line, transform.position + Vector3.up * 2.65f, new Color( 1f, 0.75f, 0.4f ), 0.75f, 0.4f );
        SFX.BABBLE( voicePitch * 0.9f, 3, -0.5f );
    }

    void StormOut() {
        GameManager.S.RecordLostCustomer( transform.position + Vector3.up * 1.6f );
        FX.TEXT( type == eCustomerType.grandma ? "Well, I never!" : "Forget it!", transform.position + Vector3.up * 2.65f, new Color( 1f, 0.4f, 0.35f ), 0.9f );
        FX.PUFF( transform.position + Vector3.up * 1.6f, new Color( 1f, 0.5f, 0.4f, 0.8f ), 4 );
        SFX.PLAY( eSfx.angryLeave );
        SFX.BABBLE( voicePitch * 0.8f, 4, -0.8f );
        angry = true;
        Leave( true );
    }

    void Leave( bool stormingOut ) {
        state = eCustomerState.leaving;
        bubble.Hide();
        if ( spot != null && spot.occupant == this ) spot.occupant = null;
        spot = null;
        // Walk back out the way we came in
        path.Reverse();
        pathIndex = 1;
        if ( stormingOut ) walkSpeed *= 1.4f;
    }

    // Biscuit the cat makes everyone a little more patient
    public void Cheer( float fraction ) {
        if ( !IsAtCounter ) return;
        patience = Mathf.Min( patienceMax, patience + patienceMax * fraction );
        FX.HEARTS( transform.position + Vector3.up * 1.5f, 2 );
    }

    // Used when a day restarts so nobody is left standing around
    public void Vanish() {
        if ( spot != null && spot.occupant == this ) spot.occupant = null;
        Destroy( gameObject );
    }

    // ------------------------------------------------------------------ animation

    void Animate() {
        bool walking = state == eCustomerState.entering || state == eCustomerState.leaving;
        if ( walking && GameManager.S.IsPlaying ) {
            walkCycle += Time.deltaTime * walkSpeed * 5f;
            bodyVisual.localPosition = new Vector3( 0, Mathf.Abs( Mathf.Sin( walkCycle ) ) * 0.08f, 0 );
            bodyVisual.localRotation = Quaternion.Euler( 0, 0, Mathf.Sin( walkCycle ) * 3f );
        } else if ( IsAtCounter && PatienceFrac < 0.3f ) {
            // Impatient foot tapping
            bodyVisual.localPosition = new Vector3( 0, Mathf.Abs( Mathf.Sin( Time.time * 12f ) ) * 0.04f, 0 );
            bodyVisual.localRotation = Quaternion.identity;
        } else {
            bodyVisual.localPosition = Vector3.zero;
            bodyVisual.localRotation = Quaternion.Euler( 0, 0, angry ? 0 : Mathf.Sin( Time.time * 2f + voicePitch * 10f ) * 1.5f );
        }
    }
}
