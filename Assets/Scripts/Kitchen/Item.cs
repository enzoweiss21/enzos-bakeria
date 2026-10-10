using UnityEngine;

// Something the baker can carry: an ingredient, a dough, or a baked good
public class Item : MonoBehaviour {
    static private readonly Color BURNT_TINT = new Color( 0.28f, 0.2f, 0.18f );

    [Header( "Inscribed" )]
    public SpriteRenderer   sRend;
    public SpriteRenderer   sparkle;        // twinkles on perfect bakes

    [Header( "Dynamic" )]
    public eItem            type;
    public bool             perfect;        // pulled out of the oven right on time
    public float            bakeProgress;   // seconds spent in an oven so far (doughs keep this if you take them out early)
    public eItem            burntFrom;      // what it was before it burnt, so it still looks like a (black) loaf

    public ItemDef  Def         { get { return ItemBook.GET( type ); } }
    public bool     IsIngredient { get { return Def != null && Def.kind == eItemKind.ingredient; } }
    public bool     IsDough     { get { return Def != null && Def.kind == eItemKind.dough; } }
    public bool     IsSellable  { get { return Def != null && Def.price > 0; } }

    public void SetType( eItem newType ) {
        type = newType;
        Refresh();
    }

    public void Refresh() {
        ItemDef def = ( type == eItem.burnt ) ? ItemBook.GET( burntFrom ) : Def;
        if ( def != null ) sRend.sprite = def.sprite;
        sRend.color = ( type == eItem.burnt ) ? BURNT_TINT : Color.white;
        if ( sparkle != null ) sparkle.enabled = perfect && type != eItem.burnt;
    }

    // Parents this item to a hand or a station so it moves with it
    public void AttachTo( Transform anchor, int sortingOrder = 5 ) {
        transform.SetParent( anchor, false );
        transform.localPosition = Vector3.zero;
        sRend.sortingOrder = sortingOrder;
        if ( sparkle != null ) sparkle.sortingOrder = sortingOrder + 1;
        SetVisible( true );
    }

    public void SetVisible( bool visible ) {
        sRend.enabled = visible;
        if ( sparkle != null ) sparkle.enabled = visible && perfect && type != eItem.burnt;
    }

    void Update() {
        // A slow twinkle so perfect bakes are easy to spot on a counter
        if ( sparkle != null && sparkle.enabled ) {
            float s = 0.7f + 0.3f * Mathf.Sin( Time.time * 6f );
            sparkle.transform.localScale = new Vector3( s, s, 1 );
            sparkle.transform.localRotation = Quaternion.Euler( 0, 0, Time.time * 40f );
        }
    }
}
