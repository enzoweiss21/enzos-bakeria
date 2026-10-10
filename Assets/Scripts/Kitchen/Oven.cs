using UnityEngine;

public enum eOvenState { empty, baking, perfect, ready, burnt }

// Bakes dough. The timing is the skill here:
//   baking  -> the bar fills up
//   PERFECT -> right when it dings there's a short golden window; take it out now for a bigger tip
//   ready   -> still fine, but the bar drains and the oven starts beeping
//   burnt   -> smoke! Only good for the trash
// Dough taken out early keeps its progress, so it can go back in later.
public class Oven : Station {
    [Header( "Inscribed: Oven" )]
    public SpriteRenderer   ovenRend;
    public Sprite           spriteOff;
    public Sprite           spriteOnA;
    public Sprite           spriteOnB;
    public WorldBar         bar;
    public SpriteRenderer   contentIcon;    // shows what's inside, since the item itself is hidden
    public SpriteRenderer   lockIcon;
    public float            perfectWindow = 1.75f;
    public float            burnDelay = 7f; // seconds after it's done before it burns

    [Header( "Dynamic: Oven" )]
    public eOvenState       state = eOvenState.empty;
    public bool             locked;
    public float            doneTimer;

    private float           flicker;
    private float           nextBeep;
    private float           nextSmoke;

    static private readonly Color BAKING_COLOR = new Color( 1f, 0.6f, 0.2f );
    static private readonly Color PERFECT_COLOR = new Color( 1f, 0.9f, 0.25f );

    public void SetLocked( bool isLocked ) {
        locked = isLocked;
        if ( lockIcon != null ) lockIcon.enabled = isLocked;
        ovenRend.color = isLocked ? new Color( 0.45f, 0.45f, 0.5f ) : Color.white;
        RefreshVisuals();
    }

    // How long the current dough has left, used by the AutoPlayer and the music
    public float SecondsUntilDone {
        get {
            if ( state != eOvenState.baking || item == null ) return 0;
            return ( item.Def.bakeSeconds - item.bakeProgress ) / GameManager.S.OvenSpeed;
        }
    }

    public override void Grab( Baker baker ) {
        if ( locked ) {
            baker.Complain( "Buy the Third Oven in the shop" );
            return;
        }
        if ( baker.Held != null ) {
            if ( !IsEmpty ) { baker.Complain( "The oven is full" ); return; }
            if ( !baker.Held.IsDough ) { baker.Complain( "Only dough goes in the oven" ); return; }

            TakeFromBaker( baker );
            item.SetVisible( false );
            state = eOvenState.baking;
            SFX.PLAY( eSfx.ovenDoor );
            RefreshVisuals();
            return;
        }
        if ( IsEmpty ) return;

        // Take whatever is inside out
        if ( state == eOvenState.perfect ) {
            item.perfect = true;
            item.Refresh();
            SFX.PLAY( eSfx.perfectDing );
            FX.SPARKLES( Center + Vector2.up * 0.6f, 8 );
            FX.TEXT( "PERFECT!", Center + Vector2.up * 0.9f, PERFECT_COLOR, 1.1f );
            GameManager.S.stats.perfectBakes++;
        } else {
            SFX.PLAY( eSfx.ovenDoor );
        }
        GiveToBaker( baker );
        state = eOvenState.empty;
        RefreshVisuals();
    }

