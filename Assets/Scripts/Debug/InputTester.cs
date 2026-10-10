using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Tests the real controls. Launch the game with  -keytest  and it runs through a checklist by pressing keys
// on a virtual keyboard and clicking a virtual mouse, exactly the way a player would, so the actual
// keyboard code in the Baker, MenuScreens, GameManager and MusicDirector is what gets tested.
// Every check is written to the log as PASS or FAIL, and the game quits when it's done.
public class InputTester : MonoBehaviour {
    static public bool ACTIVE { get; private set; }

    private Keyboard        kb;
    private Mouse           mouse;
    private HashSet<Key>    down = new HashSet<Key>();
    private GameManager     gm;
    private Baker           baker;
    private int             passed, failed;
    private string          shotFolder;
    private int             shotCount;

    void Awake() {
        string[] args = System.Environment.GetCommandLineArgs();
        for ( int i = 0; i < args.Length; i++ ) {
            if ( args[i] == "-keytest" ) ACTIVE = true;
            if ( args[i] == "-shots" && i + 1 < args.Length ) shotFolder = args[i + 1];
        }
        if ( !ACTIVE ) enabled = false;
    }

    void Start() {
        if ( !ACTIVE ) return;
        // Keep the game and the virtual devices running even when the window isn't in front
        GameManager.PAUSE_ON_FOCUS_LOSS = false;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        kb = InputSystem.AddDevice<Keyboard>( "TestKeyboard" );
        kb.MakeCurrent();
        mouse = InputSystem.AddDevice<Mouse>( "TestMouse" );
        mouse.MakeCurrent();
        gm = GameManager.S;
        baker = Baker.S;
        if ( !string.IsNullOrEmpty( shotFolder ) ) Directory.CreateDirectory( shotFolder );
        StartCoroutine( Checklist() );
    }

    // ------------------------------------------------------------------ the checklist

