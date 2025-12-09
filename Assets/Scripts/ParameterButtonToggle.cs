using UnityEngine;
using UnityEngine.UI;

public class ParameterButtonToggle : MonoBehaviour
{
    [Header("Références")]
    public GameObject optionsPanel;  // ton Panel
    public Image iconImage;          // l'image du bouton (l'engrenage)

    [Header("Sprites")]
    public Sprite gearSprite;        // icône engrenage
    public Sprite crossSprite;       // icône croix (fermer)

    private bool isOpen = false;

    void Start()
    {
        // Panel fermé au début
        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        // Icône engrenage par défaut
        if (iconImage != null && gearSprite != null)
            iconImage.sprite = gearSprite;
    }

    public void TogglePanel()
    {
        isOpen = !isOpen;

        if (optionsPanel != null)
            optionsPanel.SetActive(isOpen);

        if (iconImage != null)
            iconImage.sprite = isOpen ? crossSprite : gearSprite;
    }
}
