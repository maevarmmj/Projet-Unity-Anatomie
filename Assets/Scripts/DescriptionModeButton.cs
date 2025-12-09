using UnityEngine;
using UnityEngine.UI;

public class DescriptionModeButton : MonoBehaviour
{
    public Image iconImage;
    public Color offColor = Color.white;
    public Color onColor = Color.green;

    public void ToggleMode()
    {
        if (DescriptionModeController.Instance == null) return;

        DescriptionModeController.Instance.ToggleDescriptionMode();

        if (iconImage != null)
        {
            bool active = DescriptionModeController.Instance.descriptionModeActive;
            iconImage.color = active ? onColor : offColor;
        }
    }
}
