using System.Collections.Generic;
using System.IO;
using UnityEngine;

// A simple bot that plays the game by itself. It's only for testing and balancing:
// it runs when the game is launched with  -autoplay  on the command line, plays every day,
// saves screenshots, and writes each day's results to the log.
//
//   Enzo's Bakery.app/Contents/MacOS/Enzo's Bakery -autoplay -shots ~/Desktop/shots -shotEvery 8
//
// It drives the Baker through the same BakerInput the keyboard uses, so it exercises the real game code.
public class AutoPlayer : MonoBehaviour {
    static public bool ACTIVE { get; private set; }

    enum eAction { grab, use, hold }

    class Task {
        public Interactable     target;
        public eAction          action;
        public System.Func<bool> holdUntil;     // for 'hold': keep holding E until this is true
        public string           label;
    }

    [Header( "Dynamic" )]
    public string           currentTask;

    private Baker           baker;
    private Task            task;
    private List<Vector2Int> path = new List<Vector2Int>();
    private float           taskTimer;
    private string          shotFolder;
    private float           shotEvery = 10f;
    private float           nextShot;
    private int             shotCount;
    private float           menuTimer;
    private bool            menuShotTaken;
    private bool            visitedHowTo;
    private bool            pausedOnce;
    private float           timeScale = 1;
    private Dictionary<MixingBowl, eItem> bowlPlan = new Dictionary<MixingBowl, eItem>();
    private MixingBowl      activeBowl;     // the bowl the current recipe is going into

    // The walkable kitchen floor, in whole-tile cells (x from -10 to 9, y from -4 to -1)
    const int MIN_X = -10, MAX_X = 9, MIN_Y = -4, MAX_Y = -1;

    void Awake() {
        string[] args = System.Environment.GetCommandLineArgs();
        for ( int i = 0; i < args.Length; i++ ) {
            if ( args[i] == "-autoplay" ) ACTIVE = true;
            if ( args[i] == "-shots" && i + 1 < args.Length ) shotFolder = args[i + 1];
            if ( args[i] == "-shotEvery" && i + 1 < args.Length ) float.TryParse( args[i + 1], out shotEvery );
            if ( args[i] == "-timeScale" && i + 1 < args.Length ) float.TryParse( args[i + 1], out timeScale );
        }
        if ( !ACTIVE ) {
            enabled = false;
            return;
        }
        if ( !string.IsNullOrEmpty( shotFolder ) ) Directory.CreateDirectory( shotFolder );
        Debug.Log( "[AutoPlayer] active. shots=" + shotFolder + " every " + shotEvery + "s, timeScale " + timeScale );
    }

    void Start() {
        baker = Baker.S;
        baker.botControlled = true;
    }

    void Update() {
        GameManager gm = GameManager.S;
        if ( gm.state == eGameState.playing ) Time.timeScale = timeScale;

        if ( shotFolder != null && Time.unscaledTime >= nextShot ) {
            Shot( gm.state.ToString() );
            nextShot = Time.unscaledTime + shotEvery;
        }

        // Click through the menus after a short pause, so each screen shows up in a screenshot
        if ( gm.state != eGameState.playing ) {
            baker.botInput = new BakerInput();
            menuTimer += Mathf.Min( Time.unscaledDeltaTime, 0.1f );    // the first frame after loading can be very long
            if ( !menuShotTaken && menuTimer > 0.8f ) {
                if ( gm.state == eGameState.shop ) Shop( gm );
                Shot( gm.state.ToString() );
                menuShotTaken = true;
            }
            if ( menuTimer < 1.6f ) return;
            menuTimer = 0;
            menuShotTaken = false;
            switch ( gm.state ) {
            case eGameState.title:
                // Visit the How to Play screen once, so it gets a screenshot too
                if ( !visitedHowTo ) { visitedHowTo = true; gm.ShowHowToPlay(); }
                else gm.StartNewGame();
                break;
            case eGameState.howToPlay:  gm.GoToTitle(); break;
            case eGameState.paused:     gm.Resume(); break;
            case eGameState.dayIntro:   gm.BeginDay(); break;
            case eGameState.dayResults: LogDay( gm ); gm.ContinueFromResults(); break;
            case eGameState.shop:       gm.NextDay(); break;
            case eGameState.victory:    Debug.Log( "[AutoPlayer] VICTORY" ); Application.Quit(); break;
            }
            return;
        }

        // Pause once on the first day to check the pause menu
        if ( !pausedOnce && gm.dayTimer > 40f ) {
            pausedOnce = true;
            gm.Pause();
            return;
        }
        baker.botInput = Think();
    }

    // ------------------------------------------------------------------ deciding what to do

