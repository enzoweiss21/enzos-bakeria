using System.Collections.Generic;
using UnityEngine;

// Sends customers through the door over the course of the day.
// The day settings decide how often they come, what's on the menu, and who shows up.
public class CustomerSpawner : MonoBehaviour {
    static public CustomerSpawner S { get; private set; }

    [Header( "Inscribed" )]
    public GameObject       customerPrefab;
    public ServiceSpot[]    spots;
    public Transform        door;           // where customers appear
    [Tooltip( "y of the walkway between the door and the counter" )]
    public float            walkwayY = 2.1f;
    public float            regularPatience = 50f;
    public float            hurriedPatience = 32f;
    public float            grandmaPatience = 70f;
    public float            criticPatience = 45f;

    [Header( "Dynamic" )]
    public List<Customer>   customers = new List<Customer>();
    public float            nextSpawnTime;
    public bool             criticHasCome;
    public int              spawnedToday;

    private DaySettings     day;

    void Awake() {
        S = this;
    }

    // Customers still in the bakery, including ones walking in or out
    public int InShop {
        get {
            customers.RemoveAll( c => c == null );
            return customers.Count;
        }
    }

    // Customers standing at the counter waiting for something
    public int Waiting {
        get {
            int n = 0;
            foreach ( Customer c in customers ) if ( c != null && c.IsAtCounter ) n++;
            return n;
        }
    }

    // The patience of whoever is closest to storming out (1 if nobody is waiting)
    public float LowestPatience {
        get {
            float low = 1;
            foreach ( Customer c in customers ) if ( c != null && c.IsAtCounter ) low = Mathf.Min( low, c.PatienceFrac );
            return low;
        }
    }

    public void BeginDay( DaySettings settings ) {
        day = settings;
        foreach ( Customer c in customers ) if ( c != null ) c.Vanish();
        customers.Clear();
        foreach ( ServiceSpot s in spots ) s.occupant = null;
        criticHasCome = false;
        spawnedToday = 0;
        nextSpawnTime = 2.5f;   // the first customer arrives a moment after opening
    }

    void Update() {
        GameManager gm = GameManager.S;
        if ( gm == null || !gm.IsPlaying || !gm.DoorsOpen ) return;
        if ( gm.dayTimer < nextSpawnTime ) return;

        ServiceSpot spot = PickFreeSpot();
        if ( spot == null ) {
            // Every spot is taken; check again shortly
            nextSpawnTime = gm.dayTimer + 1f;
            return;
        }

        Spawn( spot, ChooseType( gm.Hour ) );
        // Busier at lunch, quieter first thing in the morning
        float interval = day.spawnInterval * BusyCurve( gm.Hour ) * Random.Range( 0.8f, 1.2f );
        nextSpawnTime = gm.dayTimer + interval;
    }

    // Multiplier on the time between customers at each hour of the day (lower = busier)
    static public float BusyCurve( float hour ) {
        if ( hour < 9.5f ) return 1.25f;                    // slow morning
        if ( hour >= 11.5f && hour < 13.25f ) return 0.55f; // lunch rush
        if ( hour >= 15f ) return 0.9f;                     // after-school snack crowd
        return 1f;
    }

    ServiceSpot PickFreeSpot() {
        List<ServiceSpot> free = new List<ServiceSpot>();
        foreach ( ServiceSpot s in spots ) if ( s.occupant == null ) free.Add( s );
        if ( free.Count == 0 ) return null;
        return free[Random.Range( 0, free.Count )];
    }

    eCustomerType ChooseType( float hour ) {
        // The critic always shows up during the lunch rush, when it's hardest to be perfect
        if ( day.hasCritic && !criticHasCome && hour >= 12f ) {
            criticHasCome = true;
            return eCustomerType.critic;
        }
        float r = Random.value;
        if ( r < day.hurriedChance ) return eCustomerType.hurried;
        if ( r < day.hurriedChance + day.grandmaChance ) return eCustomerType.grandma;
        return eCustomerType.regular;
    }

    List<eItem> ChooseOrder( eCustomerType type ) {
        List<eItem> order = new List<eItem>();
        int count = ( Random.value < day.doubleOrderChance && type != eCustomerType.hurried ) ? 2 : 1;
        if ( type == eCustomerType.critic ) count = 2;
        for ( int i = 0; i < count; i++ ) order.Add( day.menu[Random.Range( 0, day.menu.Length )] );
        return order;
    }

    float PatienceFor( eCustomerType type, int orderSize ) {
        float p;
        switch ( type ) {
        case eCustomerType.hurried: p = hurriedPatience; break;
        case eCustomerType.grandma: p = grandmaPatience; break;
        case eCustomerType.critic:  p = criticPatience; break;
        default:                    p = regularPatience; break;
        }
        // Two-item orders get extra time, and the Comfy Chairs upgrade keeps everyone calmer
        if ( orderSize > 1 ) p *= 1.5f;
        return p * GameManager.S.PatienceMultiplier;
    }

    void Spawn( ServiceSpot spot, eCustomerType type ) {
        // Walk: out of the door, along the walkway, then down to the counter
        Vector3 start = door.position;
        List<Vector3> path = new List<Vector3> {
            start,
            new Vector3( start.x, walkwayY, 0 ),
            new Vector3( spot.standPoint.position.x, walkwayY, 0 ),
            spot.standPoint.position
        };
        GameObject go = Instantiate( customerPrefab, start, Quaternion.identity, transform );
        Customer c = go.GetComponent<Customer>();
        List<eItem> order = ChooseOrder( type );
        c.Setup( type, order, spot, path, PatienceFor( type, order.Count ) );
        customers.Add( c );
        spawnedToday++;
        SFX.PLAY( eSfx.shopBell, 0.8f );
    }
}
