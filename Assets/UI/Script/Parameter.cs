using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UI.Script
{
    public class Parameter : MonoBehaviour
    {
        public Sprite gearSprite; // L'image de l'écrou
        public Sprite crossSprite; // L'image de la croix
        
        public Sprite gearHoverSprite;  // L'image de l'écrou survolé
        public Sprite crossHoverSprite;

        public GameObject optionsPanel; 
        
        private Image _buttonImage; 
        private Button buttonComponent;
        private bool _isCrossDisplayed = false; // Permet de suivre l'état actuel : false = écrou, true = croix

        // Start est appelée une fois avant la première exécution de Update après la création du MonoBehaviour
        void Start()
        {
            // On récupère le composant Image qui est sur le même GameObject que ce script.
            // Assurez-vous que ce script est attaché à l'objet UI qui contient le composant Image (votre bouton).
            _buttonImage = GetComponent<Image>();
            buttonComponent = GetComponent<Button>();

            if (_buttonImage == null)
            {
                Debug.LogError("Le script Parameter nécessite un composant Image sur le même GameObject.", this);
                enabled = false;
                return;
            }

            if (buttonComponent == null)
            {
                Debug.LogError("Le script Parameter nécessite un composant Button sur le même GameObject.", this);
                enabled = false;
                return;
            }
            
            buttonComponent.transition = Selectable.Transition.SpriteSwap;
            
            SetToGearState();
        }
        
        private void SetToGearState()
        {
            if (gearSprite != null)
            {
                _buttonImage.sprite = gearSprite;
                _isCrossDisplayed = false;

                SpriteState spriteState = buttonComponent.spriteState;
                spriteState.highlightedSprite = gearHoverSprite;
                buttonComponent.spriteState = spriteState;
                if (optionsPanel != null)
                {
                    optionsPanel.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning("Le sprite de l'écrou n'est pas assigné dans l'Inspector !", this);
            }
        }

        private void SetToCrossState()
        {
            if (crossSprite != null)
            {
                _buttonImage.sprite = crossSprite;
                _isCrossDisplayed = true;

                SpriteState spriteState = buttonComponent.spriteState;
                spriteState.highlightedSprite = crossHoverSprite;
                buttonComponent.spriteState = spriteState;
                if (optionsPanel != null)
                {
                    optionsPanel.SetActive(true);
                }
            }
            else
            {
                Debug.LogWarning("Le sprite de la croix n'est pas assigné dans l'Inspector !", this);
            }
        }

        public void ToggleImage()
        {
            if (_isCrossDisplayed)
            {
                SetToGearState(); // Passer à l'état écrou
            }
            else
            {
                SetToCrossState(); // Passer à l'état croix
            }

            // Désélectionner le bouton
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
        public void QuitApplication()
        {
            Debug.Log("Fermeture de l'application..."); // Pour voir que ça marche dans l'éditeur
            Application.Quit();
        }
    }
}