    IEnumerator Checklist() {
        yield return new WaitForSecondsRealtime( 1.5f );
        MusicDirector music = FindAnyObjectByType<MusicDirector>();

        // Menus
        Check( "the game starts on the title screen", gm.state == eGameState.title );
        yield return Click( gm.menus.howToButton );
        Check( "clicking 'How to Play' with the mouse opens it", gm.state == eGameState.howToPlay );
        yield return Press( Key.Escape );
        Check( "Esc goes back to the title", gm.state == eGameState.title );
        yield return Press( Key.H );
        Check( "H opens How to Play", gm.state == eGameState.howToPlay );
        yield return Press( Key.Space );
        Check( "Space goes back to the title", gm.state == eGameState.title );
        yield return Press( Key.M );
        Check( "M turns the music off", music.muted );
        yield return Press( Key.M );
        Check( "M turns the music back on", !music.muted );
        yield return Press( Key.Space );
        Check( "Space on the title starts Day 1", gm.state == eGameState.dayIntro );
        yield return Press( Key.Enter );
        Check( "Enter opens the doors", gm.state == eGameState.playing );

        // Walking: WASD and the arrow keys
        yield return Move( Key.D, Vector2.right, "D walks right" );
        yield return Move( Key.A, Vector2.left, "A walks left" );
        yield return Move( Key.W, Vector2.up, "W walks up" );
        yield return Move( Key.S, Vector2.down, "S walks down" );
        yield return Move( Key.RightArrow, Vector2.right, "Right arrow walks right" );
        yield return Move( Key.LeftArrow, Vector2.left, "Left arrow walks left" );
        yield return Move( Key.UpArrow, Vector2.up, "Up arrow walks up" );
        yield return Move( Key.DownArrow, Vector2.down, "Down arrow walks down" );

        // The whole loop: flour -> bowl -> mix -> oven -> serve
        IngredientCrate flour = Find<IngredientCrate>( c => c.ingredient == eItem.flour );
        yield return WalkTo( flour );
        yield return Press( Key.Space );
        Check( "Space at the flour sack picks up flour", Holding( eItem.flour ) );
        yield return Press( Key.Space );
        Check( "Space at the sack again puts the flour back", baker.Held == null );
        yield return Press( Key.Space );

        MixingBowl bowl = Find<MixingBowl>( b => true );
        yield return WalkTo( bowl );
        yield return Press( Key.Space );
        Check( "Space puts the flour in a mixing bowl", bowl.hasFlour && baker.Held == null );
        yield return Hold( Key.E, 2.2f );
        Check( "holding E mixes the flour into bread dough", !bowl.IsEmpty && bowl.item.type == eItem.breadDough );
        yield return Press( Key.Space );
        Check( "Space picks up the dough", Holding( eItem.breadDough ) );

        Oven oven = Find<Oven>( o => !o.locked );
        yield return WalkTo( oven );
        yield return Press( Key.Space );
        Check( "Space puts the dough in the oven", oven.state == eOvenState.baking );
        float t = 0;
        while ( oven.state == eOvenState.baking && t < 15f ) { t += Time.deltaTime; yield return null; }
        yield return Press( Key.Space, 0.05f );
        Check( "Space right after the ding takes out a PERFECT loaf", Holding( eItem.bread ) && baker.Held.perfect );
        Shot( "perfect_bread" );

        // Wait for a customer and serve them
        Customer customer = null;
        for ( t = 0; t < 40f && customer == null; t += Time.deltaTime ) {
            foreach ( Customer c in CustomerSpawner.S.customers ) if ( c != null && c.state == eCustomerState.waitingToOrder ) customer = c;
            yield return null;
        }
        Check( "a customer comes to the counter", customer != null );
        if ( customer != null ) {
            yield return WalkTo( customer.spot );
            yield return Press( Key.E );
            Check( "E takes the customer's order", customer.state == eCustomerState.waitingForFood );
            bool wantsBread = customer.order.Contains( eItem.bread );
            int servedBefore = gm.stats.served;
            yield return Press( Key.Space );
            if ( wantsBread ) {
                Check( "Space serves them the bread and they pay", gm.stats.served == servedBefore + 1 && gm.stats.earnings > 0 );
            } else {
                Check( "Space with the wrong item is refused", Holding( eItem.bread ) && gm.stats.wrongItems > 0 );
                yield return WalkTo( Find<TrashBin>( x => true ) );
                yield return Press( Key.Space );
                Check( "Space at the trash throws the bread away", baker.Held == null );
            }
            Shot( "served" );
        }

        // Pet the cat (she wanders, so try a few times)
        BakeryCat cat = gm.cat;
        for ( int tries = 0; tries < 4 && !cat.IsNapping; tries++ ) {
            yield return WalkTo( cat );
            if ( baker.target == cat ) yield return Press( Key.E, 0.05f );
        }
        Check( "E pets Biscuit (she takes a nap)", cat.IsNapping );

        // Pause menu
        yield return Press( Key.Escape );
        Check( "Esc pauses", gm.state == eGameState.paused && Time.timeScale == 0 );
        Shot( "paused" );
        yield return Press( Key.Escape );
        Check( "Esc resumes", gm.state == eGameState.playing && Time.timeScale == 1 );
        yield return Press( Key.P );
        Check( "P pauses too", gm.state == eGameState.paused );
        yield return Press( Key.R );
        Check( "R in the pause menu restarts the day", gm.state == eGameState.dayIntro && baker.Held == null );
        yield return Press( Key.Space );
        yield return Press( Key.Escape );
        yield return Press( Key.Q );
        Check( "Q in the pause menu goes to the main menu", gm.state == eGameState.title );

        // Results screen and shop keys: finish a day with enough money
        yield return Press( Key.Space );
        yield return Press( Key.Space );
        CustomerSpawner.S.BeginDay( gm.Day );       // clear the shop so the day can end right away
        gm.stats.earnings = 200;
        gm.dayTimer = gm.dayLengthSeconds;
        for ( t = 0; t < 5f && gm.state != eGameState.dayResults; t += Time.unscaledDeltaTime ) yield return null;
        Check( "the day ends once the bakery closes", gm.state == eGameState.dayResults );
        Shot( "results" );
        yield return Press( Key.Space );
        Check( "Space on the results screen opens the shop", gm.state == eGameState.shop );
        yield return Press( Key.Digit1 );
        Check( "1 in the shop buys the first upgrade (Third Oven)", gm.Has( eUpgrade.thirdOven ) );
        yield return Press( Key.Digit2 );
        Check( "2 buys the second upgrade (Turbo Ovens)", gm.Has( eUpgrade.turboOven ) );
        yield return Click( gm.menus.upgradeCards[3].buyButton );
        Check( "clicking a Buy button with the mouse buys it (Quick Whisk)", gm.Has( eUpgrade.quickWhisk ) );
        Shot( "shop" );
        yield return Press( Key.Space );
        Check( "Space in the shop opens Day 2", gm.state == eGameState.dayIntro && gm.dayIndex == 1 );
        Check( "the bought Third Oven is unlocked on Day 2", !gm.thirdOven.locked );

        Debug.Log( "[KeyTest] DONE: " + passed + " passed, " + failed + " failed" );
        yield return new WaitForSecondsRealtime( 1f );
        Application.Quit();
    }

