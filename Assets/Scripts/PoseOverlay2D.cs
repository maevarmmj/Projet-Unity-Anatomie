using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Dessine un overlay de keypoints et segments par-dessus un RawImage vidéo,
/// avec prise en compte de la rotation et des flips de la preview.
/// </summary>
public class PoseOverlay2D : MonoBehaviour
{
    [Header("Sources")]
    public Recuperation_Points_yolo client;   // ton script qui reçoit les keypoints
    public RawImage preview;                   // le RawImage qui affiche la caméra
    public RectTransform canvasRect;          // Canvas (Screen Space - Overlay) qui contient l’overlay

    [Header("Visuel")]
    public GameObject jointPrefab;            // petite Image (UI) (8-12 px) pour les points
    public float jointScale = 10f;
    public Color boneColor = new Color(0f, 1f, 0.2f, 0.9f);
    public float boneWidth = 2f;
    [Range(0f,1f)] public float emaAlpha = 0.35f; // lissage (0=fort lissage, 1=pas de lissage)
    public float confThreshold = 0.2f;

    // ordre COCO (17 points) – paires de segments
    // Indices COCO (YOLOv8-pose) :
    // 0:nose, 1:left eye, 2:right eye, 3:left ear, 4:right ear,
    // 5:left shoulder, 6:right shoulder, 7:left elbow, 8:right elbow, 9:left wrist, 10:right wrist,
    // 11:left hip, 12:right hip, 13:left knee, 14:right knee, 15:left ankle, 16:right ankle
    private readonly (int a, int b)[] bones = new (int, int)[]
    {
        // TÊTE
        (0,1), (0,2),   // nez → yeux
        (1,3), (2,4),   // yeux → oreilles
        (0,5), (0,6),   // nez → épaules (lien vers le torse)
        // BRAS
        (5,7), (7,9),
        (6,8), (8,10),
        // JAMBes
        (11,13), (13,15),
        (12,14), (14,16),
        // TORSE
        (5,6), (11,12), (5,11), (6,12)
    };


    private RectTransform[] joints;
    private Vector2[] ema; // positions lissées en pixels canvas
    public enum OrientationMode { AutoFromPreview, RawNoTransform, OnlyFlipV }
    [Header("Debug orientation")]
    public OrientationMode orientationMode = OrientationMode.AutoFromPreview;
    public GameObject bonePrefab;
    private RectTransform[] boneRects;
    [Header("Aspect")]
    public bool assumePreserveAspect = true; // on calcule le letterbox/pillarbox nous-mêmes




   // Zone (en pixels Canvas) où la vidéo est réellement visible dans le RawImage,
   // calculée à partir de la taille du RectTransform du RawImage et du ratio de la texture/source.
   Rect GetDisplayedRect()
   {
       // Rect du RawImage dans le Canvas (attention: ici on suppose que l'overlay couvre le Canvas en Stretch)
       Rect canvasR = canvasRect.rect;                // taille totale du Canvas
       Rect riRect  = preview.rectTransform.rect;     // taille allouée au RawImage
       float rw = riRect.width;
       float rh = riRect.height;

       // Point d'origine du RawImage dans le Canvas (anchoredPosition est centré → on ramène en bas-gauche Canvas)
       Vector2 canvasHalf = canvasR.size * 0.5f;
       Vector2 riCenter   = preview.rectTransform.anchoredPosition + canvasHalf; // centre en pixels Canvas
       Vector2 riBL       = riCenter - new Vector2(rw * 0.5f, rh * 0.5f);        // bas-gauche RawImage en Canvas

       // Ratio de la vidéo : on utilise d’abord la taille source (w,h) venant de Python, sinon la texture
       int sw = 0, sh = 0;
       client.TryGetSourceSize(out sw, out sh);
       if (sw <= 0 || sh <= 0)
       {
           var tex = preview.texture;
           if (tex != null) { sw = tex.width; sh = tex.height; }
       }
       if (sw <= 0 || sh <= 0)
           return new Rect(riBL.x, riBL.y, rw, rh); // fallback: occupe tout le RawImage

       float texAspect  = (float)sw / (float)sh;
       float rectAspect = rw / rh;

       // Si on suppose preserve aspect, on calcule les bandes; sinon on remplit tout le rect du RawImage
       float x = riBL.x, y = riBL.y, w = rw, h = rh;

       if (assumePreserveAspect)
       {
           if (rectAspect > texAspect)
           {
               // Pillarbox: la hauteur est pleine, la largeur est réduite
               w = rh * texAspect;
               x = riBL.x + (rw - w) * 0.5f;
               y = riBL.y;
               h = rh;
           }
           else
           {
               // Letterbox: la largeur est pleine, la hauteur est réduite
               h = rw / texAspect;
               y = riBL.y + (rh - h) * 0.5f;
               x = riBL.x;
               w = rw;
           }
       }

       return new Rect(x, y, w, h);
   }


