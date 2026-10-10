using UnityEngine;

// A piece of kitchen furniture that can hold one item (counters, bowls, ovens, the frosting table).
// Subclasses decide what they accept and what happens to the item while it's there.
public class Station : Interactable {
    [Header( "Inscribed: Station" )]
    public string       stationName = "Counter";
    public Transform    itemAnchor;         // where a held item sits on top of the furniture
    public Transform    visual;             // the sprite that "pops" when something lands on it

    [Header( "Dynamic: Station" )]
    public Item         item;

    public bool IsEmpty { get { return item == null; } }

    // Moves the item from the baker's hands onto this station
    protected void TakeFromBaker( Baker baker ) {
        item = baker.Release();
        item.AttachTo( itemAnchor );
        FX.POP( visual );
        SFX.PLAY( eSfx.putDown );
    }

    // Moves this station's item into the baker's hands
    protected void GiveToBaker( Baker baker ) {
        Item it = item;
        item = null;
        baker.Hold( it );
        SFX.PLAY( eSfx.pickUp );
    }

    // Called at the start of every day so nothing is left over from yesterday
    public virtual void ResetForNewDay() {
        if ( item != null ) Destroy( item.gameObject );
        item = null;
    }
}
