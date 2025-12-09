using UnityEngine;
using UnityEngine.InputSystem;

public class DescriptionModeController : MonoBehaviour
{
    public static DescriptionModeController Instance { get; private set; }

    [Header("Réglages")]
    public bool descriptionModeActive = false;
    public Camera mainCamera;
    public Canvas canvas;
    public GameObject infoBoxPrefab;   // le prefab Infobox de ton ami
    public float autoCloseDelay = 4f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        if (!descriptionModeActive) return;

        // On récupère une position d'écran selon le device actif (souris ou tactile)
        Vector2 screenPos;
        bool hasClick = false;

        // Tactile (Android, tablette)
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                screenPos = touch.position.ReadValue();
                hasClick = true;
            }
            else
            {
                return; // pas de tap ce frame
            }
        }
        // Souris (PC, éditeur)
        else if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPos = Mouse.current.position.ReadValue();
                hasClick = true;
            }
            else
            {
                return; // pas de clic ce frame
            }
        }
        else
        {
            return; // aucun device d'input dispo
        }

        if (!hasClick) return;

        // Raycast 3D depuis la position écran
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var clickable = hit.collider.GetComponent<BodyPartClickable>();
            if (clickable != null)
            {
                ShowBodyPartInfo(clickable.DescriptionId, screenPos);
            }
        }
    }




    public void ToggleDescriptionMode()
    {
        descriptionModeActive = !descriptionModeActive;
        Debug.Log("Mode description : " + descriptionModeActive);
    }

    void ShowBodyPartInfo(string id, Vector3 screenPos)
    {
        if (BodyPartDescriptionDatabase.Instance == null) return;

        if (!BodyPartDescriptionDatabase.Instance.TryGet(id, out var info))
        {
            Debug.LogWarning("Pas de description trouvée pour : " + id);
            return;
        }

        GameObject instance = Instantiate(infoBoxPrefab, canvas.transform);

        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransform boxRect = instance.transform as RectTransform;

        if (canvasRect != null && boxRect != null)
        {
            // 1) Position "théorique" sous le doigt
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                canvas.worldCamera,
                out Vector2 localPos
            );

            // 2) Calcul des bornes pour que la box reste DANS le canvas
            Vector2 canvasSize = canvasRect.rect.size;
            Vector2 boxSize    = boxRect.rect.size;

            Vector2 halfCanvas = canvasSize * 0.5f;
            Vector2 halfBox    = boxSize * 0.5f;

            // Optionnel : petite marge pour éviter que ça touche le bord
            float margin = 10f;

            float minX = -halfCanvas.x + halfBox.x + margin;
            float maxX =  halfCanvas.x - halfBox.x - margin;
            float minY = -halfCanvas.y + halfBox.y + margin;
            float maxY =  halfCanvas.y - halfBox.y - margin;

            Vector2 clampedPos = new Vector2(
                Mathf.Clamp(localPos.x, minX, maxX),
                Mathf.Clamp(localPos.y, minY, maxY)
            );

            boxRect.anchoredPosition = clampedPos;
        }

        var ui = instance.GetComponent<InfoBoxUI>();
        if (ui != null)
        {
            ui.SetContent(info.nom, info.description, info.fun_fact);
        }

        if (autoCloseDelay > 0f)
            Destroy(instance, autoCloseDelay);
    }



}
