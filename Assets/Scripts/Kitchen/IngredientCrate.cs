using UnityEngine;

// An endless supply of one ingredient. Walk up, press Space, and it's in your hands.
public class IngredientCrate : Interactable {
    [Header( "Inscribed" )]
    public eItem            ingredient = eItem.flour;
    public SpriteRenderer   icon;           // shows what's inside
    public SpriteRenderer   lockIcon;       // shown until the ingredient is on the menu
    public Transform        visual;

    [Header( "Dynamic" )]
    public bool             locked;

    public void SetLocked( bool isLocked ) {
        locked = isLocked;
        if ( lockIcon != null ) lockIcon.enabled = isLocked;
        if ( icon != null ) icon.color = isLocked ? new Color( 0.4f, 0.4f, 0.4f, 0.8f ) : Color.white;
    }

    public override void Grab( Baker baker ) {
        if ( locked ) {
            baker.Complain( ItemBook.NAME( ingredient ) + " unlocks on Day 2" );
            return;
        }
        if ( baker.Held == null ) {
            baker.Hold( ItemBook.SPAWN( ingredient, transform.position ) );
            SFX.PLAY( ingredient == eItem.flour ? eSfx.grabFlour : eSfx.grabSoft );
            FX.POP( visual, 0.12f );
            if ( ingredient == eItem.flour ) FX.PUFF( Center + Vector2.up * 0.3f, new Color( 1, 1, 1, 0.8f ), 3 );
        } else if ( baker.Held.type == ingredient ) {
            // Changed your mind? Put it back.
            Destroy( baker.Release().gameObject );
            SFX.PLAY( eSfx.putDown );
        } else {
            baker.Complain( "Hands full!" );
        }
    }

    public override string Prompt( Baker baker ) {
        if ( locked ) return ItemBook.NAME( ingredient ) + " (locked)";
        if ( baker.Held == null ) return "[Space] Take " + ItemBook.NAME( ingredient );
        if ( baker.Held.type == ingredient ) return "[Space] Put back " + ItemBook.NAME( ingredient );
        return "";
    }
}
