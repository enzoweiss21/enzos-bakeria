using UnityEngine;

// Keeps the whole bakery on screen whatever the window's shape.
// At 16:9 the camera shows exactly 20 x 11.25 tiles (6 screen pixels per art pixel at 1080p);
// on narrower screens it zooms out just enough that the full 20-tile width still fits.
[RequireComponent( typeof( Camera ) )]
public class CameraFit : MonoBehaviour {
    [Header( "Inscribed" )]
    public float    minHalfHeight = 5.625f;
    public float    halfWidth = 10f;

    private Camera  cam;

    void Awake() {
        cam = GetComponent<Camera>();
        // Sort sprites by their y position (lower on screen = in front)
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = Vector3.up;
    }

    void LateUpdate() {
        float aspect = (float) Screen.width / Mathf.Max( 1, Screen.height );
        cam.orthographicSize = Mathf.Max( minHalfHeight, halfWidth / aspect );
    }
}
