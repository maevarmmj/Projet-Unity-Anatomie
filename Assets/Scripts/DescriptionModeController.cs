using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DescriptionModeController : MonoBehaviour
{
    public static DescriptionModeController Instance { get; private set; }

    [Header("Réglages")]
    public bool descriptionModeActive = false;
    public Camera mainCamera;
    public Canvas canvas;
    public GameObject infoBoxPrefab;   // le prefab Infobox
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
        if (mainCamera == null || canvas == null || infoBoxPrefab == null) return;

        Vector2 screenPos;

        // --- TACTILE ---
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (!touch.press.wasPressedThisFrame) return;
            screenPos = touch.position.ReadValue();
        }
        // --- SOURIS (PC / Éditeur) ---
        else if (Mouse.current != null)
        {
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            screenPos = Mouse.current.position.ReadValue();
        }
        else return;

        // Raycast dans la scène pour vérifier qu'on a bien touché un BodyPartClickable
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

    void ShowBodyPartInfo(string id, Vector2 screenPos)
    {
        if (BodyPartDescriptionDatabase.Instance == null) return;

        if (!BodyPartDescriptionDatabase.Instance.TryGet(id, out var info))
        {
            Debug.LogWarning("Pas de description trouvée pour : " + id);
            return;
        }

        GameObject instance = Instantiate(infoBoxPrefab, canvas.transform);

        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransform boxRect    = instance.GetComponent<RectTransform>();

        // ===== NOUVEAU : déterminer d’abord si on est à gauche ou droite du CANVAS =====
        bool clickOnRight = false;

        if (canvasRect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                canvas.worldCamera,
                out Vector2 localSidePos
            );

            clickOnRight = localSidePos.x >= 0f;
        }

        // ===== Positionnement de la bulle =====
        if (canvasRect != null && boxRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(boxRect);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                canvas.worldCamera,
                out Vector2 localPos
            );

            Vector2 canvasSize = canvasRect.rect.size;
            Vector2 boxSize    = boxRect.rect.size;
            Vector2 halfCanvas = canvasSize * 0.5f;
            Vector2 halfBox    = boxSize * 0.5f;
            float margin       = 10f;

            float minX = -halfCanvas.x + halfBox.x + margin;
            float maxX =  halfCanvas.x - halfBox.x - margin;

            float minY = -halfCanvas.y + halfBox.y + margin;
            float maxY =  halfCanvas.y - halfBox.y - margin;

            Vector2 clampedPos = new Vector2(
                Mathf.Clamp(localPos.x, minX, maxX),
                Mathf.Clamp(localPos.y, minY, maxY)
            );

            // ===== Offset gauche/droite =====
            float sideOffset = canvasRect.rect.width * 0.20f;

            if (clickOnRight)
                clampedPos.x += sideOffset;  // bulle décalée à droite
            else
                clampedPos.x -= sideOffset;  // bulle décalée à gauche

            boxRect.anchoredPosition = clampedPos;
        }

        // ===== Appliquer le contenu & le flip =====
        var ui = instance.GetComponent<InfoBoxUI>();
        if (ui != null)
        {
            ui.SetContent(info.nom, info.description, info.fun_fact);
            ui.SetPointerSide(clickOnRight);
        }

        if (autoCloseDelay > 0f)
            Destroy(instance, autoCloseDelay);
    }

}
