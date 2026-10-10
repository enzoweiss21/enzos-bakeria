using UnityEngine;

// Adaptive background music, synthesized live and driven by what's happening in the bakery.
// It's a little jazzy loop in F major (Fmaj7 - Dm7 - Gm7 - C7) built from layers:
//   bass + soft kick ... always (the quiet morning)
//   electric piano ...... as soon as a customer walks in
//   shaker .............. when 2+ customers are waiting, or during the lunch rush
//   marimba melody ...... when 3+ are waiting, or during the lunch rush
//   ticking clock ....... when somebody is about to storm out or something is about to burn
// The tempo also speeds up with every waiting customer, so the music literally rushes when you should.
// Menus get a calm, slower version, and pausing turns it down.
//
// The audio is generated in OnAudioFilterRead, which runs on Unity's audio thread.
// The main thread only writes the "target" numbers below; the audio thread eases toward them.
[RequireComponent( typeof( AudioSource ) )]
public class MusicDirector : MonoBehaviour {
    [Header( "Inscribed" )]
    [Range( 0, 1 )]
    public float    volume = 0.5f;
    public float    menuBpm = 84f;
    public float    workBpm = 92f;
    [Range( 0, 0.3f )]
    public float    swing = 0.12f;      // longer on-beats, shorter off-beats: a lazy, bouncy feel

    [Header( "Dynamic (read only)" )]
    public float    keysLayer;
    public float    shakerLayer;
    public float    melodyLayer;
    public float    tensionLayer;
    public float    bpm;

    // Written on the main thread, read on the audio thread
    private volatile float  tgtMaster, tgtKeys, tgtShaker, tgtMelody, tgtTension, tgtBpm;

    // ------------------------------------------------------------------ the song

    // MIDI note numbers. 4 bars of chords, 8 bars of melody, 8 eighth-note steps per bar.
    static private readonly int[]   ROOTS = { 41, 38, 43, 36 };                      // F, D, G, C
    static private readonly int[][] CHORDS = {
        new[] { 57, 60, 64, 65 },   // Fmaj7
        new[] { 53, 57, 60, 62 },   // Dm7
        new[] { 55, 58, 62, 65 },   // Gm7
        new[] { 52, 55, 58, 60 },   // C7
    };
    static private readonly int[][] MELODY = {   // -1 = rest
        new[] { 72, -1, 69, 72, 76, -1, 74, -1 },
        new[] { 72, -1, 69, -1, 65, -1, -1, -1 },
        new[] { 70, -1, 67, 70, 74, -1, 72, -1 },
        new[] { 70, -1, 67, -1, 64, -1, 67, -1 },
        new[] { 69, 72, 77, -1, 76, -1, 72, -1 },
        new[] { 74, -1, 72, 69, 65, -1, 69, -1 },
        new[] { 70, 74, 77, -1, 74, -1, 70, -1 },
        new[] { 72, -1, 70, -1, 67, -1, 64, -1 },
    };

    // ------------------------------------------------------------------ synth state (audio thread only)

    enum eVoice { bass, keys, marimba, shaker, kick, tick }

    struct Voice {
        public bool     active;
        public eVoice   kind;
        public double   phase;
        public double   freq;
        public float    amp;
        public float    env;
        public float    decayMul;   // multiplied into env every sample
        public int      age;        // samples since note on
    }

    private Voice[]     voices = new Voice[32];
    private double      sampleRate = 48000;
    private double      stepSamplesLeft;
    private int         step;           // 0..63 (8 bars x 8 steps)
    private float       gMaster, gKeys, gShaker, gMelody, gTension, curBpm = 84;
    private uint        noiseSeed = 22222;
    private float       hpPrev, hpOut;  // tiny high-pass filter for the shaker
    private AudioSource src;

    void Awake() {
        sampleRate = AudioSettings.outputSampleRate;
        // OnAudioFilterRead only runs while the AudioSource is playing, so give it a silent looping clip
        src = GetComponent<AudioSource>();
        src.clip = AudioClip.Create( "Silence", 1024, 1, (int) sampleRate, false );
        src.loop = true;
        src.spatialBlend = 0;
        src.volume = volume;
        src.Play();
        tgtBpm = menuBpm;
        tgtMaster = 0.7f;
        tgtKeys = 1;
    }

