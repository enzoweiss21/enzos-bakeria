using UnityEngine;

// Makes the bakery feel alive as the day goes by:
//  - the sky behind the windows goes from early morning to bright noon to sunset
//  - the hum of chatter grows with the number of customers in the shop
public class BakeryAmbience : MonoBehaviour {
    [Header( "Inscribed" )]
    public SpriteRenderer[] skies;
    public Gradient         skyByDay;   // 0 = 8 AM, 1 = 4 PM
    [Range( 0, 1 )]
    public float            title01 = 0.15f;    // the time of day shown behind the title screen

    void Update() {
        GameManager gm = GameManager.S;
        if ( gm == null ) return;

        bool working = gm.state == eGameState.playing || gm.state == eGameState.paused;
        float t = working ? gm.Day01 : ( gm.state == eGameState.dayResults ? 1 : title01 );
        Color sky = skyByDay.Evaluate( t );
        foreach ( SpriteRenderer sr in skies ) sr.color = sky;

        if ( gm.state == eGameState.playing && CustomerSpawner.S != null ) {
            SFX.HOLD_LOOP( eLoop.cafe, CustomerSpawner.S.InShop / 4f );
        }
    }
}
