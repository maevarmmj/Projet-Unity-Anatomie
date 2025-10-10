// PoseAnimator.cs
using UnityEngine;
using System.Collections;

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

        // Vérifier la connexion avec UDPReceiver
        if (udpReceiver == null)
        {
            Debug.LogError("[PoseAnimator] PROBLÈME: UDPReceiver n'est pas assigné dans l'inspecteur !");
        }
        else
        {
            Debug.Log($"[PoseAnimator] UDPReceiver trouvé: {udpReceiver.name}");
        }

        // Lancer le test des rotations si le mode test est activé
        if (enableTestMode)
        {
            Debug.Log("[PoseAnimator] Mode test activé - Démarrage du test des rotations...");
            StartCoroutine(TestRotations());
        }
        else
        {
            Debug.Log("[PoseAnimator] Mode normal activé - En attente des données UDP...");
            // Test rapide pour vérifier que les rotations fonctionnent
            StartCoroutine(QuickRotationTest());
        }
    }

    void LateUpdate()
    {
        // Ne pas appliquer les données UDP si on est en mode test
        if (isTestingRotations) return;
        
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
        
        // Debug des données reçues (plus fréquent pour le diagnostic)
        if (Time.frameCount % 30 == 0) // Log toutes les 30 frames (~0.5 seconde à 60fps)
        {
            Debug.Log($"[PoseAnimator] ✅ DONNÉES REÇUES - Frame {Time.frameCount}");
            Debug.Log($"  - Nombre de keypoints: {kp.Length}");
            Debug.Log($"  - Épaule gauche (5): {kp[5]} - Valide: {kp[5] != Vector2.zero}");
            Debug.Log($"  - Épaule droite (6): {kp[6]} - Valide: {kp[6] != Vector2.zero}");
            Debug.Log($"  - Coude gauche (7): {kp[7]} - Valide: {kp[7] != Vector2.zero}");
            Debug.Log($"  - Coude droit (8): {kp[8]} - Valide: {kp[8] != Vector2.zero}");
            
            // Compter le nombre de keypoints valides
            int validKeypoints = 0;
            for (int i = 0; i < kp.Length; i++)
            {
                if (kp[i] != Vector2.zero) validKeypoints++;
            }
            Debug.Log($"  - Keypoints valides: {validKeypoints}/{kp.Length}");
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

        // Debug détaillé plus fréquent pour le diagnostic
        if (Time.frameCount % 60 == 0) // Log toutes les secondes environ
        {
            Debug.Log($"[PoseAnimator] 🔄 ROTATION APPLIQUÉE sur {boneName}:");
            Debug.Log($"  - Points bruts: {start} -> {end}");
            Debug.Log($"  - Points corrigés: {correctedStart} -> {correctedEnd}");
            Debug.Log($"  - Direction: {dir}");
            Debug.Log($"  - Angle calculé: {angle:F1}°, Offset: {offset}°, Final: {finalAngle:F1}°");
            Debug.Log($"  - Rotation avant: {bone.rotation.eulerAngles}");
            Debug.Log($"  - Rotation après: {newRotation.eulerAngles}");
        }
    }

    /// <summary>
    /// Teste les rotations de tous les membres en appliquant des mouvements séquentiels
    /// </summary>
    System.Collections.IEnumerator TestRotations()
    {
        isTestingRotations = true;
        Debug.Log("[PoseAnimator] === DÉBUT DU TEST DES ROTATIONS ===");

        // Stocker les rotations initiales
        var initialRotations = new System.Collections.Generic.Dictionary<Transform, Quaternion>();
        var bones = new System.Collections.Generic.List<(Transform bone, string name)>
        {
            (leftUpperArm, "Bras gauche (haut)"),
            (rightUpperArm, "Bras droit (haut)"),
            (leftLowerArm, "Bras gauche (bas)"),
            (rightLowerArm, "Bras droit (bas)"),
            (leftUpperLeg, "Jambe gauche (haut)"),
            (rightUpperLeg, "Jambe droite (haut)"),
            (leftLowerLeg, "Jambe gauche (bas)"),
            (rightLowerLeg, "Jambe droite (bas)")
        };

        // Sauvegarder les rotations initiales
        foreach (var (bone, name) in bones)
        {
            if (bone != null)
                initialRotations[bone] = bone.rotation;
        }

        // Test 1: Rotation complète de chaque membre individuellement
        Debug.Log("[PoseAnimator] Test 1: Rotation individuelle de chaque membre...");
        foreach (var (bone, name) in bones)
        {
            if (bone == null)
            {
                Debug.LogWarning($"[PoseAnimator] Impossible de tester {name} - Transform manquant");
                continue;
            }

            Debug.Log($"[PoseAnimator] Test de {name}...");
            
            // Faire une rotation de 360° sur testDuration secondes
            float elapsedTime = 0f;
            while (elapsedTime < testDuration)
            {
                float progress = elapsedTime / testDuration;
                float angle = progress * 360f;
                bone.rotation = Quaternion.Euler(0, 0, angle);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Revenir à la position initiale
            if (initialRotations.ContainsKey(bone))
                bone.rotation = initialRotations[bone];
            
            yield return new WaitForSeconds(0.5f); // Petite pause entre les tests
        }

        // Test 2: Mouvements coordonnés (bras ensemble, jambes ensemble)
        Debug.Log("[PoseAnimator] Test 2: Mouvements coordonnés...");
        
        // Test des bras ensemble
        Debug.Log("[PoseAnimator] Test des bras simultanément...");
        float elapsedTime2 = 0f;
        while (elapsedTime2 < testDuration)
        {
            float progress = elapsedTime2 / testDuration;
            float angle = Mathf.Sin(progress * Mathf.PI * 4) * 45f; // Oscillation

            if (leftUpperArm != null) leftUpperArm.rotation = Quaternion.Euler(0, 0, angle);
            if (rightUpperArm != null) rightUpperArm.rotation = Quaternion.Euler(0, 0, -angle);
            if (leftLowerArm != null) leftLowerArm.rotation = Quaternion.Euler(0, 0, angle * 0.5f);
            if (rightLowerArm != null) rightLowerArm.rotation = Quaternion.Euler(0, 0, -angle * 0.5f);

            elapsedTime2 += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        // Test des jambes ensemble
        Debug.Log("[PoseAnimator] Test des jambes simultanément...");
        float elapsedTime3 = 0f;
        while (elapsedTime3 < testDuration)
        {
            float progress = elapsedTime3 / testDuration;
            float angle = Mathf.Sin(progress * Mathf.PI * 3) * 30f; // Oscillation plus douce

            if (leftUpperLeg != null) leftUpperLeg.rotation = Quaternion.Euler(0, 0, angle);
            if (rightUpperLeg != null) rightUpperLeg.rotation = Quaternion.Euler(0, 0, -angle);
            if (leftLowerLeg != null) leftLowerLeg.rotation = Quaternion.Euler(0, 0, angle * 0.7f);
            if (rightLowerLeg != null) rightLowerLeg.rotation = Quaternion.Euler(0, 0, -angle * 0.7f);

            elapsedTime3 += Time.deltaTime;
            yield return null;
        }

        // Test 3: Mouvement de "marche" simulé
        Debug.Log("[PoseAnimator] Test 3: Simulation de marche...");
        float elapsedTime4 = 0f;
        while (elapsedTime4 < testDuration)
        {
            float progress = elapsedTime4 / testDuration;
            float walkCycle = Mathf.Sin(progress * Mathf.PI * 6) * 25f; // Cycle de marche

            // Bras alternés (opposés aux jambes)
            if (leftUpperArm != null) leftUpperArm.rotation = Quaternion.Euler(0, 0, walkCycle);
            if (rightUpperArm != null) rightUpperArm.rotation = Quaternion.Euler(0, 0, -walkCycle);
            
            // Jambes alternées
            if (leftUpperLeg != null) leftUpperLeg.rotation = Quaternion.Euler(0, 0, -walkCycle);
            if (rightUpperLeg != null) rightUpperLeg.rotation = Quaternion.Euler(0, 0, walkCycle);

            elapsedTime4 += Time.deltaTime;
            yield return null;
        }

        // Restaurer toutes les rotations initiales
        Debug.Log("[PoseAnimator] Restauration des rotations initiales...");
        foreach (var kvp in initialRotations)
        {
            if (kvp.Key != null)
                kvp.Key.rotation = kvp.Value;
        }

        Debug.Log("[PoseAnimator] === FIN DU TEST DES ROTATIONS ===");
        Debug.Log("[PoseAnimator] Tous les tests sont terminés. Retour au mode normal.");
        
        isTestingRotations = false;
    }

    /// <summary>
    /// Test rapide pour vérifier que les rotations de base fonctionnent
    /// </summary>
    System.Collections.IEnumerator QuickRotationTest()
    {
        Debug.Log("[PoseAnimator] 🧪 Test rapide des rotations...");
        
        yield return new WaitForSeconds(1f); // Attendre que tout soit initialisé
        
        // Test très simple : faire une petite rotation sur le bras gauche
        if (leftUpperArm != null)
        {
            Debug.Log("[PoseAnimator] Test du bras gauche...");
            Quaternion originalRotation = leftUpperArm.rotation;
            
            // Appliquer une rotation de 45°
            leftUpperArm.rotation = Quaternion.Euler(0, 0, 45f);
            Debug.Log($"[PoseAnimator] Rotation appliquée: {leftUpperArm.rotation.eulerAngles}");
            
            yield return new WaitForSeconds(1f);
            
            // Revenir à la position originale
            leftUpperArm.rotation = originalRotation;
            Debug.Log("[PoseAnimator] Rotation restaurée");
        }
        else
        {
            Debug.LogError("[PoseAnimator] ❌ Impossible de tester - bras gauche non trouvé !");
        }
        
        Debug.Log("[PoseAnimator] Test rapide terminé. Système prêt.");
    }

    /// <summary>
    /// Diagnostic complet du système - Appelez cette fonction depuis l'inspecteur
    /// </summary>
    [ContextMenu("Diagnostic Complet")]
    public void DiagnosticComplet()
    {
        Debug.Log("==================== DIAGNOSTIC POSEANIMATOR ====================");
        
        // 1. Vérifier l'Animator
        Debug.Log("1. VÉRIFICATION ANIMATOR:");
        if (animator == null)
        {
            Debug.LogError("   ❌ Animator non trouvé !");
        }
        else
        {
            Debug.Log($"   ✅ Animator trouvé: {animator.name}");
            Debug.Log($"   - Avatar: {(animator.avatar != null ? animator.avatar.name : "AUCUN")}");
            Debug.Log($"   - Humanoid: {(animator.avatar != null && animator.avatar.isHuman ? "OUI" : "NON")}");
        }

        // 2. Vérifier UDPReceiver
        Debug.Log("2. VÉRIFICATION UDP:");
        if (udpReceiver == null)
        {
            Debug.LogError("   ❌ UDPReceiver non assigné !");
        }
        else
        {
            Debug.Log($"   ✅ UDPReceiver trouvé: {udpReceiver.name}");
            Debug.Log($"   - Port: {udpReceiver.port}");
            Debug.Log($"   - Keypoints: {(udpReceiver.keypoints != null ? udpReceiver.keypoints.Length.ToString() : "NULL")}");
        }

        // 3. Vérifier les transforms
        Debug.Log("3. VÉRIFICATION TRANSFORMS:");
        var bones = new (Transform bone, string name)[]
        {
            (leftUpperArm, "Bras gauche (haut)"),
            (rightUpperArm, "Bras droit (haut)"),
            (leftLowerArm, "Bras gauche (bas)"),
            (rightLowerArm, "Bras droit (bas)"),
            (leftUpperLeg, "Jambe gauche (haut)"),
            (rightUpperLeg, "Jambe droite (haut)"),
            (leftLowerLeg, "Jambe gauche (bas)"),
            (rightLowerLeg, "Jambe droite (bas)")
        };

        int validBones = 0;
        foreach (var (bone, name) in bones)
        {
            if (bone != null)
            {
                Debug.Log($"   ✅ {name}: {bone.name}");
                validBones++;
            }
            else
            {
                Debug.LogError($"   ❌ {name}: NON TROUVÉ");
            }
        }
        Debug.Log($"   RÉSUMÉ: {validBones}/{bones.Length} os trouvés");

        // 4. État du système
        Debug.Log("4. ÉTAT DU SYSTÈME:");
        Debug.Log($"   - Mode test: {enableTestMode}");
        Debug.Log($"   - Test en cours: {isTestingRotations}");
        Debug.Log($"   - Composant actif: {enabled}");

        Debug.Log("======================== FIN DIAGNOSTIC ========================");
    }
}