    // ------------------------------------------------------------------ main thread: listen to the game

    void Update() {
        GameManager gm = GameManager.S;
        if ( gm == null ) return;

        float master = 0.7f, keys = 1, shaker = 0, melody = 0, tension = 0, tempo = menuBpm;
        switch ( gm.state ) {
        case eGameState.playing:
        case eGameState.paused:
            CustomerSpawner sp = CustomerSpawner.S;
            int waiting = sp != null ? sp.Waiting : 0;
            int inShop = sp != null ? sp.InShop : 0;
            master = ( gm.state == eGameState.paused ) ? 0.3f : 1f;
            keys = ( inShop > 0 ) ? 1 : 0;
            shaker = ( waiting >= 2 || gm.IsRush ) ? 1 : 0;
            melody = ( waiting >= 3 || gm.IsRush ) ? 1 : 0;
            tension = ( ( sp != null && sp.LowestPatience < 0.25f ) || SomethingAboutToBurn() ) ? 1 : 0;
            tempo = Mathf.Min( 112, workBpm + waiting * 3f + ( gm.IsRush ? 5f : 0 ) );
            break;
        case eGameState.victory:
            melody = 1;
            shaker = 1;
            tempo = 100;
            break;
        }
        tgtMaster = master;
        tgtKeys = keys;
        tgtShaker = shaker;
        tgtMelody = melody;
        tgtTension = tension;
        tgtBpm = tempo;
        src.volume = volume;

        // Show the live values in the Inspector
        keysLayer = gKeys;
        shakerLayer = gShaker;
        melodyLayer = gMelody;
        tensionLayer = gTension;
        bpm = curBpm;
    }

