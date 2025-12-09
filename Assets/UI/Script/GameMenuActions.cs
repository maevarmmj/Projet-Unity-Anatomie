using UnityEngine;
namespace UI.Script
{
    public class GameMenuActions : MonoBehaviour
    {
        // Fonction à relier au bouton "Quitter" dans l'inspecteur
        public void QuitApplication()
        {
            Debug.Log("Demande de fermeture de l'application...");

            // Cette partie fonctionne une fois le jeu construit (Build)
            Application.Quit();

            // Cette partie permet de stopper le jeu si on teste dans l'éditeur Unity
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
    #endif
        }
    }
}