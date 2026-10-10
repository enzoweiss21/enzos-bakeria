using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Makes menu buttons feel clickable: they grow a little and tick when the mouse moves over them
[RequireComponent( typeof( Button ) )]
public class ButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    private Button  button;
    private float   target = 1;

    void Awake() {
        button = GetComponent<Button>();
    }

    void OnDisable() {
        target = 1;
        transform.localScale = Vector3.one;
    }

    public void OnPointerEnter( PointerEventData eventData ) {
        if ( !button.interactable ) return;
        target = 1.06f;
        SFX.PLAY( eSfx.uiHover, 0.35f );
    }

    public void OnPointerExit( PointerEventData eventData ) {
        target = 1;
    }

    void Update() {
        float s = Mathf.MoveTowards( transform.localScale.x, target, Time.unscaledDeltaTime * 1.5f );
        transform.localScale = new Vector3( s, s, 1 );
    }
}
