using UnityEngine;
using System.Collections.Generic;

public class AnatomyRotator : MonoBehaviour
{
    [Header("Source")]
    public Recuperation_Points_yolo client;
    public float confidenceThreshold = 0.5f;
    [Range(0, 1)] public float smoothing = 0.7f;

    [Header("Calibration taille")]
    public bool autoScaleToPerson = true;
    [Range(0f, 1f)] public float scaleSmoothing = 0.8f;
    // Racine du rig à scaler (si null, on prend ce GameObject)
    public Transform rigRoot;

    private float initialRigTorsoLength = 1f;
    private float currentScale = 1f;

    [Header("Décalage profondeur")]
    [Tooltip("Décale tout le squelette vers la caméra pour qu'il soit légèrement devant la personne.")]
    public float depthBias = 0.1f;  // 0.1 = un peu devant

    [Header("Réglages Monde")]
    public UnityEngine.UI.RawImage videoPreview;
    public Camera mainCamera;
    public float depthScale = 3.0f;
    public Vector3 globalOffset = new Vector3(0, 0, 0);

    [Header("--- BASSIN (Hips) ---")]
    public Transform hipsBone; // DEF-spine
    public Vector3 hipsRotationFix = new Vector3(0, 0, 0); // Blender: Souvent (0, 90, 90) ou (90, 0, 0)

    [Header("--- THORAX (Chest) ---")]
    public Transform chestBone; // DEF-spine.003
    public Vector3 chestRotationFix = new Vector3(0, 0, 0); // Blender: Souvent différent du bassin !

    [Header("--- MEMBRES (Bras / Jambes) ---")]
    public List<BoneLink> bonesToRotate = new List<BoneLink>();

    // Variables internes
    private Vector3[] videoCorners = new Vector3[4];
    private RectTransform videoRect;

    [System.Serializable]
    public class BoneLink
    {
        public string name;
        public Transform bone;
        public int startIdx;
        public int endIdx;
    }

    void Start()
    {
        if (videoPreview != null) videoRect = videoPreview.rectTransform;

        // --- Init racine du rig ---
        if (rigRoot == null)
            rigRoot = this.transform; // On suppose que le script est sur le root du squelette

        // --- Longueur de torse de référence du modèle (bind pose) ---
        if (hipsBone != null && chestBone != null)
        {
            initialRigTorsoLength = Vector3.Distance(hipsBone.position, chestBone.position);
        }
        else
        {
            initialRigTorsoLength = 1f;
        }

        currentScale = rigRoot.localScale.x;
    }


    void Update()
    {
        if (client == null || client.latestBody3D == null || client.latestBody3D.Length == 0) return;
        if (videoRect != null) videoRect.GetWorldCorners(videoCorners);

        // 1. GÉRER LE BASSIN (Racine)
        UpdateHips();

        // 2. GÉRER LE THORAX (Relativement au bassin)
        UpdateChest();

        // 3. SCALE GLOBAL EN FONCTION DE LA PERSONNE
        if (autoScaleToPerson)
        {
            UpdateScaleFromPerson();
        }

        // 4. ROTATION DES MEMBRES
        foreach (var link in bonesToRotate)
        {
            RotateBone(link);
        }

    }

    void UpdateScaleFromPerson()
    {
        if (initialRigTorsoLength <= 0.0001f) return;
        if (client.latestBody3D == null || client.latestBody3D.Length < 25) return;

        // Hanches : indices 23 (gauche) & 24 (droite) MediaPipe
        var pLeftHip = client.latestBody3D[23];
        var pRightHip = client.latestBody3D[24];

        if (pLeftHip.c < confidenceThreshold || pRightHip.c < confidenceThreshold) return;

        // Milieu des hanches
        Recuperation_Points_yolo.Point3D midHip = new Recuperation_Points_yolo.Point3D();
        midHip.x = (pLeftHip.x + pRightHip.x) * 0.5f;
        midHip.y = (pLeftHip.y + pRightHip.y) * 0.5f;
        midHip.z = (pLeftHip.z + pRightHip.z) * 0.5f;

        // Épaules : indices 11 (gauche) & 12 (droite) MediaPipe
        var pLeftSh = client.latestBody3D[11];
        var pRightSh = client.latestBody3D[12];
        if (pLeftSh.c < confidenceThreshold || pRightSh.c < confidenceThreshold) return;

        Recuperation_Points_yolo.Point3D midSh = new Recuperation_Points_yolo.Point3D();
        midSh.x = (pLeftSh.x + pRightSh.x) * 0.5f;
        midSh.y = (pLeftSh.y + pRightSh.y) * 0.5f;
        midSh.z = (pLeftSh.z + pRightSh.z) * 0.5f;

        // Positions monde de la personne, sur ton plan vidéo + profondeur
        Vector3 hipWorld = ComputeWorldPos(midHip);
        Vector3 shWorld  = ComputeWorldPos(midSh);
    
        float personTorsoLength = Vector3.Distance(hipWorld, shWorld);
        if (personTorsoLength <= 0.0001f) return;

        // Facteur de scale souhaité
        float targetScale = personTorsoLength / initialRigTorsoLength;

        // Lissage pour éviter que ça pompe
        currentScale = Mathf.Lerp(currentScale, targetScale, 1f - scaleSmoothing);

        rigRoot.localScale = Vector3.one * currentScale;
    }