    BakerInput Think() {
        taskTimer += Time.deltaTime;
        if ( task == null || !StillValid( task ) || taskTimer > 12f ) {
            task = Plan();
            taskTimer = 0;
            path.Clear();
            if ( task == null ) {
                currentTask = "idle";
                return new BakerInput();
            }
            currentTask = task.label;
            path = FindPath( Cell( baker.transform.position ), task.target );
        }
        return Execute();
    }

    bool StillValid( Task t ) {
        return t.target != null && t.target.Targetable;
    }

    Task Plan() {
        Item held = baker.Held;
        List<Customer> customers = Waiting( eCustomerState.waitingForFood );

        if ( held != null ) {
            // Something a customer wants: serve the most impatient one
            Customer wanting = null;
            foreach ( Customer c in customers ) {
                if ( Wants( c, held.type ) && ( wanting == null || c.PatienceFrac < wanting.PatienceFrac ) ) wanting = c;
            }
            if ( wanting != null && wanting.spot != null ) return Do( wanting.spot, eAction.grab, "serve " + held.type );

            if ( held.type == eItem.burnt ) return Do( Find<TrashBin>( t => true ), eAction.grab, "trash" );
            if ( held.type == eItem.cupcake ) {
                FrostingStation fs = Find<FrostingStation>( f => f.IsEmpty );
                if ( fs != null ) return Do( fs, eAction.grab, "to frosting" );
            }
            if ( held.IsDough ) {
                Oven oven = Find<Oven>( o => o.IsEmpty && !o.locked );
                if ( oven != null ) return Do( oven, eAction.grab, "bake" );
            }
            if ( held.IsIngredient ) {
                MixingBowl bowl = BowlFor( held.type );
                if ( bowl != null ) return Do( bowl, eAction.grab, "add " + held.type );
                return Do( Find<IngredientCrate>( c => c.ingredient == held.type ), eAction.grab, "put back" );
            }
            Counter free = Find<Counter>( c => c.IsEmpty );
            if ( free != null ) return Do( free, eAction.grab, "stash " + held.type );
            return Do( Find<TrashBin>( t => true ), eAction.grab, "trash (no room)" );
        }

        // Empty hands. Rescue anything that's done baking first.
        Oven doneOven = Find<Oven>( o => o.state == eOvenState.perfect || o.state == eOvenState.ready || o.state == eOvenState.burnt );
        if ( doneOven != null ) return Do( doneOven, eAction.grab, "take out of oven" );

        foreach ( Customer c in Waiting( eCustomerState.waitingToOrder ) ) {
            if ( c.spot != null ) return Do( c.spot, eAction.use, "take order" );
        }

        FrostingStation frost = Find<FrostingStation>( f => !f.IsEmpty );
        if ( frost != null ) {
            if ( frost.item.type == eItem.cupcake ) return Hold( frost, () => frost.IsEmpty || frost.item.type != eItem.cupcake, "frost" );
            return Do( frost, eAction.grab, "pick up frosted" );
        }

        Counter stocked = Find<Counter>( c => !c.IsEmpty && SomeoneWants( c.item.type ) );
        if ( stocked != null ) return Do( stocked, eAction.grab, "pick up stock" );

        foreach ( MixingBowl b in All<MixingBowl>() ) {
            if ( !b.IsEmpty && Find<Oven>( o => o.IsEmpty && !o.locked ) != null ) return Do( b, eAction.grab, "take dough" );
            if ( b.IsEmpty && b.hasFlour ) {
                eItem plan = bowlPlan.ContainsKey( b ) ? bowlPlan[b] : eItem.bread;
                eItem topping = ToppingFor( plan );
                if ( topping != eItem.none && b.topping == eItem.none ) {
                    return Do( Find<IngredientCrate>( c => c.ingredient == topping ), eAction.grab, "get " + topping );
                }
                return Hold( b, () => !b.IsEmpty, "mix" );
            }
        }

        // Start something new that somebody is waiting for
        eItem needed = NextNeeded();
        if ( needed != eItem.none ) {
            MixingBowl bowl = Find<MixingBowl>( b => b.IsEmpty && !b.HasIngredients );
            if ( bowl != null && Find<Oven>( o => o.IsEmpty && !o.locked ) != null ) {
                bowlPlan[bowl] = needed;
                activeBowl = bowl;
                return Do( Find<IngredientCrate>( c => c.ingredient == eItem.flour ), eAction.grab, "get flour for " + needed );
            }
        }
        return null;
    }

