using System.Collections.Generic;
using UnityEngine;

public enum eSfx {
    pickUp, putDown, error, sparkle, confirm,
    voiceBlip, customerArrive, takeOrder, angryLeave,
    grabFlour, grabSoft, bowlAdd, bowlDone, frosting, trash,
    ovenDoor, ovenDing, perfectDing, warningBeep, burnt,
    cashRegister, tipJar, coins,
    shopBell, doorClose, footstep,
    catMeow, catPurr,
    uiClick, uiHover, upgradeBuy,
    jingleDayStart, jingleWin, jingleLose, jingleRush, jingleVictory
}

// Sounds that keep playing while something is happening, and fade in/out
public enum eLoop { whisk, ovenFire, cafe }

// One named sound. Several clips can be listed for variety; one is picked at random.
[System.Serializable]
public class SfxEntry {
    public eSfx         id;
    public AudioClip[]  clips;
    [Range( 0, 1 )]
    public float        volume = 1;
    [Tooltip( "Random pitch range so repeated sounds don't feel robotic" )]
    public float        pitchJitter = 0.05f;
}

[System.Serializable]
public class LoopEntry {
    public eLoop        id;
    public AudioClip    clip;
    [Range( 0, 1 )]
    public float        maxVolume = 0.5f;
    [HideInInspector]
    public AudioSource  source;
    [HideInInspector]
    public float        target;
}

// The single place every sound effect goes through. Call SFX.PLAY( eSfx.pickUp ) from anywhere.
public class SFX : MonoBehaviour {
    static private SFX S;

    [Header( "Inscribed" )]
    public List<SfxEntry>   sounds = new List<SfxEntry>();
    public List<LoopEntry>  loops = new List<LoopEntry>();
    [Range( 0, 1 )]
    public float            masterVolume = 0.8f;
    public int              voices = 12;

    private Dictionary<eSfx, SfxEntry>  lookup = new Dictionary<eSfx, SfxEntry>();
    private AudioSource[]               pool;
    private int                         nextVoice;

    void Awake() {
        S = this;
        foreach ( SfxEntry e in sounds ) lookup[e.id] = e;

        // A small pool of AudioSources so overlapping sounds can each have their own pitch
        pool = new AudioSource[voices];
        for ( int i = 0; i < voices; i++ ) {
            pool[i] = gameObject.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
        }
        foreach ( LoopEntry loop in loops ) {
            loop.source = gameObject.AddComponent<AudioSource>();
            loop.source.clip = loop.clip;
            loop.source.loop = true;
            loop.source.volume = 0;
            loop.source.playOnAwake = false;
        }
    }

    // LateUpdate runs after every Update, so all of this frame's HOLD_LOOP requests are in.
    // Loops ease toward their target volume, then the target resets to 0:
    //  anything that wants a loop to keep playing has to ask for it again every frame.
    void LateUpdate() {
        foreach ( LoopEntry loop in loops ) {
            float goal = loop.target * loop.maxVolume * masterVolume;
            loop.source.volume = Mathf.MoveTowards( loop.source.volume, goal, Time.unscaledDeltaTime * 1.5f );
            if ( loop.source.volume > 0.001f && !loop.source.isPlaying ) {
                loop.source.time = Random.Range( 0, loop.clip.length * 0.9f );
                loop.source.Play();
            } else if ( loop.source.volume <= 0.001f && loop.source.isPlaying ) {
                loop.source.Stop();
            }
            loop.target = 0;
        }
    }

    static public void PLAY( eSfx id, float volume = 1, float pitch = 1 ) {
        if ( S == null || !S.lookup.ContainsKey( id ) ) return;
        SfxEntry e = S.lookup[id];
        if ( e.clips == null || e.clips.Length == 0 ) return;

        AudioSource src = S.pool[S.nextVoice];
        S.nextVoice = ( S.nextVoice + 1 ) % S.pool.Length;
        src.clip = e.clips[Random.Range( 0, e.clips.Length )];
        src.volume = e.volume * volume * S.masterVolume;
        src.pitch = pitch * ( 1 + Random.Range( -e.pitchJitter, e.pitchJitter ) );
        src.Play();
    }

    // Ask for a loop to play this frame. 'amount' (0-1) scales its volume.
    static public void HOLD_LOOP( eLoop id, float amount = 1 ) {
        if ( S == null ) return;
        foreach ( LoopEntry loop in S.loops ) {
            if ( loop.id == id ) loop.target = Mathf.Max( loop.target, Mathf.Clamp01( amount ) );
        }
    }

    // A little "Animal Crossing" style babble: a few quick blips at the customer's own voice pitch
    static public void BABBLE( float voicePitch, int syllables, float mood = 0 ) {
        if ( S == null ) return;
        S.StartCoroutine( S.BabbleRoutine( voicePitch, syllables, mood ) );
    }

    private System.Collections.IEnumerator BabbleRoutine( float voicePitch, int syllables, float mood ) {
        for ( int i = 0; i < syllables; i++ ) {
            // Happy voices rise at the end, grumpy ones fall
            float contour = 1 + mood * 0.08f * i;
            PLAY( eSfx.voiceBlip, 0.6f, voicePitch * contour * Random.Range( 0.92f, 1.08f ) );
            yield return new WaitForSecondsRealtime( Random.Range( 0.06f, 0.09f ) );
        }
    }
}
