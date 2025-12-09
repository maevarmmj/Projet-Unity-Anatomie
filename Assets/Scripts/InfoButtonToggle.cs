using UnityEngine;
using UnityEngine.UI;

public class InfoButtonToggle : MonoBehaviour
{
    [Header("Références")]
    public GameObject infoPanel;  // L'instance de l'Infobox dans ton Canvas
    public Image iconImage;       // L'image du bouton info

    [Header("Sprites")]
    public Sprite infoSprite;     // Icône info (état fermé)
    public Sprite crossSprite;    // Icône croix (état ouvert)

    private bool isOpen = false;

    void Start()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (iconImage != null && infoSprite != null)
            iconImage.sprite = infoSprite;
    }

    public void ToggleInfo()
    {
        isOpen = !isOpen;

        if (infoPanel != null)
            infoPanel.SetActive(isOpen);

        if (iconImage != null)
            iconImage.sprite = isOpen ? crossSprite : infoSprite;
    }
}
