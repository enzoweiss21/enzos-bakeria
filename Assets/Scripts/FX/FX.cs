using System.Collections;
using TMPro;
using UnityEngine;

// Little bits of "juice": floating text, puffs of flour or smoke, hearts, sparkles, and squash & stretch.
// Everything here is cosmetic; no gameplay depends on it.
public class FX : MonoBehaviour {
    static private FX S;

    [Header( "Inscribed" )]
    public GameObject   floatingTextPrefab;
    public Sprite       puffSprite;
    public Sprite       heartSprite;
    public Sprite       sparkleSprite;
    public Sprite       starSprite;
    public Sprite       zzzSprite;
    public string       sortingLayer = "WorldUI";

    void Awake() {
        S = this;
    }

    // ------------------------------------------------------------------ floating text

    static public void TEXT( string text, Vector3 pos, Color color, float size = 1f, float rise = 0.9f ) {
        if ( S == null || S.floatingTextPrefab == null ) return;
        GameObject go = Instantiate( S.floatingTextPrefab, pos, Quaternion.identity );
        TextMeshPro tmp = go.GetComponent<TextMeshPro>();
        tmp.text = text;
        tmp.color = color;
        go.transform.localScale = Vector3.one * size;
        S.StartCoroutine( S.FloatAway( go, tmp, rise, 1.3f ) );
    }

    private IEnumerator FloatAway( GameObject go, TextMeshPro tmp, float rise, float duration ) {
        Vector3 start = go.transform.position;
        Color c = tmp.color;
        Vector3 baseScale = go.transform.localScale;
        for ( float t = 0; t < duration; t += Time.unscaledDeltaTime ) {
            if ( go == null ) yield break;
            float u = t / duration;
            // Ease out as it rises, pop in quickly, fade during the last 40%
            go.transform.position = start + Vector3.up * rise * ( 1 - ( 1 - u ) * ( 1 - u ) );
            go.transform.localScale = baseScale * Mathf.Min( 1, 0.6f + u * 4f );
            c.a = ( u < 0.6f ) ? 1 : 1 - ( u - 0.6f ) / 0.4f;
            tmp.color = c;
            yield return null;
        }
        if ( go != null ) Destroy( go );
    }

    // ------------------------------------------------------------------ sprite bursts

    static public void PUFF( Vector3 pos, Color color, int count = 5 ) {
        BURST( S != null ? S.puffSprite : null, pos, color, count, 0.5f, 0.8f, 0.25f );
    }

    static public void HEARTS( Vector3 pos, int count = 3 ) {
        BURST( S != null ? S.heartSprite : null, pos, Color.white, count, 0.35f, 1.1f, 0.6f, true );
    }

    static public void SPARKLES( Vector3 pos, int count = 6 ) {
        BURST( S != null ? S.sparkleSprite : null, pos, Color.white, count, 0.6f, 0.7f, 0.1f );
    }

    static public void STARS( Vector3 pos, int count = 5 ) {
        BURST( S != null ? S.starSprite : null, pos, Color.white, count, 0.7f, 1.0f, 0.4f, true );
    }

    static public void ZZZ( Vector3 pos ) {
        BURST( S != null ? S.zzzSprite : null, pos, Color.white, 1, 0.1f, 1.6f, 0.5f, true );
    }

    // Spawns 'count' copies of a sprite that drift outward/upward and fade
    static void BURST( Sprite sprite, Vector3 pos, Color color, int count, float spread, float life, float riseSpeed, bool floatUp = false ) {
        if ( S == null || sprite == null ) return;
        for ( int i = 0; i < count; i++ ) {
            GameObject go = new GameObject( "FX_" + sprite.name );
            go.transform.position = pos;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingLayerName = S.sortingLayer;
            sr.sortingOrder = 20;
            Vector2 dir = floatUp ? new Vector2( Random.Range( -0.5f, 0.5f ), 1 ) : Random.insideUnitCircle.normalized;
            S.StartCoroutine( S.Drift( go, sr, dir * spread, riseSpeed, life * Random.Range( 0.8f, 1.2f ) ) );
        }
    }

    private IEnumerator Drift( GameObject go, SpriteRenderer sr, Vector2 velocity, float rise, float life ) {
        Color c = sr.color;
        float scale0 = Random.Range( 0.8f, 1.2f );
        for ( float t = 0; t < life; t += Time.unscaledDeltaTime ) {
            if ( go == null ) yield break;
            float u = t / life;
            go.transform.position += (Vector3) ( velocity * ( 1 - u ) + Vector2.up * rise ) * Time.unscaledDeltaTime;
            go.transform.localScale = Vector3.one * scale0 * ( 0.6f + 0.6f * Mathf.Sin( u * Mathf.PI * 0.5f ) );
            c.a = 1 - u * u;
            sr.color = c;
            yield return null;
        }
        if ( go != null ) Destroy( go );
    }

    // ------------------------------------------------------------------ squash & stretch

    // Makes a transform "pop" (grow then settle back), e.g. when an item lands on a counter
    static public void POP( Transform t, float amount = 0.18f ) {
        if ( S == null || t == null ) return;
        S.StartCoroutine( S.PopRoutine( t, amount ) );
    }

    private IEnumerator PopRoutine( Transform t, float amount ) {
        Vector3 baseScale = Vector3.one;
        const float duration = 0.25f;
        for ( float time = 0; time < duration; time += Time.unscaledDeltaTime ) {
            if ( t == null ) yield break;
            float u = time / duration;
            float squash = Mathf.Sin( u * Mathf.PI ) * amount * ( 1 - u );
            t.localScale = new Vector3( baseScale.x * ( 1 + squash ), baseScale.y * ( 1 - squash * 0.7f ), 1 );
            yield return null;
        }
        if ( t != null ) t.localScale = baseScale;
    }
}
