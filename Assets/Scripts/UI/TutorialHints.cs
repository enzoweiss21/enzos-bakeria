using TMPro;
using UnityEngine;

// On Day 1 a hint box walks the player through their first order.
// Instead of a fixed script, it looks at what's going on right now and shows the next step,
// so it can never get "stuck" if the player does things in a different order.
// On later days it only pops up once, when something new appears (frosting, the critic).
public class TutorialHints : MonoBehaviour {
    [Header( "Inscribed" )]
    public TextMeshProUGUI  hintText;
    public CanvasGroup      group;
    public MixingBowl[]     bowls;
    public Oven[]           ovens;

    [Header( "Dynamic" )]
    public bool             firstServeDone;
    public float            doneMessageTimer;
    public bool             frostingHintShown;
    public bool             criticHintShown;

    private string          special;        // a one-off hint for later days
    private float           specialTimer;
    private int             lastDay = -1;

    void Update() {
        GameManager gm = GameManager.S;
        if ( gm == null ) return;
        if ( gm.dayIndex != lastDay ) {
            lastDay = gm.dayIndex;
            firstServeDone = gm.dayIndex > 0;
        }

        string hint = ( gm.dayIndex == 0 ) ? DayOneHint( gm ) : LaterDayHint( gm );
        bool show = !string.IsNullOrEmpty( hint );
        if ( show ) hintText.text = hint;
        group.alpha = Mathf.MoveTowards( group.alpha, show ? 1 : 0, Time.unscaledDeltaTime * 4f );
    }

    string DayOneHint( GameManager gm ) {
        if ( firstServeDone ) {
            if ( doneMessageTimer <= 0 ) return "";
            doneMessageTimer -= Time.deltaTime;
            return "Nice! Serve fast for bigger tips, and reach the goal before 4 PM.\nTip: pet Biscuit the cat (E) when customers get grumpy.";
        }
        if ( gm.stats.served > 0 ) {
            firstServeDone = true;
            doneMessageTimer = 7f;
            return "";
        }

        Baker baker = gm.baker;
        Customer waiting = FindCustomer( eCustomerState.waitingToOrder );
        Customer ordered = FindCustomer( eCustomerState.waitingForFood );

        if ( waiting == null && ordered == null ) {
            return "Walk with WASD. Your first customer is on the way!";
        }
        if ( ordered == null ) {
            return "A customer is at the counter!\nStand in front of them and press E to take their order.";
        }

        eItem wanted = ordered.order[0];
        if ( baker.Held != null ) {
            Item held = baker.Held;
            if ( held.type == wanted ) return "Serve it! Face the customer and press Space.";
            if ( held.IsIngredient ) {
                return "Put the " + ItemBook.NAME( held.type ) + " in a mixing bowl (Space).";
            }
            if ( held.IsDough ) return "Put the dough in an oven (Space).";
            if ( held.type == eItem.burnt ) return "Oops, it burnt! Throw it in the trash can (Space).";
            return "Put it down on a counter (Space), or serve it to someone who wants it.";
        }

        // Empty hands: what's the next step?
        foreach ( Oven o in ovens ) {
            if ( o.state == eOvenState.perfect || o.state == eOvenState.ready ) {
                return "Ding! It's done. Grab it from the oven (Space) right away for a PERFECT bake.";
            }
        }
        foreach ( Oven o in ovens ) {
            if ( o.state == eOvenState.baking ) return "Baking... take it out right when it dings for a PERFECT bake and a bigger tip.";
        }
        foreach ( MixingBowl b in bowls ) {
            if ( !b.IsEmpty ) return "Dough's ready! Pick it up from the bowl (Space).";
            if ( b.hasFlour && wanted == eItem.cookies && b.topping == eItem.none ) {
                return "Cookies need chocolate too! Grab some from the crate and add it to the bowl.";
            }
            if ( b.hasFlour ) return "Hold E at the bowl to mix the dough.";
        }
        if ( wanted == eItem.cookies ) return "They want cookies: flour + chocolate.\nGrab flour from the sack (Space).";
        return "They want bread: just flour.\nGrab flour from the sack (Space).";
    }

    string LaterDayHint( GameManager gm ) {
        // Point out new things the first time they show up
        if ( !frostingHintShown && gm.Day.berriesUnlocked && FindOrderFor( eItem.frostedCupcake ) ) {
            frostingHintShown = true;
            ShowSpecial( "Cupcakes: flour + berries, bake, then hold E at the frosting station (pink piping bag)." );
        }
        if ( !criticHintShown && FindCritic() ) {
            criticHintShown = true;
            ShowSpecial( "The food critic is here! Only PERFECT bakes will impress them... but they pay triple." );
        }
        if ( specialTimer > 0 ) {
            specialTimer -= Time.deltaTime;
            return special;
        }
        return "";
    }

    void ShowSpecial( string text ) {
        special = text;
        specialTimer = 7f;
    }

    static Customer FindCustomer( eCustomerState state ) {
        if ( CustomerSpawner.S == null ) return null;
        foreach ( Customer c in CustomerSpawner.S.customers ) if ( c != null && c.state == state ) return c;
        return null;
    }

    static bool FindOrderFor( eItem item ) {
        if ( CustomerSpawner.S == null ) return false;
        foreach ( Customer c in CustomerSpawner.S.customers ) {
            if ( c != null && c.state == eCustomerState.waitingForFood && c.order.Contains( item ) ) return true;
        }
        return false;
    }

    static bool FindCritic() {
        if ( CustomerSpawner.S == null ) return false;
        foreach ( Customer c in CustomerSpawner.S.customers ) {
            if ( c != null && c.type == eCustomerType.critic && c.IsAtCounter ) return true;
        }
        return false;
    }
}