    static bool SomethingAboutToBurn() {
        foreach ( Interactable it in Interactable.ALL ) {
            Oven o = it as Oven;
            if ( o != null && o.state == eOvenState.ready && o.burnDelay - o.doneTimer < 3f ) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ audio thread: make the sound

    void OnAudioFilterRead( float[] data, int channels ) {
        // Ease the layers toward their targets once per buffer (about every 20 ms)
        gMaster  += ( tgtMaster - gMaster ) * 0.05f;
        gKeys    += ( tgtKeys - gKeys ) * 0.04f;
        gShaker  += ( tgtShaker - gShaker ) * 0.04f;
        gMelody  += ( tgtMelody - gMelody ) * 0.04f;
        gTension += ( tgtTension - gTension ) * 0.08f;
        curBpm   += ( tgtBpm - curBpm ) * 0.02f;

        int frames = data.Length / channels;
        for ( int i = 0; i < frames; i++ ) {
            if ( stepSamplesLeft <= 0 ) {
                PlayStep( step );
                // Swing: even steps a bit longer, odd steps a bit shorter
                double stepLen = sampleRate * 60.0 / curBpm / 2.0;
                stepSamplesLeft += stepLen * ( ( step % 2 == 0 ) ? 1 + swing : 1 - swing );
                step = ( step + 1 ) % 64;
            }
            stepSamplesLeft--;

            float s = RenderVoices() * gMaster;
            // Soft clip so stacked notes can never crackle
            s = (float) System.Math.Tanh( s );
            for ( int c = 0; c < channels; c++ ) data[i * channels + c] += s;
        }
    }

    void PlayStep( int st ) {
        int bar = st / 8;
        int s = st % 8;
        int chord = bar % 4;
        int root = ROOTS[chord];
        int nextRoot = ROOTS[( chord + 1 ) % 4];

        // Soft kick on beats 1 and 3
        if ( s == 0 || s == 4 ) NoteOn( eVoice.kick, 0, 0.35f, 0.12f );

        // Walking-ish bass: root, fifth, octave, then a step toward the next chord
        if ( s == 0 ) NoteOn( eVoice.bass, root, 0.38f, 0.45f );
        if ( s == 3 ) NoteOn( eVoice.bass, root + 7, 0.28f, 0.3f );
        if ( s == 4 ) NoteOn( eVoice.bass, root + 12, 0.3f, 0.35f );
        if ( s == 6 ) NoteOn( eVoice.bass, nextRoot + 1, 0.25f, 0.25f );

        // Electric piano comping on the off-beats
        if ( gKeys > 0.02f && ( s == 2 || s == 5 ) ) {
            foreach ( int n in CHORDS[chord] ) NoteOn( eVoice.keys, n, 0.055f * gKeys, 0.35f );
        }

        // Shaker on every eighth note, accented off-beats
        if ( gShaker > 0.02f ) NoteOn( eVoice.shaker, 0, ( s % 2 == 1 ? 0.11f : 0.06f ) * gShaker, 0.035f );

        // The melody
        if ( gMelody > 0.02f ) {
            int note = MELODY[bar][s];
            if ( note > 0 ) NoteOn( eVoice.marimba, note, 0.16f * gMelody, 0.5f );
        }

        // Tick-tock when things are tense
        if ( gTension > 0.02f && s % 2 == 0 ) NoteOn( eVoice.tick, ( s % 4 == 0 ) ? 96 : 91, 0.09f * gTension, 0.03f );
    }

    void NoteOn( eVoice kind, int midi, float amp, float decaySeconds ) {
        for ( int i = 0; i < voices.Length; i++ ) {
            if ( voices[i].active ) continue;
            voices[i].active = true;
            voices[i].kind = kind;
            voices[i].freq = 440.0 * System.Math.Pow( 2, ( midi - 69 ) / 12.0 );
            voices[i].phase = 0;
            voices[i].amp = amp;
            voices[i].env = 1;
            voices[i].decayMul = (float) System.Math.Exp( -1.0 / ( decaySeconds * sampleRate ) );
            voices[i].age = 0;
            return;
        }
        // All voices busy: just skip the note
    }

    float RenderVoices() {
        float mix = 0;
        double twoPi = System.Math.PI * 2;
        for ( int i = 0; i < voices.Length; i++ ) {
            if ( !voices[i].active ) continue;
            ref Voice v = ref voices[i];
            double p = v.phase;
            float sample;
            switch ( v.kind ) {
            case eVoice.bass:
                // Triangle wave: warm and round
                double t = p - System.Math.Floor( p );
                sample = (float) ( 4 * System.Math.Abs( t - 0.5 ) - 1 );
                break;
            case eVoice.keys:
                // A sine with a couple of quiet overtones sounds like a mellow electric piano
                sample = (float) ( System.Math.Sin( twoPi * p ) + 0.22 * System.Math.Sin( twoPi * 2 * p ) + 0.06 * System.Math.Sin( twoPi * 3 * p ) );
                break;
            case eVoice.marimba:
                // Fundamental plus a bright 4th harmonic that dies away quickly: the "tok" of a mallet
                float strike = (float) System.Math.Exp( -v.age / ( 0.02 * sampleRate ) );
                sample = (float) ( System.Math.Sin( twoPi * p ) + 0.5 * strike * System.Math.Sin( twoPi * 4 * p ) );
                break;
            case eVoice.shaker:
                // White noise through a simple high-pass filter
                float noise = NextNoise();
                hpOut = 0.7f * ( hpOut + noise - hpPrev );
                hpPrev = noise;
                sample = hpOut;
                break;
            case eVoice.kick:
                // A sine that sweeps down from 120 Hz to 50 Hz
                double f = 50 + 70 * System.Math.Exp( -v.age / ( 0.03 * sampleRate ) );
                v.freq = f;
                sample = (float) System.Math.Sin( twoPi * p );
                break;
            default:
                // Woodblock tick
                sample = (float) System.Math.Sin( twoPi * p );
                break;
            }

            // 3 ms fade in so notes never click, then exponential decay
            float attack = System.Math.Min( 1f, v.age / (float) ( 0.003 * sampleRate ) );
            mix += sample * v.env * attack * v.amp;

            v.phase = p + v.freq / sampleRate;
            v.env *= v.decayMul;
            v.age++;
            if ( v.env < 0.0005f ) v.active = false;
        }
        return mix;
    }

    // Fast pseudo-random noise (xorshift), safe to use on the audio thread
    float NextNoise() {
        noiseSeed ^= noiseSeed << 13;
        noiseSeed ^= noiseSeed >> 17;
        noiseSeed ^= noiseSeed << 5;
        return ( noiseSeed / (float) uint.MaxValue ) * 2f - 1f;
    }
}
