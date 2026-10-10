using UnityEngine;

// Cupcakes come out of the oven plain. Put one here and hold E to pipe on the pink frosting.
public class FrostingStation : Station {
    [Header( "Inscribed: Frosting" )]
    public WorldBar     progressBar;
    public float        frostSeconds = 1.2f;

    [Header( "Dynamic: Frosting" )]
    [Range( 0, 1 )]
    public float        progress;

    private float       nextSquish;

    public override void Grab( Baker baker ) {
        if ( baker.Held != null ) {
            if ( !IsEmpty ) { baker.Complain( "No room here!" ); return; }
            if ( baker.Held.type != eItem.cupcake ) { baker.Complain( "Only plain cupcakes get frosting" ); return; }
            TakeFromBaker( baker );
            progress = 0;
        } else if ( !IsEmpty ) {
            GiveToBaker( baker );
            progress = 0;
        }
        Refresh();
    }

    public override void UseHeld( Baker baker ) {
        if ( IsEmpty || item.type != eItem.cupcake ) return;

        progress += Time.deltaTime / ( frostSeconds * GameManager.S.MixSeconds / GameManager.BASE_MIX_SECONDS );
        if ( Time.time >= nextSquish ) {
            SFX.PLAY( eSfx.frosting, 0.7f );
            nextSquish = Time.time + 0.3f;
        }
        if ( progress >= 1 ) {
            item.SetType( eItem.frostedCupcake );
            progress = 0;
            FX.POP( item.transform, 0.3f );
            FX.SPARKLES( Center + Vector2.up * 0.6f, 4 );
            SFX.PLAY( eSfx.confirm, 0.6f );
        }
        Refresh();
    }

    public override void Use( Baker baker ) {
        if ( IsEmpty ) baker.Complain( "Put a baked cupcake here first" );
        else if ( item.type != eItem.cupcake ) baker.Complain( "It's already frosted!" );
    }

    void Refresh() {
        progressBar.Show( progress > 0 && progress < 1 );
        progressBar.Set( progress, new Color( 1f, 0.55f, 0.75f ) );
    }

    public override string Prompt( Baker baker ) {
        if ( baker.Held != null && baker.Held.type == eItem.cupcake && IsEmpty ) return "[Space] Put cupcake down to frost";
        if ( !IsEmpty && baker.Held == null ) {
            return ( item.type == eItem.cupcake ) ? "[Hold E] Frost    [Space] Pick up" : "[Space] Pick up " + ItemBook.NAME( item.type );
        }
        return "Frosting station";
    }

    public override void ResetForNewDay() {
        base.ResetForNewDay();
        progress = 0;
        Refresh();
    }
}
