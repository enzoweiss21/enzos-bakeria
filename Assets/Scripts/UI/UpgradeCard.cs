using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One upgrade in the shop between days
public class UpgradeCard : MonoBehaviour {
    [Header( "Inscribed" )]
    public eUpgrade         upgrade;
    public Image            icon;
    public TextMeshProUGUI  nameText;
    public TextMeshProUGUI  descriptionText;
    public TextMeshProUGUI  keyText;        // "1", "2"... the keyboard shortcut
    public Button           buyButton;
    public TextMeshProUGUI  buyLabel;
    public Image            background;

    public void Refresh( GameManager gm, int index ) {
        UpgradeDef def = gm.GetUpgrade( upgrade );
        if ( def == null ) return;
        icon.sprite = def.icon;
        icon.enabled = def.icon != null;
        nameText.text = def.displayName;
        descriptionText.text = def.description;
        keyText.text = ( index + 1 ).ToString();

        bool owned = gm.Has( upgrade );
        bool affordable = gm.savings >= def.price;
        buyButton.interactable = !owned && affordable;
        buyLabel.text = owned ? "Owned" : "Buy $" + def.price;
        background.color = owned ? new Color( 0.75f, 0.95f, 0.7f ) : Color.white;
        icon.color = ( owned || affordable ) ? Color.white : new Color( 1, 1, 1, 0.5f );
    }
}
