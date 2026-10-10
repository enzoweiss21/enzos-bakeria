using UnityEngine;

// Put in flour (and optionally one topping), then hold E to mix it into dough.
//   flour             -> bread dough
//   flour + chocolate -> cookie dough
//   flour + berries   -> cupcake batter
public class MixingBowl : Station {
    [Header( "Inscribed: Mixing Bowl" )]
    public SpriteRenderer   flourIcon;      // little icons that show what's in the bowl
    public SpriteRenderer   toppingIcon;
    public WorldBar         progressBar;

    [Header( "Dynamic: Mixing Bowl" )]
    public bool             hasFlour;
    public eItem            topping = eItem.none;
    [Range( 0, 1 )]
    public float            mixProgress;

    private float           lastComplaint;

    public bool HasIngredients { get { return hasFlour || topping != eItem.none; } }

    void Start() {
        RefreshIcons();
    }

    public override void Grab( Baker baker ) {
        Item held = baker.Held;

        // Finished dough waiting to be picked up
        if ( !IsEmpty ) {
            if ( held == null ) GiveToBaker( baker );
            else baker.Complain( "Take the dough out first" );
            return;
        }

        if ( held == null ) {
            // Empty hands on a half-filled bowl: take the last ingredient back out (undo)
            if ( topping != eItem.none ) {
                baker.Hold( ItemBook.SPAWN( topping, Center ) );
                topping = eItem.none;
            } else if ( hasFlour ) {
                baker.Hold( ItemBook.SPAWN( eItem.flour, Center ) );
                hasFlour = false;
            } else {
                return;
            }
            mixProgress = 0;
            SFX.PLAY( eSfx.pickUp );
            RefreshIcons();
            return;
        }

        if ( !held.IsIngredient ) {
            baker.Complain( "Only ingredients go in the bowl" );
            return;
        }

        // Add an ingredient
        eItem added = held.type;
        if ( added == eItem.flour ) {
            if ( hasFlour ) { baker.Complain( "It already has flour" ); return; }
            hasFlour = true;
        } else {
            if ( topping != eItem.none ) { baker.Complain( "Only one topping per bowl" ); return; }
            topping = added;
        }
        Destroy( baker.Release().gameObject );
        mixProgress = 0;
        SFX.PLAY( eSfx.bowlAdd );
        FX.POP( visual );
        if ( added == eItem.flour ) FX.PUFF( Center + Vector2.up * 0.4f, Color.white, 4 );
        RefreshIcons();
    }

    public override void Use( Baker baker ) {
        if ( !IsEmpty ) return;
        if ( !HasIngredients ) {
            baker.Complain( "The bowl is empty" );
        } else if ( !hasFlour ) {
            baker.Complain( "Needs flour!" );
        }
    }

    public override void UseHeld( Baker baker ) {
        if ( !IsEmpty || !hasFlour ) return;

        mixProgress += Time.deltaTime / GameManager.S.MixSeconds;
        SFX.HOLD_LOOP( eLoop.whisk );
        // A gentle wobble while mixing
        if ( visual != null ) visual.localRotation = Quaternion.Euler( 0, 0, Mathf.Sin( Time.time * 30f ) * 4f );

        if ( mixProgress >= 1 ) FinishMixing();
        RefreshIcons();
    }

    void FinishMixing() {
        eItem dough = ItemBook.RECIPE( hasFlour, topping );
        hasFlour = false;
        topping = eItem.none;
        mixProgress = 0;
        item = ItemBook.SPAWN( dough, itemAnchor.position );
        item.AttachTo( itemAnchor );
        if ( visual != null ) visual.localRotation = Quaternion.identity;
        FX.POP( visual, 0.25f );
        FX.PUFF( Center + Vector2.up * 0.4f, new Color( 1f, 0.95f, 0.85f ), 5 );
        SFX.PLAY( eSfx.bowlDone );
    }

    void Update() {
        // Let the bowl settle back when the baker stops mixing
        if ( visual != null ) visual.localRotation = Quaternion.Slerp( visual.localRotation, Quaternion.identity, Time.deltaTime * 10f );
    }

    void RefreshIcons() {
        flourIcon.enabled = hasFlour;
        toppingIcon.enabled = topping != eItem.none;
        if ( topping != eItem.none ) toppingIcon.sprite = ItemBook.GET( topping ).sprite;
        progressBar.Show( mixProgress > 0 && mixProgress < 1 );
        progressBar.Set( mixProgress, new Color( 0.95f, 0.85f, 0.55f ) );
    }

    public override string Prompt( Baker baker ) {
        if ( !IsEmpty ) return ( baker.Held == null ) ? "[Space] Take " + ItemBook.NAME( item.type ) : "";
        if ( baker.Held != null && baker.Held.IsIngredient ) return "[Space] Add " + ItemBook.NAME( baker.Held.type );
        if ( baker.Held == null && hasFlour ) {
            string result = ItemBook.NAME( ItemBook.RECIPE( true, topping ) );
            return "[Hold E] Mix " + result + "    [Space] Take out";
        }
        if ( baker.Held == null && HasIngredients ) return "Needs flour!    [Space] Take out";
        return "Mixing bowl";
    }

    public override void ResetForNewDay() {
        base.ResetForNewDay();
        hasFlour = false;
        topping = eItem.none;
        mixProgress = 0;
        RefreshIcons();
    }
}