    // ------------------------------------------------------------------ virtual keyboard and mouse

    void ApplyKeys() {
        Key[] keys = new Key[down.Count];
        down.CopyTo( keys );
        InputSystem.QueueStateEvent( kb, new KeyboardState( keys ) );
    }

    // A quick tap. Waits first, because screens ignore keys for a moment after they open.
    IEnumerator Press( Key key, float waitBefore = 0.45f ) {
        yield return new WaitForSecondsRealtime( waitBefore );
        down.Add( key );
        ApplyKeys();
        yield return null;
        yield return null;
        down.Remove( key );
        ApplyKeys();
        yield return null;
        yield return null;
    }

    IEnumerator Hold( Key key, float seconds ) {
        yield return new WaitForSecondsRealtime( 0.3f );
        down.Add( key );
        ApplyKeys();
        yield return new WaitForSecondsRealtime( seconds );
        down.Remove( key );
        ApplyKeys();
        yield return null;
    }

    IEnumerator Move( Key key, Vector2 direction, string label ) {
        Vector2 before = baker.transform.position;
        yield return Hold( key, 0.35f );
        Vector2 moved = (Vector2) baker.transform.position - before;
        Check( label, Vector2.Dot( moved, direction ) > 0.4f );
    }

    IEnumerator Click( Button button ) {
        yield return new WaitForSecondsRealtime( 0.45f );
        Vector2 pos = RectTransformUtility.WorldToScreenPoint( null, button.transform.position );
        InputSystem.QueueStateEvent( mouse, new MouseState { position = pos } );
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent( mouse, new MouseState { position = pos }.WithButton( MouseButton.Left ) );
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent( mouse, new MouseState { position = pos } );
        yield return null;
        yield return null;
    }

    // Walks to the cell next to the target with the arrow-style keys, then taps toward it to face it
    IEnumerator WalkTo( Interactable target ) {
        List<Vector2Int> path = KitchenGrid.FindPath( KitchenGrid.Cell( baker.transform.position ), target );
        foreach ( Vector2Int cell in path ) {
            Vector2 goal = KitchenGrid.CellCenter( cell );
            for ( float t = 0; t < 2f; t += Time.deltaTime ) {
                Vector2 d = goal - (Vector2) baker.transform.position;
                if ( Mathf.Abs( d.x ) < 0.1f && Mathf.Abs( d.y ) < 0.1f ) break;
                down.Clear();
                if ( d.x > 0.08f ) down.Add( Key.D );
                if ( d.x < -0.08f ) down.Add( Key.A );
                if ( d.y > 0.08f ) down.Add( Key.W );
                if ( d.y < -0.08f ) down.Add( Key.S );
                ApplyKeys();
                yield return null;
            }
        }
        down.Clear();
        ApplyKeys();
        Vector2Int here = KitchenGrid.Cell( baker.transform.position );
        Vector2Int there = KitchenGrid.Cell( target.Center );
        Key face = ( there.y > here.y ) ? Key.W : ( there.y < here.y ) ? Key.S : ( there.x > here.x ) ? Key.D : Key.A;
        down.Add( face );
        ApplyKeys();
        yield return new WaitForSecondsRealtime( 0.1f );
        down.Clear();
        ApplyKeys();
        yield return new WaitForSecondsRealtime( 0.1f );
    }

    // ------------------------------------------------------------------ helpers

    void Check( string what, bool ok ) {
        if ( ok ) passed++;
        else failed++;
        Debug.Log( "[KeyTest] " + ( ok ? "PASS  " : "FAIL  " ) + what );
    }

    bool Holding( eItem type ) {
        return baker.Held != null && baker.Held.type == type;
    }

    static T Find<T>( System.Predicate<T> match ) where T : Interactable {
        foreach ( Interactable it in Interactable.ALL ) {
            T t = it as T;
            if ( t != null && match( t ) ) return t;
        }
        return null;
    }

    void Shot( string label ) {
        if ( string.IsNullOrEmpty( shotFolder ) ) return;
        shotCount++;
        ScreenCapture.CaptureScreenshot( Path.Combine( shotFolder, "key_" + shotCount.ToString( "00" ) + "_" + label + ".png" ) );
    }
}
