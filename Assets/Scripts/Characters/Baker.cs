using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// One frame of player input. The keyboard fills this in, or the AutoPlayer does when it's testing the game.
public struct BakerInput {
    public Vector2  move;
    public bool     grab;       // Space pressed this frame
    public bool     use;        // E pressed this frame
    public bool     useHeld;    // E held down
}

// Enzo, the player. WASD to walk, Space to pick up / put down, E to use things.
[RequireComponent( typeof( Rigidbody2D ) )]
public class Baker : MonoBehaviour {
    static public Baker S { get; private set; }

    [Header( "Inscribed" )]
    public float            baseSpeed = 4.2f;
    [Tooltip( "How far in front of the baker's feet to look for something to use" )]
    public float            reach = 0.75f;
    public Transform        visual;         // body + hat; bobs while walking
    public SpriteRenderer[] flipRenderers;  // sprites that mirror when walking left
    public Transform        holdAnchor;
    public Transform        selector;       // corner brackets around the current target
    public SpriteRenderer   selectorRend;

    [Header( "Dynamic" )]
    public Item             held;
    public Vector2          facing = Vector2.down;
    public Interactable     target;
    public bool             botControlled;
    public BakerInput       botInput;

    private Rigidbody2D     rigid;
    private Vector2         moveInput;
    private float           stepTimer;
    private float           walkCycle;
    private string          lastComplaint;
    private float           lastComplaintTime;

    public Item Held { get { return held; } }
    public bool IsMoving { get { return moveInput.sqrMagnitude > 0.01f; } }

    void Awake() {
        S = this;
        rigid = GetComponent<Rigidbody2D>();
    }

    void Update() {
        if ( GameManager.S == null || !GameManager.S.IsPlaying ) {
            moveInput = Vector2.zero;
            SetTarget( null );
            return;
        }

        BakerInput input = botControlled ? botInput : ReadKeyboard();
        moveInput = Vector2.ClampMagnitude( input.move, 1 );

        if ( IsMoving ) {
            facing = SnapToAxis( moveInput );
            if ( Mathf.Abs( moveInput.x ) > 0.1f ) {
                foreach ( SpriteRenderer sr in flipRenderers ) sr.flipX = moveInput.x < 0;
            }
        }
        AnimateWalk();

        SetTarget( FindTarget() );
        HUD.PROMPT( target != null ? target.Prompt( this ) : ( held != null ? "Carrying " + ItemBook.NAME( held.type ) : "" ) );

        if ( input.grab ) {
            if ( target != null ) target.Grab( this );
            else if ( held != null ) Complain( "Put it on a counter" );
        }
        if ( input.use && target != null ) target.Use( this );
        if ( input.useHeld && target != null ) target.UseHeld( this );
    }

    void FixedUpdate() {
        float speed = baseSpeed * ( GameManager.S != null ? GameManager.S.MoveSpeedMultiplier : 1 );
        rigid.linearVelocity = moveInput * speed;
    }

    BakerInput ReadKeyboard() {
        BakerInput input = new BakerInput();
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if ( kb == null ) return input;
        float x = 0, y = 0;
        if ( kb.aKey.isPressed || kb.leftArrowKey.isPressed )  x -= 1;
        if ( kb.dKey.isPressed || kb.rightArrowKey.isPressed ) x += 1;
        if ( kb.sKey.isPressed || kb.downArrowKey.isPressed )  y -= 1;
        if ( kb.wKey.isPressed || kb.upArrowKey.isPressed )    y += 1;
        input.move = new Vector2( x, y );
        input.grab = kb.spaceKey.wasPressedThisFrame;
        input.use = kb.eKey.wasPressedThisFrame;
        input.useHeld = kb.eKey.isPressed;
#endif
        // Ignore keys for a moment after a menu closes so the "Start" keypress doesn't also grab something
        if ( GameManager.S.InputGraceActive ) {
            input.grab = input.use = input.useHeld = false;
        }
        return input;
    }

    // Facing is always one of the four directions, so it's obvious which station is being targeted
    static Vector2 SnapToAxis( Vector2 v ) {
        if ( Mathf.Abs( v.x ) > Mathf.Abs( v.y ) ) return new Vector2( Mathf.Sign( v.x ), 0 );
        return new Vector2( 0, Mathf.Sign( v.y ) );
    }

    // ------------------------------------------------------------------ targeting

    Interactable FindTarget() {
        Vector2 feet = transform.position;
        Vector2 probe = feet + facing * reach;
        Interactable best = null;
        float bestScore = float.MaxValue;
        foreach ( Interactable it in Interactable.ALL ) {
            if ( !it.Targetable ) continue;
            float d = it.DistanceTo( probe );
            if ( d > 0.35f ) continue;
            // Ties (standing between two counters) go to the one whose center is closer
            float score = d + 0.05f * Vector2.Distance( it.Center, probe );
            if ( score < bestScore ) {
                bestScore = score;
                best = it;
            }
        }
        return best;
    }

    void SetTarget( Interactable newTarget ) {
        target = newTarget;
        if ( selector == null ) return;
        selectorRend.enabled = target != null;
        if ( target != null ) {
            selector.position = target.Center;
            // Gently pulse so it reads as "selected"
            float pulse = 1 + 0.06f * Mathf.Sin( Time.time * 8f );
            selector.localScale = new Vector3( target.size.x * pulse, target.size.y * pulse, 1 );
        }
    }

    // ------------------------------------------------------------------ carrying

    public void Hold( Item item ) {
        if ( item == null ) return;
        held = item;
        item.AttachTo( holdAnchor, 5 );
        FX.POP( item.transform, 0.25f );
    }

    public Item Release() {
        Item item = held;
        held = null;
        if ( item != null ) item.transform.SetParent( null );
        return item;
    }

    // Drop whatever is in our hands (used when a new day starts)
    public void ClearHands() {
        if ( held != null ) Destroy( held.gameObject );
        held = null;
    }

    // A short message over the baker's head with a "nope" sound, for when an action isn't allowed
    public void Complain( string message ) {
        if ( message == lastComplaint && Time.time - lastComplaintTime < 0.8f ) return;
        lastComplaint = message;
        lastComplaintTime = Time.time;
        FX.TEXT( message, transform.position + Vector3.up * 1.4f, new Color( 1f, 0.75f, 0.6f ), 0.8f, 0.5f );
        SFX.PLAY( eSfx.error, 0.6f );
    }

    // ------------------------------------------------------------------ animation

    void AnimateWalk() {
        if ( IsMoving ) {
            walkCycle += Time.deltaTime * 14f;
            stepTimer -= Time.deltaTime;
            if ( stepTimer <= 0 ) {
                SFX.PLAY( eSfx.footstep, 0.18f );
                stepTimer = 0.3f;
            }
        } else {
            walkCycle = 0;
            stepTimer = 0;
        }
        // Hop a little with each step, and lean into the walk
        float hop = IsMoving ? Mathf.Abs( Mathf.Sin( walkCycle ) ) * 0.07f : 0;
        float breathe = IsMoving ? 0 : Mathf.Sin( Time.time * 2.5f ) * 0.015f;
        visual.localPosition = new Vector3( 0, hop, 0 );
        visual.localScale = new Vector3( 1 - breathe, 1 + breathe, 1 );
        visual.localRotation = Quaternion.Euler( 0, 0, IsMoving ? -moveInput.x * 4f : 0 );
    }

    public void PlaceAt( Vector3 pos ) {
        transform.position = pos;
        rigid.position = pos;
        rigid.linearVelocity = Vector2.zero;
        facing = Vector2.down;
    }
}