    void Update() {
        if ( GameManager.S == null || !GameManager.S.IsPlaying || item == null ) {
            AnimateFire();
            return;
        }

        switch ( state ) {
        case eOvenState.baking:
            item.bakeProgress += Time.deltaTime * GameManager.S.OvenSpeed;
            SFX.HOLD_LOOP( eLoop.ovenFire, 0.6f );
            // A little steam rising from the oven while it bakes
            if ( Time.time >= nextSmoke ) {
                FX.PUFF( Center + new Vector2( Random.Range( -0.2f, 0.2f ), 0.75f ), new Color( 1, 1, 1, 0.35f ), 1 );
                nextSmoke = Time.time + 0.7f;
            }
            if ( item.bakeProgress >= item.Def.bakeSeconds ) {
                item.SetType( item.Def.bakesInto );
                state = eOvenState.perfect;
                doneTimer = 0;
                SFX.PLAY( eSfx.ovenDing );
                FX.SPARKLES( Center + Vector2.up * 0.6f, 4 );
            }
            break;

        case eOvenState.perfect:
            doneTimer += Time.deltaTime;
            SFX.HOLD_LOOP( eLoop.ovenFire, 0.4f );
            if ( doneTimer >= perfectWindow ) state = eOvenState.ready;
            break;

        case eOvenState.ready:
            doneTimer += Time.deltaTime;
            SFX.HOLD_LOOP( eLoop.ovenFire, 0.4f );
            float timeLeft = burnDelay - doneTimer;
            // Beep faster and faster during the last few seconds
            if ( timeLeft < 3f && Time.time >= nextBeep ) {
                SFX.PLAY( eSfx.warningBeep, 0.5f, 1.2f );
                nextBeep = Time.time + Mathf.Lerp( 0.25f, 0.7f, timeLeft / 3f );
            }
            if ( doneTimer >= burnDelay ) Burn();
            break;

        case eOvenState.burnt:
            if ( Time.time >= nextSmoke ) {
                FX.PUFF( Center + Vector2.up * 0.7f, new Color( 0.3f, 0.3f, 0.3f, 0.8f ), 2 );
                nextSmoke = Time.time + 0.5f;
            }
            break;
        }

        AnimateFire();
        RefreshVisuals();
    }

    void Burn() {
        item.burntFrom = item.type;
        item.perfect = false;
        item.SetType( eItem.burnt );
        state = eOvenState.burnt;
        SFX.PLAY( eSfx.burnt );
        FX.PUFF( Center + Vector2.up * 0.6f, new Color( 0.25f, 0.25f, 0.25f, 0.9f ), 8 );
        FX.TEXT( "Burnt!", Center + Vector2.up * 0.9f, new Color( 0.6f, 0.6f, 0.6f ) );
        GameManager.S.stats.burnt++;
    }

    // Flicker between the two lit sprites while there's something in the oven
    void AnimateFire() {
        bool lit = !locked && ( state == eOvenState.baking || state == eOvenState.perfect || state == eOvenState.ready );
        if ( !lit ) {
            ovenRend.sprite = spriteOff;
            return;
        }
        flicker += Time.deltaTime;
        ovenRend.sprite = ( Mathf.FloorToInt( flicker * 6f ) % 2 == 0 ) ? spriteOnA : spriteOnB;
    }

    void RefreshVisuals() {
        contentIcon.enabled = item != null;
        if ( item != null ) {
            contentIcon.sprite = item.sRend.sprite;
            contentIcon.color = item.sRend.color;
        }

        switch ( state ) {
        case eOvenState.baking:
            bar.Show( true );
            bar.Set( item.bakeProgress / item.Def.bakeSeconds, BAKING_COLOR );
            break;
        case eOvenState.perfect:
            bar.Show( true );
            // Flash gold so the perfect moment is impossible to miss
            bar.Set( 1, ( Mathf.FloorToInt( Time.time * 8f ) % 2 == 0 ) ? PERFECT_COLOR : Color.white );
            break;
        case eOvenState.ready:
            bar.Show( true );
            float left = 1 - doneTimer / burnDelay;
            bar.Set( left, WorldBar.PATIENCE_COLOR( left ) );
            break;
        default:
            bar.Show( false );
            break;
        }

        // Shake a little when it's about to burn
        if ( visual != null ) {
            bool danger = state == eOvenState.ready && burnDelay - doneTimer < 3f;
            visual.localPosition = danger ? new Vector3( Mathf.Sin( Time.time * 60f ) * 0.03f, 0, 0 ) : Vector3.zero;
        }
    }

    public override string Prompt( Baker baker ) {
        if ( locked ) return "Third oven (buy it in the shop)";
        if ( baker.Held != null && baker.Held.IsDough && IsEmpty ) return "[Space] Bake " + ItemBook.NAME( baker.Held.type );
        if ( baker.Held == null && !IsEmpty ) {
            switch ( state ) {
            case eOvenState.baking:  return "[Space] Take out early (it keeps its progress)";
            case eOvenState.perfect: return "[Space] Take out - PERFECT!";
            case eOvenState.ready:   return "[Space] Take out " + ItemBook.NAME( item.type );
            case eOvenState.burnt:   return "[Space] Take out the burnt mess";
            }
        }
        return "Oven";
    }

    public override void ResetForNewDay() {
        base.ResetForNewDay();
        state = eOvenState.empty;
        doneTimer = 0;
        RefreshVisuals();
    }
}