    // What should be baked next: something ordered that isn't already on its way
    eItem NextNeeded() {
        Dictionary<eItem, int> need = new Dictionary<eItem, int>();
        foreach ( Customer c in Waiting( eCustomerState.waitingForFood ) ) {
            for ( int i = 0; i < c.order.Count; i++ ) {
                if ( c.delivered[i] ) continue;
                need[c.order[i]] = ( need.ContainsKey( c.order[i] ) ? need[c.order[i]] : 0 ) + 1;
            }
        }
        // Subtract everything in the pipeline (in bowls, ovens, on counters, in hand)
        foreach ( Interactable it in Interactable.ALL ) {
            Station s = it as Station;
            if ( s == null || s.item == null ) continue;
            Reduce( need, Product( s.item.type ) );
        }
        foreach ( KeyValuePair<MixingBowl, eItem> kv in bowlPlan ) {
            if ( kv.Key.IsEmpty && kv.Key.HasIngredients ) Reduce( need, kv.Value );
        }
        foreach ( KeyValuePair<eItem, int> kv in need ) if ( kv.Value > 0 ) return kv.Key;
        return eItem.none;
    }

    static void Reduce( Dictionary<eItem, int> need, eItem product ) {
        if ( product != eItem.none && need.ContainsKey( product ) ) need[product]--;
    }

    // What an item will eventually become
    static eItem Product( eItem item ) {
        switch ( item ) {
        case eItem.breadDough: case eItem.bread: return eItem.bread;
        case eItem.cookieDough: case eItem.cookies: return eItem.cookies;
        case eItem.cupcakeBatter: case eItem.cupcake: case eItem.frostedCupcake: return eItem.frostedCupcake;
        }
        return eItem.none;
    }

    static eItem ToppingFor( eItem product ) {
        if ( product == eItem.cookies ) return eItem.chocolate;
        if ( product == eItem.frostedCupcake ) return eItem.berries;
        return eItem.none;
    }

    MixingBowl BowlFor( eItem ingredient ) {
        if ( ingredient == eItem.flour ) {
            if ( activeBowl != null && activeBowl.IsEmpty && !activeBowl.hasFlour ) return activeBowl;
            MixingBowl any = Find<MixingBowl>( b => b.IsEmpty && !b.hasFlour );
            if ( any != null ) bowlPlan[any] = eItem.bread;   // no plan for this one: just make bread
            return any;
        }
        return Find<MixingBowl>( b => b.IsEmpty && b.hasFlour && b.topping == eItem.none );
    }

    bool Wants( Customer c, eItem item ) {
        for ( int i = 0; i < c.order.Count; i++ ) if ( !c.delivered[i] && c.order[i] == item ) return true;
        return false;
    }

    bool SomeoneWants( eItem item ) {
        foreach ( Customer c in Waiting( eCustomerState.waitingForFood ) ) if ( Wants( c, item ) ) return true;
        return false;
    }

    static List<Customer> Waiting( eCustomerState state ) {
        List<Customer> list = new List<Customer>();
        foreach ( Customer c in CustomerSpawner.S.customers ) if ( c != null && c.state == state ) list.Add( c );
        return list;
    }

    // ------------------------------------------------------------------ doing it

    static Task Do( Interactable target, eAction action, string label ) {
        if ( target == null ) return null;
        return new Task { target = target, action = action, label = label };
    }

    static Task Hold( Interactable target, System.Func<bool> until, string label ) {
        return new Task { target = target, action = eAction.hold, holdUntil = until, label = label };
    }

    BakerInput Execute() {
        BakerInput input = new BakerInput();
        Vector2 pos = baker.transform.position;

        // Walk the path one cell at a time
        if ( path.Count > 0 ) {
            Vector2 goal = CellCenter( path[0] );
            Vector2 delta = goal - pos;
            if ( delta.magnitude < 0.08f ) {
                path.RemoveAt( 0 );
            } else {
                input.move = delta.normalized;
                return input;
            }
            if ( path.Count > 0 ) {
                input.move = ( CellCenter( path[0] ) - pos ).normalized;
                return input;
            }
        }

        // At the right cell: turn to face the target
        Vector2Int here = Cell( pos );
        Vector2Int there = Cell( task.target.Center );
        Vector2 face = new Vector2( Mathf.Clamp( there.x - here.x, -1, 1 ), Mathf.Clamp( there.y - here.y, -1, 1 ) );
        if ( baker.target != task.target ) {
            input.move = face * 0.2f;
            if ( taskTimer > 4f ) task = null;  // can't reach it, rethink
            return input;
        }

        switch ( task.action ) {
        case eAction.grab:
            input.grab = true;
            task = null;
            break;
        case eAction.use:
            input.use = true;
            task = null;
            break;
        case eAction.hold:
            input.useHeld = true;
            if ( task.holdUntil() ) task = null;
            break;
        }
        return input;
    }

    // ------------------------------------------------------------------ path finding on the kitchen grid

    static Vector2Int Cell( Vector2 p ) {
        return new Vector2Int( Mathf.FloorToInt( p.x ), Mathf.FloorToInt( p.y ) );
    }

