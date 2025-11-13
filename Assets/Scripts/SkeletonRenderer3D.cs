using UnityEngine;
using UnityEngine.UI; // Requis pour RawImage

/// <summary>
/// Affiche le squelette 3D en s'alignant sur un RawImage
/// (qui est sur un Canvas 'Screen Space - Camera').
/// Utilise les coordonnées normalisées (0-1) et une profondeur Z relative.
/// </summary>
public class SkeletonRenderer3D : MonoBehaviour
{
    [Header("Source")]
    public Recuperation_Points_yolo client; // À assigner

    [Header("Cible d'alignement")]
    // Glissez votre objet 'Recup_Points' (qui a le RawImage) ici
    public RawImage videoPreview;
    public Camera mainCamera; // Glissez votre 'Main Camera' ici

    [Header("Prefabs & Style")]
    public GameObject jointPrefab;
    public float jointScale = 0.03f;
    public Color bodyColor = Color.green;
    public Color leftHandColor = new Color(0.2f, 0.8f, 1f);
    public Color rightHandColor = new Color(1f, 0.6f, 0.2f);

    [Header("Réglages 3D")]
    [Tooltip("Intensité de l'effet de profondeur. Plus élevé = plus de décalage Z.")]
    public float depthScale = 0.5f;
    [Range(0f, 1f)]
    public float confidenceThreshold = 0.5f;

    private const int BODY_JOINTS = 33;
    private const int HAND_JOINTS = 21;

    private GameObject[] bodyJoints, leftHandJoints, rightHandJoints;
    private Transform bodyParent, leftHandParent, rightHandParent;

    // Pour les calculs d'alignement
    private RectTransform videoRect;
    private Vector3[] videoCorners = new Vector3[4];

    void Start()
    {
        if (client == null || videoPreview == null || mainCamera == null)
        {
            Debug.LogError("Assignez le Client, videoPreview (RawImage), et mainCamera !");
            this.enabled = false;
            return;
        }

        videoRect = videoPreview.rectTransform;

        // Créer les parents
        bodyParent = new GameObject("BodyJoints").transform;
        bodyParent.SetParent(this.transform);
        leftHandParent = new GameObject("LeftHandJoints").transform;
        leftHandParent.SetParent(this.transform);
        rightHandParent = new GameObject("RightHandJoints").transform;
        rightHandParent.SetParent(this.transform);

        // Instancier les sphères
        bodyJoints = new GameObject[BODY_JOINTS];
        BuildSkeleton(ref bodyJoints, bodyParent, bodyColor);
        leftHandJoints = new GameObject[HAND_JOINTS];
        BuildSkeleton(ref leftHandJoints, leftHandParent, leftHandColor);
        rightHandJoints = new GameObject[HAND_JOINTS];
        BuildSkeleton(ref rightHandJoints, rightHandParent, rightHandColor);
    }

    void BuildSkeleton(ref GameObject[] joints, Transform parent, Color color)
    {
        for (int i = 0; i < joints.Length; i++)
        {
            joints[i] = Instantiate(jointPrefab, parent);
            joints[i].name = $"Joint_{i}";
            joints[i].transform.localScale = Vector3.one * jointScale;
            var renderer = joints[i].GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(renderer.material);
                renderer.material.color = color;
            }
            joints[i].SetActive(false);
        }
    }

    void Update()
    {
        if (client == null) return;

        // 1. Obtenir les 4 coins du RawImage dans l'espace 3D
        videoRect.GetWorldCorners(videoCorners);
        // videoCorners[0] = Bas-Gauche (BL)
        // videoCorners[1] = Haut-Gauche (TL)
        // videoCorners[2] = Haut-Droite (TR)
        // videoCorners[3] = Bas-Droite (BR)

        // 2. Mettre à jour les squelettes
        UpdateSkeleton(client.latestBody3D, bodyJoints, true); // true = corps

        var hands = client.latestHands3D;
        UpdateSkeleton(hands?.left,  leftHandJoints, false);
        UpdateSkeleton(hands?.right, rightHandJoints, false);
    }

    void UpdateSkeleton(Recuperation_Points_yolo.Point3D[] points, GameObject[] joints, bool isBody)
    {
        if (points == null)
        {
            foreach (var joint in joints) joint.SetActive(false);
            return;
        }

        for (int i = 0; i < joints.Length; i++)
        {
            if (i >= points.Length)
            {
                joints[i].SetActive(false);
                continue;
            }

            var p = points[i];
            if (p == null || p.c < confidenceThreshold)
            {
                joints[i].SetActive(false);
                continue;
            }

            // --- NOUVELLE LOGIQUE D'ALIGNEMENT ---

            // 1. Coordonnées normalisées de MediaPipe (0,0 = Haut-Gauche)
            float normX = p.x;
            float normY = p.y; // 0.0 = Haut, 1.0 = Bas

            // 2. Trouver la position sur le plan 3D de la vidéo
            // On interpole entre le coin Haut-Gauche et Haut-Droite en utilisant X
            Vector3 posTopEdge = Vector3.Lerp(videoCorners[1], videoCorners[2], normX);
            // On interpole entre le coin Bas-Gauche et Bas-Droite en utilisant X
            Vector3 posBottomEdge = Vector3.Lerp(videoCorners[0], videoCorners[3], normX);
            // On interpole entre le point haut et bas en utilisant Y
            Vector3 posOnPlane = Vector3.Lerp(posTopEdge, posBottomEdge, normY);

            // 3. Appliquer la profondeur Z
            // Le 'z' de MediaPipe (surtout pour le corps) est relatif.
            // On le multiplie par depthScale pour l'amplifier.
            // On le décale "vers l'avant" (direction opposée du 'forward' de la caméra).

            // Pour les mains, le Z est centré sur le poignet (point 0)
            // Pour le corps, il est centré sur les hanches (approx)
            float zOffset = p.z;

            // Les 'z' du corps de MediaPipe Pose sont étranges.
            // On va juste utiliser le 'z' des mains et ignorer celui du corps pour l'instant
            // Sauf si vous voulez un effet 3D plus prononcé.
            if (isBody) {
                zOffset = 0; // Pas de profondeur pour le corps pour l'instant
            }

            // On utilise la direction de la caméra pour décaler
            Vector3 depthDirection = -mainCamera.transform.forward;
            Vector3 finalPos = posOnPlane + (depthDirection * zOffset * depthScale);

            joints[i].transform.position = finalPos;
            joints[i].SetActive(true);
        }
    }
}