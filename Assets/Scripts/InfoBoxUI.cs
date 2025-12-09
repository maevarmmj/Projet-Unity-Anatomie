using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InfoBoxUI : MonoBehaviour
{
    [Header("Textes")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI funFactText;

    [Header("Fond avec le pic")]
    public Image backgroundImage;        // Image sur backgroundImage
    public Sprite bubbleRightSprite;     // pic en bas à droite
    public Sprite bubbleLeftSprite;      // pic en bas à gauche (même image, mais retournée dans un logiciel)

    public void SetContent(string nom, string description, string funFact)
    {
        if (titleText != null)       titleText.text = nom;
        if (descriptionText != null) descriptionText.text = description;
        if (funFactText != null && !string.IsNullOrEmpty(funFact))
            funFactText.text = "Fun fact : " + funFact;
    }

    /// <summary>
    /// clickOnRight = true si le clic est dans la moitié droite de l'écran.
    /// On choisit juste quel sprite utiliser (pic à gauche ou à droite).
    /// </summary>
    public void SetPointerSide(bool clickOnRight)
    {
        if (backgroundImage == null) return;

        // On met notre sprite de base
        if (bubbleRightSprite != null)
            backgroundImage.sprite = bubbleRightSprite;

        // Flip horizontal du fond UNIQUEMENT
        var rt = backgroundImage.rectTransform;
        Vector3 s = rt.localScale;
        s.x = clickOnRight ? -1f : 1f;
        rt.localScale = s;
    }

}