    static Vector2 CellCenter( Vector2Int c ) {
        return new Vector2( c.x + 0.5f, c.y + 0.5f );
    }

    static bool Walkable( Vector2Int c ) {
        if ( c.x < MIN_X || c.x > MAX_X || c.y < MIN_Y || c.y > MAX_Y ) return false;
        // The island counter in the middle of the kitchen, and the plants in the top corners
        if ( c.y == -2 && c.x >= -2 && c.x <= 1 ) return false;
        if ( c.y == -1 && ( c.x == MIN_X || c.x == MAX_X ) ) return false;
        return true;
    }

    // Breadth-first search to any walkable cell next to the target's cell
    List<Vector2Int> FindPath( Vector2Int start, Interactable target ) {
        Vector2Int goalCell = Cell( target.Center );
        HashSet<Vector2Int> goals = new HashSet<Vector2Int>();
        foreach ( Vector2Int d in DIRS ) if ( Walkable( goalCell + d ) ) goals.Add( goalCell + d );
        if ( !Walkable( start ) ) start = NearestWalkable( start );

        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Queue<Vector2Int> open = new Queue<Vector2Int>();
        open.Enqueue( start );
        cameFrom[start] = start;
        while ( open.Count > 0 ) {
            Vector2Int c = open.Dequeue();
            if ( goals.Contains( c ) ) {
                List<Vector2Int> result = new List<Vector2Int>();
                while ( c != start ) {
                    result.Insert( 0, c );
                    c = cameFrom[c];
                }
                result.Insert( 0, start );
                return result;
            }
            foreach ( Vector2Int d in DIRS ) {
                Vector2Int n = c + d;
                if ( Walkable( n ) && !cameFrom.ContainsKey( n ) ) {
                    cameFrom[n] = c;
                    open.Enqueue( n );
                }
            }
        }
        return new List<Vector2Int>();
    }

    static Vector2Int NearestWalkable( Vector2Int c ) {
        c.x = Mathf.Clamp( c.x, MIN_X, MAX_X );
        c.y = Mathf.Clamp( c.y, MIN_Y, MAX_Y );
        if ( !Walkable( c ) ) c.y = -1;
        return c;
    }

    static readonly Vector2Int[] DIRS = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    // ------------------------------------------------------------------ helpers

    static T Find<T>( System.Predicate<T> match ) where T : Interactable {
        // Prefer the closest match so the bot doesn't run back and forth
        T best = null;
        float bestDist = float.MaxValue;
        Vector2 from = Baker.S.transform.position;
        foreach ( Interactable it in Interactable.ALL ) {
            T t = it as T;
            if ( t == null || !t.Targetable || !match( t ) ) continue;
            float d = Vector2.Distance( from, t.Center );
            if ( d < bestDist ) {
                bestDist = d;
                best = t;
            }
        }
        return best;
    }

    static List<T> All<T>() where T : Interactable {
        List<T> list = new List<T>();
        foreach ( Interactable it in Interactable.ALL ) if ( it is T ) list.Add( (T) it );
        return list;
    }

    void Shop( GameManager gm ) {
        // Buy upgrades in a sensible order while money lasts
        eUpgrade[] priority = { eUpgrade.thirdOven, eUpgrade.turboOven, eUpgrade.comfyChairs, eUpgrade.quickWhisk, eUpgrade.runningShoes, eUpgrade.tipJar };
        foreach ( eUpgrade u in priority ) {
            UpgradeDef def = gm.GetUpgrade( u );
            if ( def != null && !gm.Has( u ) && gm.savings >= def.price ) {
                gm.BuyUpgrade( u );
                Debug.Log( "[AutoPlayer] bought " + u );
            }
        }
        gm.menus.Show( eGameState.shop );     // refresh the cards
    }

    void LogDay( GameManager gm ) {
        DayStats s = gm.stats;
        Debug.Log( "[AutoPlayer] " + gm.Day.title + ( gm.lastDayPassed ? " PASSED" : " FAILED" ) +
                   " earned $" + s.earnings + " / goal $" + gm.Day.goal +
                   " served " + s.served + " lost " + s.lost + " perfect " + s.perfectBakes +
                   " burnt " + s.burnt + " wrong " + s.wrongItems + " tips $" + s.tips +
                   " bestStreak " + s.bestStreak + " stars " + gm.Stars( s, gm.Day.goal ) +
                   " spawned " + CustomerSpawner.S.spawnedToday + " savings $" + gm.savings );
    }

    void Shot( string label ) {
        if ( string.IsNullOrEmpty( shotFolder ) ) return;
        shotCount++;
        string file = Path.Combine( shotFolder, shotCount.ToString( "000" ) + "_" + label + ".png" );
        ScreenCapture.CaptureScreenshot( file );
    }
}
