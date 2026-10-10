using UnityEngine;

// A plain countertop: put anything down, pick it back up later.
// Handy for keeping a fresh loaf ready before anyone has even asked for it.
public class Counter : Station {
    public override void Grab( Baker baker ) {
        if ( baker.Held != null && IsEmpty ) {
            TakeFromBaker( baker );
        } else if ( baker.Held == null && !IsEmpty ) {
            GiveToBaker( baker );
        } else if ( baker.Held != null ) {
            baker.Complain( "No room here!" );
        }
    }

    public override string Prompt( Baker baker ) {
        if ( baker.Held != null && IsEmpty ) return "[Space] Put down " + ItemBook.NAME( baker.Held.type );
        if ( baker.Held == null && !IsEmpty ) return "[Space] Pick up " + ItemBook.NAME( item.type );
        return "";
    }
}
