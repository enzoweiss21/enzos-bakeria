using UnityEngine;

// Builds a random-looking customer out of paper-doll layers (Kenney's Roguelike Characters):
// a body, pants, shoes, a shirt, hair, and sometimes a beard or a hat.
public class CustomerLook : MonoBehaviour {
    [Header( "Inscribed: the layers, back to front" )]
    public SpriteRenderer   body;
    public SpriteRenderer   pants;
    public SpriteRenderer   shoes;
    public SpriteRenderer   shirt;
    public SpriteRenderer   hair;
    public SpriteRenderer   beard;
    public SpriteRenderer   hat;

    [Header( "Inscribed: the sprites to choose from" )]
    public Sprite[]         bodies;
    public Sprite[]         pantsOptions;
    public Sprite[]         shoeOptions;
    public Sprite[]         shirts;
    public Sprite[]         hairs;
    public Sprite[]         beards;
    public Sprite[]         hats;
    [Tooltip( "Hair sprites from this index on are white (used for grandmas)" )]
    public int              whiteHairStart = 28;
    [Tooltip( "Shirt used for the food critic's dark suit" )]
    public int              criticShirt = 69;

    public SpriteRenderer[] AllLayers { get { return new SpriteRenderer[] { body, pants, shoes, shirt, hair, beard, hat }; } }

    public void Randomize( eCustomerType type ) {
        body.sprite = Pick( bodies );
        pants.sprite = Pick( pantsOptions );
        shoes.sprite = Pick( shoeOptions );
        shirt.sprite = Pick( shirts );
        hair.sprite = hairs[Random.Range( 0, whiteHairStart )];
        beard.sprite = ( Random.value < 0.2f ) ? Pick( beards ) : null;
        hat.sprite = ( Random.value < 0.12f ) ? Pick( hats ) : null;

        switch ( type ) {
        case eCustomerType.grandma:
            // Grandmas and grandpas always have white hair and never wear the silly hats
            hair.sprite = hairs[Random.Range( whiteHairStart, hairs.Length )];
            hat.sprite = null;
            break;
        case eCustomerType.critic:
            shirt.sprite = shirts[Mathf.Clamp( criticShirt, 0, shirts.Length - 1 )];
            hat.sprite = hats.Length > 0 ? hats[0] : null;
            beard.sprite = null;
            break;
        }
    }

    static Sprite Pick( Sprite[] options ) {
        if ( options == null || options.Length == 0 ) return null;
        return options[Random.Range( 0, options.Length )];
    }

    public void SetAlpha( float a ) {
        foreach ( SpriteRenderer sr in AllLayers ) {
            Color c = sr.color;
            c.a = a;
            sr.color = c;
        }
    }
}
