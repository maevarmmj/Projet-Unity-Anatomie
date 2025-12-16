using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;


public class AnatomyQuizManager : MonoBehaviour
{
    [Header("Références")]
    public Camera mainCamera;
    public DescriptionModeController descriptionController; // pour désactiver le mode description pendant le quiz
    public Canvas canvas; // optionnel si besoin
    public TextMeshProUGUI instructionText;
    public TextMeshProUGUI feedbackText;
    public TextMeshProUGUI scoreText;

    private string lastTargetId = null;


    [Header("HUD Root")]
    public GameObject quizHUDRoot;

    [Header("UI Lock")]
    public Button descriptionModeButton;
    
    [Header("Réglages Quiz")]
    public bool quizActive = false;
    public int questionsPerRun = 10;
    public float feedbackDisplayTime = 1.2f;

    [Header("Liste des IDs jouables (doivent matcher le JSON + BodyPartClickable)")]
    public List<string> targetIds = new List<string>()
    {
        // Exemples : remplace par tes IDs réels
        "DEF-thigh.L",     // jambe gauche (ex)
        "DEF-thigh.R",
        "HumanLungs",
        "HumanHeart",
        "HumanBrain",
    };

    int currentQuestionIndex = 0;
    int correctCount = 0;
    string currentTargetId = null;
    float feedbackUntil = 0f;

    void Start()
    {
        UpdateHUD();
        SetHUDVisible(false);
    }

    void Update()
    {
        if (!quizActive) return;

        // efface feedback après délai
        if (feedbackText != null && Time.unscaledTime > feedbackUntil && feedbackText.text != "")
            feedbackText.text = "";

        // clic souris / tactile : on peut rester compatible avec ton setup InputSystem via DescriptionModeController
        // => ici on s'appuie sur le même raycast que toi, mais on déclenche via Input legacy ?
        // => Pour rester cohérent avec ton projet Input System, on déclenche via GetMouseButtonDown qui peut être bloqué
        // => Donc : on laisse DescriptionModeController gérer l'input ET on lui demande de nous remonter les clics.
        // PLUS SIMPLE : on capture les clics ici avec Input System.
        if (UnityEngine.InputSystem.Touchscreen.current != null)
        {
            var touch = UnityEngine.InputSystem.Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                Vector2 screenPos = touch.position.ReadValue();
                HandleScreenClick(screenPos);
            }
        }
        else if (UnityEngine.InputSystem.Mouse.current != null)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 screenPos = mouse.position.ReadValue();
                HandleScreenClick(screenPos);
            }
        }
    }

    public void ToggleQuiz()
    {
        Debug.Log("🎮 ToggleQuiz() appelé !");
        quizActive = !quizActive;

        // Verrouille / déverrouille le bouton mode description
        if (descriptionModeButton != null)
            descriptionModeButton.interactable = !quizActive;

        // Coupe le mode description si on lance le mini-jeu
        if (descriptionController != null && quizActive)
            descriptionController.descriptionModeActive = false;

        if (quizActive) StartNewRun();
        else StopRun();
    }



    void StartNewRun()
    {
        Debug.Log("StartNewRun()");

        currentQuestionIndex = 0;
        correctCount = 0;
        PickNextTarget();
        SetHUDVisible(true);
        UpdateHUD();
        Debug.Log("HUD should be visible now");
        ShowFeedback("🎮 Mini-jeu démarré !", good: true);
    }

    void StopRun()
    {
        currentTargetId = null;
        SetHUDVisible(false);
        if (feedbackText != null) feedbackText.text = "";
    }

    void PickNextTarget()
    {
        if (targetIds == null || targetIds.Count == 0)
        {
            currentTargetId = null;
            if (instructionText != null) instructionText.text = "Aucune cible configurée.";
            return;
        }

        // Si 1 seul élément, pas possible d'éviter la répétition
        if (targetIds.Count == 1)
        {
            currentTargetId = targetIds[0];
        }
        else
        {
            // Tire au hasard en évitant le même que le précédent (max 10 essais)
            string candidate = null;
            for (int i = 0; i < 10; i++)
            {
                candidate = targetIds[Random.Range(0, targetIds.Count)];
                if (candidate != lastTargetId) break;
            }

            // sécurité si jamais (cas rare)
            if (candidate == lastTargetId)
            {
                // choisit le premier différent
                foreach (var id in targetIds)
                {
                    if (id != lastTargetId) { candidate = id; break; }
                }
            }

            currentTargetId = candidate;
        }

        lastTargetId = currentTargetId;

        // Afficher un nom humain depuis le JSON si dispo
        string displayName = currentTargetId;
        if (BodyPartDescriptionDatabase.Instance != null &&
            BodyPartDescriptionDatabase.Instance.TryGet(currentTargetId, out var desc) &&
            desc != null && !string.IsNullOrEmpty(desc.nom))
        {
            displayName = desc.nom;
        }

        if (instructionText != null)
            instructionText.text = $"Trouve : <b>{displayName}</b>";
    }


    void HandleScreenClick(Vector2 screenPos)
    {
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        var clickable = hit.collider.GetComponent<BodyPartClickable>();
        if (clickable == null) return;

        string clickedId = clickable.DescriptionId;

        if (string.IsNullOrEmpty(currentTargetId))
            return;

        if (clickedId == currentTargetId)
        {
            correctCount++;
            currentQuestionIndex++;

            ShowFeedback("✅ Bien joué !", good: true);

            if (currentQuestionIndex >= questionsPerRun)
            {
                // fin
                if (instructionText != null)
                    instructionText.text = $"Terminé ! Score : {correctCount}/{questionsPerRun}";
                quizActive = false;
                // tu peux laisser le HUD affiché ou le cacher
                // SetHUDVisible(false);
                UpdateHUD();
                return;
            }

            PickNextTarget();
            UpdateHUD();
        }
        else
        {
            // mauvais clic
            ShowFeedback("❌ Non, essaie encore", good: false);
        }
    }

    void ShowFeedback(string msg, bool good)
    {
        if (feedbackText == null) return;
        feedbackText.text = msg;
        feedbackUntil = Time.unscaledTime + feedbackDisplayTime;
    }

    void UpdateHUD()
    {
        if (scoreText != null)
            scoreText.text = $"Score : {correctCount}/{questionsPerRun}";
    }

    void SetHUDVisible(bool v)
    {
        if (quizHUDRoot != null) quizHUDRoot.SetActive(v);
    }

}