    void UpdateHips()
    {
        if (hipsBone == null) return;

        // A. POSITION
        var pLeftHip = client.latestBody3D[23];
        var pRightHip = client.latestBody3D[24];
        if (pLeftHip.c < confidenceThreshold || pRightHip.c < confidenceThreshold) return;

        Recuperation_Points_yolo.Point3D mid = new Recuperation_Points_yolo.Point3D();
        mid.x = (pLeftHip.x + pRightHip.x) * 0.5f;
        mid.y = (pLeftHip.y + pRightHip.y) * 0.5f;
        mid.z = (pLeftHip.z + pRightHip.z) * 0.5f;

        Vector3 targetPos = ComputeWorldPos(mid) + globalOffset;
        hipsBone.position = Vector3.Lerp(hipsBone.position, targetPos, 1f - smoothing);

        // B. ROTATION
        // Axe Droit (Hanche G -> Hanche D)
        Vector3 worldLeft = ComputeWorldPos(pLeftHip);
        Vector3 worldRight = ComputeWorldPos(pRightHip);
        Vector3 hipRightVector = (worldRight - worldLeft).normalized;

        // Axe Haut (Vers les épaules)
        var pLeftSh = client.latestBody3D[11];
        var pRightSh = client.latestBody3D[12];
        Vector3 midShoulder = (ComputeWorldPos(pLeftSh) + ComputeWorldPos(pRightSh)) * 0.5f;
        Vector3 upVector = (midShoulder - targetPos).normalized;

        // Axe Avant (Produit vectoriel)
        Vector3 forwardVector = Vector3.Cross(hipRightVector, upVector).normalized;

        if (forwardVector != Vector3.zero && upVector != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(forwardVector, upVector);
            targetRot *= Quaternion.Euler(hipsRotationFix);
            hipsBone.rotation = Quaternion.Slerp(hipsBone.rotation, targetRot, 1f - smoothing);
        }
    }

    void UpdateChest()
    {
        if (chestBone == null) return;

        // On utilise les épaules pour déterminer l'orientation du thorax
        var pLeftSh = client.latestBody3D[11];
        var pRightSh = client.latestBody3D[12];

        if (pLeftSh.c < confidenceThreshold || pRightSh.c < confidenceThreshold) return;

        Vector3 worldLeftSh = ComputeWorldPos(pLeftSh);
        Vector3 worldRightSh = ComputeWorldPos(pRightSh);

        // Axe Droit du Thorax (Epaule G -> Epaule D)
        Vector3 chestRightVector = (worldRightSh - worldLeftSh).normalized;

        // Axe Haut (Du thorax vers le nez, pour l'inclinaison)
        var pNose = client.latestBody3D[0];
        Vector3 worldNose = ComputeWorldPos(pNose);
        Vector3 midShoulder = (worldLeftSh + worldRightSh) * 0.5f;
        Vector3 chestUpVector = (worldNose - midShoulder).normalized;
        // Fallback si le nez n'est pas fiable : on utilise le Vector3.up global relatif
        if (pNose.c < confidenceThreshold) chestUpVector = Vector3.up;

        // Axe Avant
        Vector3 forwardVector = Vector3.Cross(chestRightVector, chestUpVector).normalized;

        if (forwardVector != Vector3.zero && chestUpVector != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(forwardVector, chestUpVector);

            // Correction spécifique pour le thorax !
            targetRot *= Quaternion.Euler(chestRotationFix);

            chestBone.rotation = Quaternion.Slerp(chestBone.rotation, targetRot, 1f - smoothing);
        }
    }

    void RotateBone(BoneLink link)
    {
        if (link.bone == null) return;

        Vector3 mpStart = ComputeWorldPos(client.latestBody3D[link.startIdx]);
        Vector3 mpEnd = ComputeWorldPos(client.latestBody3D[link.endIdx]);
        Vector3 targetDirection = (mpEnd - mpStart).normalized;

        if (targetDirection == Vector3.zero) return;

        if (link.bone.childCount > 0)
        {
            Transform child = link.bone.GetChild(0);
            Vector3 currentBoneDirection = (child.position - link.bone.position).normalized;
            Quaternion rotationDiff = Quaternion.FromToRotation(currentBoneDirection, targetDirection);
            Quaternion finalRot = rotationDiff * link.bone.rotation;
            link.bone.rotation = Quaternion.Slerp(link.bone.rotation, finalRot, 1f - smoothing);
        }
    }

    Vector3 ComputeWorldPos(Recuperation_Points_yolo.Point3D p)
    {
        Vector3 posTop = Vector3.Lerp(videoCorners[1], videoCorners[2], p.x);
        Vector3 posBottom = Vector3.Lerp(videoCorners[0], videoCorners[3], p.x);
        Vector3 posOnPlane = Vector3.Lerp(posTop, posBottom, p.y);

        Vector3 depthDir = -mainCamera.transform.forward;

        // MediaPipe : plus proche caméra => z négatif, on inverse
        float zVal = -p.z;

        // Décalage global vers la caméra pour que le squelette ne soit pas "dans" la personne
        Vector3 safetyOffset = depthDir * depthBias;

        return posOnPlane + (depthDir * zVal * depthScale) + safetyOffset;
    }


}