    // Convertit une coord viewport (0..1) vidéo → position “anchored” (Canvas, pivot centre)
    Vector2 VideoViewportToCanvasPos(Vector2 v01)
    {
        // 1) Orientation comme tu le fais déjà
        Vector2 vAdj = v01;
        switch (orientationMode)
        {
            case OrientationMode.RawNoTransform: break;
            case OrientationMode.OnlyFlipV: vAdj = new Vector2(v01.x, 1f - v01.y); break;
            case OrientationMode.AutoFromPreview: vAdj = ApplyPreviewOrientation(v01); break;
        }

        // 2) Zone réellement affichée de la vidéo
        Rect disp = GetDisplayedRect();

        // 3) Coord en pixels Canvas, origine bas-gauche du Canvas
        Vector2 pxBL = new Vector2(disp.x + vAdj.x * disp.width,
                                   disp.y + vAdj.y * disp.height);

        // 4) anchoredPosition attend un offset depuis le centre du Canvas (pivot centre)
        Vector2 canvasHalf = new Vector2(canvasRect.rect.width, canvasRect.rect.height) * 0.5f;
        return pxBL - canvasHalf;
    }

    void Start()
    {
        // Fabrique 17 joints
        joints = new RectTransform[17];
        for (int i = 0; i < joints.Length; i++)
        {
            var go = Instantiate(jointPrefab, canvasRect);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = Vector2.one * jointScale;
            rt.gameObject.SetActive(false);
            joints[i] = rt;
        }
        ema = new Vector2[17];
        boneRects = new RectTransform[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            var go = Instantiate(bonePrefab, canvasRect);
            boneRects[i] = go.GetComponent<RectTransform>();
            boneRects[i].gameObject.SetActive(false);
        }

        // S’assure que l’overlay est au-dessus
        var cv = canvasRect.GetComponent<Canvas>();
        if (cv != null) cv.sortingOrder = 10;
    }

    void Update()
    {
        
        if (client == null || preview == null || canvasRect == null) return;
        if (!client.TryGetKeypointsViewport(out var vp, confThreshold)) return;

      
        // Convertit points viewport (0..1) -> pixels overlay en tenant compte de la rotation et des flips du RawImage
        for (int i = 0; i < joints.Length && i < vp.Length; i++)
        {
            var v = vp[i];
            bool valid = v.x >= 0f && v.y >= 0f;
            if (!valid)
            {
                joints[i].gameObject.SetActive(false);
                continue;
            }

            // Applique les mêmes flips/rotation que la preview
            Vector2 vAdj = v;
            switch (orientationMode)
            {
                case OrientationMode.RawNoTransform:
                    // ne rien faire
                    break;
                case OrientationMode.OnlyFlipV:
                    vAdj = new Vector2(v.x, 1f - v.y);
                    break;
                case OrientationMode.AutoFromPreview:
                    vAdj = ApplyPreviewOrientation(v); // ton code auto existant
                    break;
            }
            Vector2 px = VideoViewportToCanvasPos(v);

            // Lissage EMA (exponentiel)
            if (!joints[i].gameObject.activeSelf || emaAlpha >= 0.999f)
                ema[i] = px;
            else
                ema[i] = Vector2.Lerp(ema[i], px, emaAlpha);

            joints[i].anchoredPosition = ema[i];
            joints[i].gameObject.SetActive(true);
        }

        for (int i = 0; i < bones.Length; i++)
        {
            var (a, c) = bones[i];
            if (!joints[a].gameObject.activeSelf || !joints[c].gameObject.activeSelf)
            {
                boneRects[i].gameObject.SetActive(false);
                continue;
            }

            Vector2 A = joints[a].anchoredPosition;
            Vector2 C = joints[c].anchoredPosition;

            var mid = (A + C) * 0.5f;
            var dir = (C - A);
            float len = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            var br = boneRects[i];
            br.sizeDelta = new Vector2(len, Mathf.Max(1f, boneWidth));
            br.anchoredPosition = mid;
            br.localRotation = Quaternion.Euler(0, 0, angle);
            br.gameObject.SetActive(true);
        }

    }

    // Applique le même miroir/rotation que ton RawImage de preview
    // Entrée: coord viewport (0..1) "logiques" venant de client.TryGetKeypointsViewport (déjà flipY OpenCV→Unity)
    // Sortie: coord viewport après rotation/mirroir pour correspondre visuellement à la preview.
    Vector2 ApplyPreviewOrientation(Vector2 v)
    {
        // 1) point de départ (viewport standard)
        float x = v.x, y = v.y;

        // 2) appliquer le flip vertical de la preview (si uvRect.height < 0)
        var uv = preview.uvRect;
        bool vFlip = uv.height < 0f;
        bool hFlip = uv.width < 0f; // si tu actives forceHorizontalMirror dans ta preview

        if (vFlip) y = 1f - y;
        if (hFlip) x = 1f - x;

        // 3) appliquer la rotation du RawImage (z euler)
        //    Unity a mis preview.rectTransform.localEulerAngles.z = -webcam.videoRotationAngle
        int rot = Mathf.RoundToInt(preview.rectTransform.localEulerAngles.z);
        rot = ((rot % 360) + 360) % 360; // 0..359

        // Convertir rotation de l’image dans l’espace viewport
        // (0: rien, 90: tourner sens horaire, 180: demi-tour, 270: antihoraire)
        switch (rot)
        {
            case 90:
                {
                    float nx = y;
                    float ny = 1f - x;
                    x = nx; y = ny;
                    break;
                }
            case 180:
                x = 1f - x;
                y = 1f - y;
                break;
            case 270:
                {
                    float nx = 1f - y;
                    float ny = x;
                    x = nx; y = ny;
                    break;
                }
        }

        return new Vector2(x, y);
    }
}
