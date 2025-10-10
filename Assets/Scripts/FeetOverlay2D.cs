using UnityEngine;
using UnityEngine.UI;
using System;

public class FeetOverlay2D : MonoBehaviour
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
    public Color leftFootColor  = new Color(0.4f, 1f, 0.6f, 0.95f);
    public Color rightFootColor = new Color(1f, 0.4f, 0.6f, 0.95f);
    [Range(0f,1f)] public float emaAlpha = 0.35f;
    public float confThreshold = 0.15f;

    [Header("Aspect")]
    public bool assumePreserveAspect = true;

    public enum OrientationMode { AutoFromPreview, RawNoTransform, OnlyFlipV }
    [Header("Orientation")]
    public OrientationMode orientationMode = OrientationMode.AutoFromPreview;

    // UI : L/R * (ankle, heel, bigtoe)
    private RectTransform ankleL, heelL, bigtoeL;
    private RectTransform ankleR, heelR, bigtoeR;
    private Vector2 emaAnkL, emaHeelL, emaBigL, emaAnkR, emaHeelR, emaBigR;

    // Segments (cheville->talon, cheville->gros orteil)
    private RectTransform segAnkHeelL, segAnkBigL, segAnkHeelR, segAnkBigR;

    void Start()
    {
        BuildFootUI(leftFootColor,  out ankleL, out heelL, out bigtoeL, out segAnkHeelL, out segAnkBigL);
        BuildFootUI(rightFootColor, out ankleR, out heelR, out bigtoeR, out segAnkHeelR, out segAnkBigR);

        var cv = canvasRect.GetComponent<Canvas>();
        if (cv != null) cv.sortingOrder = Mathf.Max(cv.sortingOrder, 11);
    }

    void BuildFootUI(Color c, out RectTransform ankle, out RectTransform heel, out RectTransform bigtoe,
                     out RectTransform segAnkHeel, out RectTransform segAnkBig)
    {
        ankle  = Instantiate(jointPrefab, canvasRect).GetComponent<RectTransform>();
        heel   = Instantiate(jointPrefab, canvasRect).GetComponent<RectTransform>();
        bigtoe = Instantiate(jointPrefab, canvasRect).GetComponent<RectTransform>();
        SetupDot(ankle, c); SetupDot(heel, c); SetupDot(bigtoe, c);

        segAnkHeel = Instantiate(bonePrefab, canvasRect).GetComponent<RectTransform>();
        segAnkBig  = Instantiate(bonePrefab, canvasRect).GetComponent<RectTransform>();
        SetupSeg(segAnkHeel, c); SetupSeg(segAnkBig, c);
    }

    void SetupDot(RectTransform rt, Color c)
    {
        rt.sizeDelta = Vector2.one * jointScale;
        var img = rt.GetComponent<Image>(); if (img) img.color = c;
        rt.gameObject.SetActive(false);
    }
    void SetupSeg(RectTransform rt, Color c)
    {
        var img = rt.GetComponent<Image>(); if (img) img.color = c;
        rt.gameObject.SetActive(false);
    }

    void Update()
    {
        if (client == null || preview == null || canvasRect == null) return;
        var ft = client.latestFeet; // Feet (left/right avec ankle/heel/bigtoe)

        UpdateOneFoot(ft?.left,  ref ankleL, ref heelL, ref bigtoeL, ref segAnkHeelL, ref segAnkBigL,
                      ref emaAnkL, ref emaHeelL, ref emaBigL);
        UpdateOneFoot(ft?.right, ref ankleR, ref heelR, ref bigtoeR, ref segAnkHeelR, ref segAnkBigR,
                      ref emaAnkR, ref emaHeelR, ref emaBigR);
    }

    void UpdateOneFoot(Recuperation_Points_yolo.FeetSide side,
                       ref RectTransform ankle, ref RectTransform heel, ref RectTransform bigtoe,
                       ref RectTransform segAnkHeel, ref RectTransform segAnkBig,
                       ref Vector2 emaAnk, ref Vector2 emaHeel, ref Vector2 emaBig)
    {
        bool hasAnk = Valid(side?.ankle);
        bool hasHeel= Valid(side?.heel);
        bool hasBig = Valid(side?.bigtoe);

        // Points
        if (hasAnk)  SetDot(side.ankle,  ref ankle,  ref emaAnk);
        else         ankle.gameObject.SetActive(false);

        if (hasHeel) SetDot(side.heel,   ref heel,   ref emaHeel);
        else         heel.gameObject.SetActive(false);

        if (hasBig)  SetDot(side.bigtoe, ref bigtoe, ref emaBig);
        else         bigtoe.gameObject.SetActive(false);

        // Segments
        if (hasAnk && hasHeel) SetSeg(ankle.anchoredPosition, heel.anchoredPosition, ref segAnkHeel);
        else                   segAnkHeel.gameObject.SetActive(false);

        if (hasAnk && hasBig)  SetSeg(ankle.anchoredPosition, bigtoe.anchoredPosition, ref segAnkBig);
        else                   segAnkBig.gameObject.SetActive(false);
    }

    bool Valid(Recuperation_Points_yolo.Point p)
        => p != null && p.c >= confThreshold && p.x >= 0 && p.y >= 0;

    void SetDot(Recuperation_Points_yolo.Point p, ref RectTransform dot, ref Vector2 ema)
    {
        if (!GetSourceWH(out var sw, out var sh)) { dot.gameObject.SetActive(false); return; }
        Vector2 v01 = new Vector2(p.x / sw, 1f - (p.y / sh));

        Vector2 pos = VideoViewportToCanvasPos(v01);
        if (!dot.gameObject.activeSelf || emaAlpha >= 0.999f) ema = pos;
        else ema = Vector2.Lerp(ema, pos, emaAlpha);
        dot.anchoredPosition = ema;
        dot.gameObject.SetActive(true);
    }

    void SetSeg(Vector2 A, Vector2 B, ref RectTransform seg)
    {
        var mid = (A + B) * 0.5f;
        var dir = (B - A);
        float len = dir.magnitude;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        seg.sizeDelta = new Vector2(len, Mathf.Max(1f, boneWidth));
        seg.anchoredPosition = mid;
        seg.localRotation = Quaternion.Euler(0, 0, ang);
        seg.gameObject.SetActive(true);
    }

    // ----------- Mapping vidéo → Canvas (identique à Hands/Pose) -----------
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