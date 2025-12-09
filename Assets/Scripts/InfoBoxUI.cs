using UnityEngine;
using TMPro;  // si tu utilises TextMeshPro

public class InfoBoxUI : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI funFactText;

    public void SetContent(string nom, string description, string funFact)
    {
        Debug.Log($"SetContent: nom={nom}, desc={description}, fun={funFact}");

        if (titleText != null)       titleText.text = nom;
        if (descriptionText != null) descriptionText.text = description;
        if (funFactText != null && !string.IsNullOrEmpty(funFact))
            funFactText.text = "Fun fact : " + funFact;
    }
}
