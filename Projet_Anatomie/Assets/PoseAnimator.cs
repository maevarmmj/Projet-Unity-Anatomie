// PoseAnimator.cs
using UnityEngine;

public class PoseAnimator : MonoBehaviour
{
    public UDPReceiver udpReceiver;
    [Header("Mode Test")]
    public bool enableTestMode = false;
    [Tooltip("Durée en secondes pour tester chaque articulation")]
    public float testDuration = 2f;
    
    private Animator animator;
    private bool isTestingRotations = false;
    private Transform leftUpperArm, rightUpperArm, leftLowerArm, rightLowerArm;
    private Transform leftUpperLeg, rightUpperLeg, leftLowerLeg, rightLowerLeg;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator non trouvé sur ce personnage !");
            this.enabled = false;
            return;
        }

        Debug.Log("[PoseAnimator] Animator détecté, récupération des os...");

        leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

        // Debug de la détection des transforms
        Debug.Log($"[PoseAnimator] Transforms détectés:");
        Debug.Log($"  - Bras gauche (haut): {(leftUpperArm != null ? "✓" : "✗")}");
        Debug.Log($"  - Bras droit (haut): {(rightUpperArm != null ? "✓" : "✗")}");
        Debug.Log($"  - Bras gauche (bas): {(leftLowerArm != null ? "✓" : "✗")}");
        Debug.Log($"  - Bras droit (bas): {(rightLowerArm != null ? "✓" : "✗")}");
        Debug.Log($"  - Jambe gauche (haut): {(leftUpperLeg != null ? "✓" : "✗")}");
        Debug.Log($"  - Jambe droite (haut): {(rightUpperLeg != null ? "✓" : "✗")}");
        Debug.Log($"  - Jambe gauche (bas): {(leftLowerLeg != null ? "✓" : "✗")}");
        Debug.Log($"  - Jambe droite (bas): {(rightLowerLeg != null ? "✓" : "✗")}");

        if (leftUpperArm == null || rightUpperArm == null || leftLowerArm == null || rightLowerArm == null ||
            leftUpperLeg == null || rightUpperLeg == null || leftLowerLeg == null || rightLowerLeg == null)
        {
            Debug.LogWarning("[PoseAnimator] Certains os n'ont pas été trouvés. L'animation pourrait être incomplète.");
        }
        else
        {
            Debug.Log("[PoseAnimator] Tous les os ont été détectés avec succès !");
        }

        // Lancer le test des rotations si le mode test est activé
        if (enableTestMode)
        {
            Debug.Log("[PoseAnimator] Mode test activé - Démarrage du test des rotations...");
            StartCoroutine(TestRotations());
        }
    }

    void LateUpdate()
    {
        if (udpReceiver == null)
        {
            Debug.LogWarning("[PoseAnimator] UDPReceiver non assigné !");
            return;
        }

        if (udpReceiver.keypoints == null)
        {
            Debug.LogWarning("[PoseAnimator] Aucun keypoint reçu (keypoints = null)");
            return;
        }

        if (udpReceiver.keypoints.Length < 17)
        {
            Debug.LogWarning($"[PoseAnimator] Nombre insuffisant de keypoints: {udpReceiver.keypoints.Length}/17");
            return;
        }

        Vector2[] kp = udpReceiver.keypoints;
        
        // Debug des données reçues (occasionnel pour éviter le spam)
        if (Time.frameCount % 60 == 0) // Log toutes les 60 frames (~1 seconde à 60fps)
        {
            Debug.Log($"[PoseAnimator] Keypoints reçus - Frame {Time.frameCount}");
            Debug.Log($"  - Épaule gauche (5): {kp[5]}");
            Debug.Log($"  - Épaule droite (6): {kp[6]}");
            Debug.Log($"  - Coude gauche (7): {kp[7]}");
            Debug.Log($"  - Coude droit (8): {kp[8]}");
        }

        ApplyBoneRotation(leftUpperArm, kp[5], kp[7], 90f, "Bras gauche (haut)");
        ApplyBoneRotation(rightUpperArm, kp[6], kp[8], 90f, "Bras droit (haut)");
        ApplyBoneRotation(leftLowerArm, kp[7], kp[9], 90f, "Bras gauche (bas)");
        ApplyBoneRotation(rightLowerArm, kp[8], kp[10], 90f, "Bras droit (bas)");
        ApplyBoneRotation(leftUpperLeg, kp[11], kp[13], -90f, "Jambe gauche (haut)");
        ApplyBoneRotation(rightUpperLeg, kp[12], kp[14], -90f, "Jambe droite (haut)");
        ApplyBoneRotation(leftLowerLeg, kp[13], kp[15], -90f, "Jambe gauche (bas)");
        ApplyBoneRotation(rightLowerLeg, kp[14], kp[16], -90f, "Jambe droite (bas)");
    }

    void ApplyBoneRotation(Transform bone, Vector2 start, Vector2 end, float offset, string boneName = "Unknown")
    {
        if (bone == null)
        {
            if (Time.frameCount % 300 == 0) // Log toutes les 5 secondes environ
                Debug.LogWarning($"[PoseAnimator] Transform manquant pour {boneName}");
            return;
        }

        if (start == Vector2.zero || end == Vector2.zero)
        {
            if (Time.frameCount % 180 == 0) // Log toutes les 3 secondes environ
                Debug.LogWarning($"[PoseAnimator] Keypoints invalides pour {boneName} - start: {start}, end: {end}");
            return;
        }

        // Conversion des coordonnées écran
        Vector2 correctedStart = new Vector2(start.x, Screen.height - start.y);
        Vector2 correctedEnd = new Vector2(end.x, Screen.height - end.y);
        
        Vector2 dir = (correctedEnd - correctedStart).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float finalAngle = angle + offset;
        
        Quaternion newRotation = Quaternion.Euler(0, 0, finalAngle);
        bone.rotation = newRotation;

        // Debug détaillé occasionnel
        if (Time.frameCount % 120 == 0) // Log toutes les 2 secondes environ
        {
            Debug.Log($"[PoseAnimator] Rotation appliquée sur {boneName}:");
            Debug.Log($"  - Points: {start} -> {end} (corrigés: {correctedStart} -> {correctedEnd})");
            Debug.Log($"  - Direction: {dir}");
            Debug.Log($"  - Angle calculé: {angle:F1}°, Offset: {offset}°, Final: {finalAngle:F1}°");
        }
    }
}