using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;

namespace UI.Script
{
    public class ClickManager : MonoBehaviour
    {
        [Header("Réglages")] public GameObject infoBoxPrefab;
        public Transform canvasTransform;
        
        public GraphicRaycaster uiRaycaster; 
        public EventSystem eventSystem;
        
        [Header("Filtres")]
        public string blockingTag = "UI_Interactable";

        [Header("Options")] public bool destroyAfterTime = true;
        public float lifeTime = 2.0f;

        void Update()
        {
            // Vérifie si une souris est connectée et si le bouton gauche a été pressé durant cette frame
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (IsPointerOverBlockingTag())
                {
                    return; // On arrête tout, on ne fait pas apparaître la bulle
                }
                SpawnInfoBox();
            }
        }
        
        private bool IsPointerOverBlockingTag()
        {
            // 1. On prépare les données du pointeur (souris)
            PointerEventData pointerData = new PointerEventData(eventSystem);
            pointerData.position = Mouse.current.position.ReadValue();

            // 2. On prépare une liste pour recevoir les résultats
            List<RaycastResult> results = new List<RaycastResult>();

            // 3. On lance le rayon via le GraphicRaycaster
            if (uiRaycaster != null)
            {
                uiRaycaster.Raycast(pointerData, results);
            }

            // 4. On parcourt tout ce qu'on a touché
            foreach (RaycastResult result in results)
            {
                Debug.Log("raycast" + result.gameObject.name);
                // Si l'objet touché (ou un de ses parents) a le tag bloquant
                if (result.gameObject.CompareTag(blockingTag))
                {
                    return true; // C'est bloqué !
                }
            }

            return false; // Rien de bloquant trouvé
        }

        void SpawnInfoBox()
        {
            // 1. Récupérer la position de la souris avec le NOUVEAU système
            Vector2 mousePos2D = Mouse.current.position.ReadValue();
            Vector3 mousePos = new Vector3(mousePos2D.x, mousePos2D.y, 0);
            
            //todo check si click sur bouton lire liste position bouton
            

            // 2. Créer le prefab
            GameObject newBox = Instantiate(infoBoxPrefab, mousePos, Quaternion.identity, canvasTransform);

            // 3. Trouver le composant Text (version classique)
            // Si tu utilises TextMeshPro, remplace Text par TextMeshProUGUI
            TextMeshProUGUI textComponent = newBox.GetComponentInChildren<TextMeshProUGUI>();

            // 4. Mettre à jour le texte
            if (textComponent != null)
            {
                textComponent.text = $"X: {mousePos.x:F0}\nY: {mousePos.y:F0}";
            }
            
            CanvasGroup cg = newBox.GetComponent<CanvasGroup>();
            if (cg == null) cg = newBox.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;

            // 5. Destruction auto
            if (destroyAfterTime)
            {
                Destroy(newBox, lifeTime);
            }
        }
    }
}