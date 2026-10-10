using System.Collections.Generic;
using UnityEngine;

// Every kind of thing the baker can carry
public enum eItem {
    none,
    // Ingredients (from the crates)
    flour, chocolate, berries,
    // Mixed in a bowl, waiting for the oven
    breadDough, cookieDough, cupcakeBatter,
    // Out of the oven
    bread, cookies, cupcake, frostedCupcake,
    // Left in the oven for too long
    burnt
}

public enum eItemKind { ingredient, dough, baked, burnt }

// The design data for one item. Edit these in the Inspector on the ItemBook (in _Managers).
[System.Serializable]
public class ItemDef {
    public eItem        item;
    public string       displayName;
    public eItemKind    kind;
    public Sprite       sprite;
    [Tooltip( "What a customer pays for it. 0 means it can't be sold." )]
    public int          price;
    [Tooltip( "Doughs only: what comes out of the oven" )]
    public eItem        bakesInto;
    [Tooltip( "Doughs only: seconds in the oven until it's done" )]
    public float        bakeSeconds;
}

// Holds the ItemDef table and spawns Item GameObjects
public class ItemBook : MonoBehaviour {
    static public ItemBook S { get; private set; }

    [Header( "Inscribed" )]
    public GameObject       itemPrefab;
    public List<ItemDef>    items = new List<ItemDef>();

    private Dictionary<eItem, ItemDef> lookup;

    void Awake() {
        S = this;
        lookup = new Dictionary<eItem, ItemDef>();
        foreach ( ItemDef def in items ) lookup[def.item] = def;
    }

    static public ItemDef GET( eItem item ) {
        if ( S == null || !S.lookup.ContainsKey( item ) ) return null;
        return S.lookup[item];
    }

    static public string NAME( eItem item ) {
        ItemDef def = GET( item );
        return ( def != null ) ? def.displayName : item.ToString();
    }

    // Creates a new item in the world. The caller decides where it goes next.
    static public Item SPAWN( eItem type, Vector3 pos ) {
        GameObject go = Instantiate( S.itemPrefab, pos, Quaternion.identity );
        Item item = go.GetComponent<Item>();
        item.SetType( type );
        return item;
    }

    // The mixing bowl recipes. Every baked good needs flour plus (at most) one topping.
    static public eItem RECIPE( bool hasFlour, eItem topping ) {
        if ( !hasFlour ) return eItem.none;
        switch ( topping ) {
        case eItem.chocolate: return eItem.cookieDough;
        case eItem.berries:   return eItem.cupcakeBatter;
        default:              return eItem.breadDough;
        }
    }
}
