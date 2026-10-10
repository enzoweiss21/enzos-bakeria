using System.Collections.Generic;
using UnityEngine;

// The speech bubble over a customer's head: "!" until their order is taken,
// then one or two pictures of what they want (with a check mark once delivered), plus their patience bar.
public class OrderBubble : MonoBehaviour {
    [Header( "Inscribed" )]
    public SpriteRenderer   bubble;
    public SpriteRenderer   alertIcon;
    public SpriteRenderer[] itemIcons;      // two slots
    public SpriteRenderer[] checkMarks;     // shown over delivered items
    public WorldBar         patienceBar;
    public SpriteRenderer   badge;          // star for the critic, etc.

    private Vector3         baseScale;

    void Awake() {
        baseScale = transform.localScale;
        Hide();
    }

    public void Hide() {
        bubble.enabled = false;
        alertIcon.enabled = false;
        foreach ( SpriteRenderer sr in itemIcons ) sr.enabled = false;
        foreach ( SpriteRenderer sr in checkMarks ) sr.enabled = false;
        patienceBar.Show( false );
    }

    public void ShowAlert() {
        bubble.enabled = true;
        alertIcon.enabled = true;
        foreach ( SpriteRenderer sr in itemIcons ) sr.enabled = false;
        foreach ( SpriteRenderer sr in checkMarks ) sr.enabled = false;
        patienceBar.Show( true );
        FX.POP( transform, 0.3f );
    }

    public void ShowOrder( List<eItem> order, List<bool> delivered ) {
        bubble.enabled = true;
        alertIcon.enabled = false;
        int n = order.Count;
        for ( int i = 0; i < itemIcons.Length; i++ ) {
            bool used = i < n;
            itemIcons[i].enabled = used;
            checkMarks[i].enabled = used && delivered[i];
            if ( !used ) continue;
            itemIcons[i].sprite = ItemBook.GET( order[i] ).sprite;
            itemIcons[i].color = delivered[i] ? new Color( 1, 1, 1, 0.45f ) : Color.white;
            // One item sits in the middle, two items sit side by side
            float x = ( n == 1 ) ? 0 : ( i == 0 ? -0.27f : 0.27f );
            itemIcons[i].transform.localPosition = new Vector3( x, 0.05f, -0.01f );
        }
        // Wider bubble for two items
        bubble.transform.localScale = new Vector3( n == 1 ? 1f : 1.55f, 1f, 1f );
        patienceBar.Show( true );
    }

    public void SetPatience( float frac, bool panic ) {
        patienceBar.Set( frac, WorldBar.PATIENCE_COLOR( frac ) );
        // The bubble trembles when they're about to give up
        float shake = panic ? Mathf.Sin( Time.time * 40f ) * 0.025f : 0;
        bubble.transform.localPosition = new Vector3( shake, 0, 0 );
    }

    public void SetBadge( Sprite sprite ) {
        badge.enabled = sprite != null;
        badge.sprite = sprite;
    }
}
