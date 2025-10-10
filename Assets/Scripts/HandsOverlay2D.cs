using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class HandsOverlay2D : MonoBehaviour
{
    [Header("Sources")]
    public Recuperation_Points_yolo client;
    public RawImage preview;
    public RectTransform canvasRect;

    [Header("Prefabs UI")]
    public GameObject jointPrefab;    // petit cercle UI
    public GameObject bonePrefab;     // petite barre UI (2x50px)

    [Header("Style")]
    public float jointScale = 10f;
    public float boneWidth  = 2f;
    public Color leftHandColor  = new Color(0.2f, 0.8f, 1f, 0.95f);
    public Color rightHandColor = new Color(1f, 0.6f, 0.2f, 0.95f);
    [Range(0f,1f)] public float emaAlpha = 0.35f;
    public float confThreshold = 0.15f;

    [Header("Aspect")]
    public bool assumePreserveAspect = true;

    public enum OrientationMode { AutoFromPreview, RawNoTransform, OnlyFlipV }
    [Header("Orientation")]
    public OrientationMode orientationMode = OrientationMode.AutoFromPreview;

    // Connexions officielles MediaPipe Hands (0..20)
    // (thumb) 0-1,1-2,2-3,3-4 ; (index) 0-5,5-6,6-7,7-8 ; (middle) 0-9,9-10,10-11,11-12 ;
    // (ring) 0-13,13-14,14-15,15-16 ; (pinky) 0-17,17-18,18-19,19-20 ; et chaînes entre MCPs : 5-9,9-13,13-17
    private readonly (int a, int b)[] handBones = new (int, int)[]
    {
        (0,1),(1,2),(2,3),(3,4),
        (0,5),(5,6),(6,7),(7,8),
        (0,9),(9,10),(10,11),(11,12),
        (0,13),(13,14),(14,15),(15,16),
        (0,17),(17,18),(18,19),(19,20),
        (5,9),(9,13),(13,17)
    };

    // UI structures pour chaque main
    private RectTransform[] jointsL, jointsR;
    private RectTransform[] bonesL,  bonesR;
    private Vector2[] emaL, emaR;

    void Start()
    {
        BuildHandUI(ref jointsL, ref bonesL, ref emaL, leftHandColor);
        BuildHandUI(ref jointsR, ref bonesR, ref emaR, rightHandColor);

        var cv = canvasRect.GetComponent<Canvas>();
        if (cv != null) cv.sortingOrder = Mathf.Max(cv.sortingOrder, 12);
    }

    void BuildHandUI(ref RectTransform[] joints, ref RectTransform[] bones, ref Vector2[] ema, Color color)
    {
        joints = new RectTransform[21];
        ema    = new Vector2[21];
        for (int i = 0; i < 21; i++)
        {
            var go = Instantiate(jointPrefab, canvasRect);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = Vector2.one * jointScale;
            var img = go.GetComponent<Image>(); if (img) img.color = color;
            rt.gameObject.SetActive(false);
            joints[i] = rt;
        }
        bones = new RectTransform[handBones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            var go = Instantiate(bonePrefab, canvasRect);
            var rt = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>(); if (img) img.color = color;
            rt.gameObject.SetActive(false);
            bones[i] = rt;
        }
    }

    void Update()
    {
        if (client == null || preview == null || canvasRect == null) return;

        // Récupère mains depuis le client (stockées depuis le JSON Python)
        // Expected: client expose latestHandsLeft / latestHandsRight (Point[]), sinon adapte ici.
        var left  = client.latestHandsLeft;
        var right = client.latestHandsRight;

        // Masque tout si pas de main
        bool hasL = left  != null && left.Length  == 21;
        bool hasR = right != null && right.Length == 21;

        UpdateOneHand(hasL ? left : null, jointsL, bonesL, emaL);
        UpdateOneHand(hasR ? right: null, jointsR, bonesR, emaR);
    }

    void UpdateOneHand(Recuperation_Points_yolo.Point[] hand, RectTransform[] joints, RectTransform[] bones, Vector2[] ema)
    {
        // Si hand=null → tout masquer
        if (hand == null)
        {
            for (int i = 0; i < joints.Length; i++) joints[i].gameObject.SetActive(false);
            for (int i = 0; i < bones.Length;  i++) bones[i].gameObject.SetActive(false);
            return;
        }

        // Points
        for (int i = 0; i < 21; i++)
        {
            var p = hand[i];
            bool ok = p != null && p.c >= confThreshold && p.x >= 0 && p.y >= 0;
            if (!ok) { joints[i].gameObject.SetActive(false); continue; }

            if (!GetSourceWH(out var sw, out var sh)) { joints[i].gameObject.SetActive(false); continue; }
            Vector2 v01 = new Vector2(p.x / sw, 1f - (p.y / sh));

            Vector2 pos = VideoViewportToCanvasPos(v01);

            if (!joints[i].gameObject.activeSelf || emaAlpha >= 0.999f) ema[i] = pos;
            else ema[i] = Vector2.Lerp(ema[i], pos, emaAlpha);

            joints[i].anchoredPosition = ema[i];
            joints[i].gameObject.SetActive(true);
        }

        // Os
        for (int i = 0; i < handBones.Length; i++)
        {
            var (a, b) = handBones[i];
            if (!joints[a].gameObject.activeSelf || !joints[b].gameObject.activeSelf)
            { bones[i].gameObject.SetActive(false); continue; }

            Vector2 A = joints[a].anchoredPosition;
            Vector2 B = joints[b].anchoredPosition;
            var mid = (A + B) * 0.5f;
            var dir = (B - A);
            float len = dir.magnitude;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            var br = bones[i];
            br.sizeDelta = new Vector2(len, Mathf.Max(1f, boneWidth));
            br.anchoredPosition = mid;
            br.localRotation = Quaternion.Euler(0, 0, ang);
            br.gameObject.SetActive(true);
        }
    }

    // ----------- Mapping vidéo → Canvas (mêmes helpers que PoseOverlay2D) -----------
    bool GetSourceWH(out int sw, out int sh)
    {
        if (client != null && client.TryGetSourceSize(out sw, out sh) && sw > 0 && sh > 0)
            return true;
        sw = sh = 0;
        return false;
    }


    Rect GetDisplayedRect()
    {
        Rect canvasR = canvasRect.rect;
        Rect riRect  = preview.rectTransform.rect;
        float rw = riRect.width, rh = riRect.height;

        Vector2 canvasHalf = canvasR.size * 0.5f;
        Vector2 riCenter   = preview.rectTransform.anchoredPosition + canvasHalf;
        Vector2 riBL       = riCenter - new Vector2(rw * 0.5f, rh * 0.5f);

        int sw, sh;
        if (!GetSourceWH(out sw, out sh))
        {
            var tex = preview.texture;
            if (tex != null) { sw = tex.width; sh = tex.height; }
        }

        if (sw <= 0 || sh <= 0)
        {
            var tex = preview.texture;
            if (tex != null) { sw = tex.width; sh = tex.height; }
        }

        float x = riBL.x, y = riBL.y, w = rw, h = rh;
        if (assumePreserveAspect && sw > 0 && sh > 0)
        {
            float texAspect  = (float)sw / (float)sh;
            float rectAspect = rw / rh;
            if (rectAspect > texAspect) { w = rh * texAspect; x = riBL.x + (rw - w) * 0.5f; }
            else                        { h = rw / texAspect; y = riBL.y + (rh - h) * 0.5f; }
        }
        return new Rect(x, y, w, h);
    }

    Vector2 VideoViewportToCanvasPos(Vector2 v01)
    {
        Vector2 vAdj = v01;
        switch (orientationMode)
        {
            case OrientationMode.RawNoTransform: break;
            case OrientationMode.OnlyFlipV: vAdj = new Vector2(v01.x, 1f - v01.y); break;
            case OrientationMode.AutoFromPreview: vAdj = ApplyPreviewOrientation(v01); break;
        }

        Rect disp = GetDisplayedRect();
        Vector2 pxBL = new Vector2(disp.x + vAdj.x * disp.width,
                                   disp.y + vAdj.y * disp.height);
        Vector2 canvasHalf = new Vector2(canvasRect.rect.width, canvasRect.rect.height) * 0.5f;
        return pxBL - canvasHalf;
    }

    Vector2 ApplyPreviewOrientation(Vector2 v)
    {
        float x = v.x, y = v.y;
        var uv = preview.uvRect;
        bool vFlip = uv.height < 0f;
        bool hFlip = uv.width  < 0f;
        if (vFlip) y = 1f - y;
        if (hFlip) x = 1f - x;

        int rot = Mathf.RoundToInt(preview.rectTransform.localEulerAngles.z);
        rot = ((rot % 360) + 360) % 360;
        switch (rot)
        {
            case 90:  { float nx = y;      float ny = 1f - x; x = nx; y = ny; break; }
            case 180: { x = 1f - x; y = 1f - y; break; }
            case 270: { float nx = 1f - y; float ny = x;      x = nx; y = ny; break; }
        }
        return new Vector2(x, y);
    }
}