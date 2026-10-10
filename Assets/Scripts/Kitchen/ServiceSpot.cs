using UnityEngine;

// The patch of service counter in front of one customer spot.
// Facing it from the kitchen side lets the baker take that customer's order and hand over food.
public class ServiceSpot : Interactable {
    [Header( "Inscribed" )]
    [Tooltip( "Where the customer stands while they wait (on the shop side of the counter)" )]
    public Transform    standPoint;

    [Header( "Dynamic" )]
    public Customer     occupant;

    public override bool Targetable { get { return occupant != null && occupant.IsAtCounter; } }

    public override void Grab( Baker baker ) {
        Interact( baker );
    }

    public override void Use( Baker baker ) {
        Interact( baker );
    }

    // Space and E both work at the counter, so players never press the "wrong" key here
    void Interact( Baker baker ) {
        if ( occupant == null ) return;
        if ( occupant.state == eCustomerState.waitingToOrder ) {
            occupant.TakeOrder();
        } else if ( occupant.state == eCustomerState.waitingForFood ) {
            if ( baker.Held == null ) {
                occupant.RemindOrder();
            } else {
                occupant.TryServe( baker );
            }
        }
    }

    public override string Prompt( Baker baker ) {
        if ( occupant == null ) return "";
        if ( occupant.state == eCustomerState.waitingToOrder ) return "[E] Take order";
        if ( occupant.state == eCustomerState.waitingForFood ) {
            return ( baker.Held != null ) ? "[Space] Serve " + ItemBook.NAME( baker.Held.type ) : "Wants: " + occupant.OrderText;
        }
        return "";
    }
}
