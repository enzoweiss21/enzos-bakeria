using UnityEngine;

// A small progress bar drawn in the world (patience, mixing, baking).
// Made of two stretched white sprites: a dark background and a colored fill that grows from the left.
public class WorldBar : MonoBehaviour {
    [Header( "Inscribed" )]
    public SpriteRenderer   background;
    public SpriteRenderer   fill;
    public float            width = 0.8f;
    public float            height = 0.12f;

    [Header( "Dynamic" )]
    [Range( 0, 1 )]
    public float            value;

    void Awake() {
        background.transform.localScale = new Vector3( width + 0.06f, height + 0.06f, 1 );
        Set( value, fill.color );
    }

    public void Set( float newValue, Color color ) {
        value = Mathf.Clamp01( newValue );
        fill.color = color;
        // The white sprite is 1 unit wide with a centered pivot, so scale and slide it to grow from the left
        fill.transform.localScale = new Vector3( width * value, height, 1 );
        fill.transform.localPosition = new Vector3( -width * 0.5f + width * value * 0.5f, 0, -0.01f );
    }

    public void Show( bool visible ) {
        if ( gameObject.activeSelf != visible ) gameObject.SetActive( visible );
    }

    // Green when there's plenty left, yellow in the middle, red when it's almost empty
    static public Color PATIENCE_COLOR( float frac ) {
        Color green  = new Color( 0.35f, 0.85f, 0.35f );
        Color yellow = new Color( 1f, 0.8f, 0.2f );
        Color red    = new Color( 0.95f, 0.25f, 0.2f );
        return ( frac > 0.5f ) ? Color.Lerp( yellow, green, ( frac - 0.5f ) * 2 ) : Color.Lerp( red, yellow, frac * 2 );
    }
}
