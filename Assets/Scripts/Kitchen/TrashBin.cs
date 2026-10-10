using UnityEngine;

// For burnt cookies and mistakes
public class TrashBin : Interactable {
    [Header( "Inscribed" )]
    public Transform visual;

    public override void Grab( Baker baker ) {
        if ( baker.Held == null ) return;
        Item tossed = baker.Release();
        FX.TEXT( "Tossed", Center + Vector2.up * 0.8f, new Color( 0.8f, 0.8f, 0.8f ), 0.8f );
        Destroy( tossed.gameObject );
        SFX.PLAY( eSfx.trash );
        FX.POP( visual, 0.25f );
    }

    public override string Prompt( Baker baker ) {
        return ( baker.Held != null ) ? "[Space] Throw away " + ItemBook.NAME( baker.Held.type ) : "Trash";
    }
}
