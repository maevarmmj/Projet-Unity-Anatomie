using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BodyPartClickable : MonoBehaviour
{
    [Tooltip("ID utilisé dans le JSON. Si vide, on prend gameObject.name.")]
    public string descriptionIdOverride;

    public string DescriptionId
    {
        get
        {
            return string.IsNullOrEmpty(descriptionIdOverride)
                ? gameObject.name
                : descriptionIdOverride;
        }
    }
}
