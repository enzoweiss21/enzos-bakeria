using System.Collections.Generic;
using UnityEngine;

// Anything the baker can face and press Space or E on: stations, customers' counter spots, the cat.
// The Baker picks the Interactable closest to the spot just in front of them.
public abstract class Interactable : MonoBehaviour {
    static public readonly List<Interactable> ALL = new List<Interactable>();

    [Header( "Inscribed: Interactable" )]
    [Tooltip( "Center of the clickable area, relative to the transform (pivots are at the bottom of the tile)" )]
    public Vector2      centerOffset = new Vector2( 0, 0.5f );
    [Tooltip( "Width and height of the area the baker can target" )]
    public Vector2      size = Vector2.one;

    public Vector2 Center { get { return (Vector2) transform.position + centerOffset; } }

    protected virtual void OnEnable()  { ALL.Add( this ); }
    protected virtual void OnDisable() { ALL.Remove( this ); }

    // How far the point p is from this object's area (0 when p is inside it)
    public float DistanceTo( Vector2 p ) {
        Vector2 d = p - Center;
        float dx = Mathf.Max( 0, Mathf.Abs( d.x ) - size.x * 0.5f );
        float dy = Mathf.Max( 0, Mathf.Abs( d.y ) - size.y * 0.5f );
        return Mathf.Sqrt( dx * dx + dy * dy );
    }

    // Can the baker target this right now? (the third oven is locked until it's bought, etc.)
    public virtual bool Targetable { get { return true; } }

    // Space: pick up / put down
    public virtual void Grab( Baker baker ) { }

    // E pressed this frame: take an order, pet the cat...
    public virtual void Use( Baker baker ) { }

    // E held down (called every frame while held): mixing and frosting
    public virtual void UseHeld( Baker baker ) { }

    // The key hint shown at the bottom of the screen, e.g. "[Space] Take flour"
    public virtual string Prompt( Baker baker ) { return ""; }
}
