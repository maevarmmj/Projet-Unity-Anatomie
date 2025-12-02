using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(RawImage))]
public class Recuperation_Points_yolo : MonoBehaviour
{
    [Header("Configuration Réseau")]
    // METTEZ L'IP DE VOTRE PC ICI DANS L'INSPECTEUR UNITY
    public string host = "10.21.23.141";
    public int port = 5053;

    [Header("Camera Capture")]
    public int targetWidth = 2560;
    public int targetHeight = 1600;
    [Range(1, 100)] public int jpegQuality = 60; // Baissé un peu pour le WiFi
    public float sendFps = 15f;

    // --- Options Miroir ---
    [Header("Options Miroir")]
    public bool forceHorizontalMirror = false; // Mettre à TRUE pour la caméra frontale (selfie)

    // --- Interne ---
    private RenderTexture rt;
    private int rtW = -1, rtH = -1;
    private RawImage preview;
    private WebCamTexture webcam;
    private Texture2D captureTex;

    private Thread netThread;
    private volatile bool running;

    private readonly object frameLock = new object();
    private byte[] nextFrameJpg = null;

    // Résultats 3D
    private volatile int srcW, srcH;
    public int SrcW => srcW;
    public int SrcH => srcH;

    public volatile Point3D[] latestBody3D;
    public volatile Hands3D latestHands3D;

    public bool TryGetSourceSize(out int w, out int h) { w = srcW; h = srcH; return (w > 0 && h > 0); }
    int ReadInt32BE(BinaryReader br) { byte[] b = ReadExact(br, 4); if (b == null || b.Length < 4) throw new EndOfStreamException(); return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]; }
    byte[] ReadExact(BinaryReader br, int len) { byte[] buf = new byte[len]; int read = 0; while (read < len) { int r = br.Read(buf, read, len - read); if (r <= 0) return null; read += r; } return buf; }

    void Start()
    {
        // 1. Démarrer la caméra
        preview = GetComponent<RawImage>();

        // Sur Android, on essaie de prendre la caméra arrière par défaut
        WebCamDevice[] devices = WebCamTexture.devices;
        string camName = "";
        foreach(var d in devices) {
            if (!d.isFrontFacing) { // Préférez la caméra arrière (Back Facing)
                camName = d.name;
                break;
            }
        }

        if (string.IsNullOrEmpty(camName) && devices.Length > 0) camName = devices[0].name;

        webcam = new WebCamTexture(camName, targetWidth, targetHeight);
        preview.texture = webcam;
        webcam.Play();

        captureTex = new Texture2D(2, 2, TextureFormat.RGB24, false);

        // 2. Lancer les threads
        running = true;
        StartCoroutine(CaptureLoop());
        netThread = new Thread(NetLoop) { IsBackground = true };
        netThread.Start();
    }

    void OnDestroy()
    {
        running = false;
        try { netThread?.Join(300); } catch {}
        if (webcam != null && webcam.isPlaying) webcam.Stop();

        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    System.Collections.IEnumerator CaptureLoop()
    {
        float interval = 1f / Mathf.Max(1f, sendFps);
        float nextSend = 0f;

        while (running)
        {
            yield return new WaitForEndOfFrame();
            if (webcam == null || !webcam.isPlaying || webcam.width < 16) continue;
            if (Time.unscaledTime < nextSend) continue;

            if (rt == null || rtW != webcam.width || rtH != webcam.height)
            {
                if (rt != null) rt.Release();
                rtW = webcam.width; rtH = webcam.height;
                rt = new RenderTexture(rtW, rtH, 0, RenderTextureFormat.ARGB32);
                rt.Create();
                captureTex = new Texture2D(rtW, rtH, TextureFormat.RGB24, false);
            }

            Graphics.Blit(webcam, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            captureTex.ReadPixels(new Rect(0, 0, rtW, rtH), 0, 0);
            captureTex.Apply(false);
            RenderTexture.active = prev;

            byte[] jpg = captureTex.EncodeToJPG(jpegQuality);
            lock (frameLock) { nextFrameJpg = jpg; }
            nextSend = Time.unscaledTime + interval;
        }
    }

    void Update() { ApplyPreviewOrientation(); }

    void ApplyPreviewOrientation()
    {
        if (webcam == null || !webcam.isPlaying) return;
        // Sur Android, la rotation est souvent nécessaire
        preview.rectTransform.localEulerAngles = new Vector3(0, 0, -webcam.videoRotationAngle);

        // Ajustement du flip
        Rect uv = preview.uvRect;
        bool vMirrored = webcam.videoVerticallyMirrored;

        // Logique de flip adaptée pour mobile
        float yMin = vMirrored ? 1 : 0;
        float yH = vMirrored ? -1 : 1;

        if (forceHorizontalMirror) uv = new Rect(1, yMin, -1, yH);
        else uv = new Rect(0, yMin, 1, yH);

        preview.uvRect = uv;
    }

    void NetLoop()
    {
        while (running)
        {
            TcpClient client = null;
            try
            {
                // Connexion au PC
                client = new TcpClient();
                var result = client.BeginConnect(host, port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(2000); // Timeout 2s

                if (!success || !client.Connected) {
                    Debug.LogWarning($"[TCP] Impossible de se connecter à {host}:{port}");
                    client.Close();
                    Thread.Sleep(1000);
                    continue;
                }

                client.EndConnect(result);
                client.NoDelay = true;
                Debug.Log($"✅ Connecté au PC ({host}) !");

                using (client)
                using (var stream = client.GetStream())
                using (var bw = new BinaryWriter(stream))
                using (var br = new BinaryReader(stream))
                {
                    while (running)
                    {
                        byte[] jpgToSend = null;
                        lock (frameLock) { if (nextFrameJpg != null) { jpgToSend = nextFrameJpg; nextFrameJpg = null; } }

                        if (jpgToSend == null) { Thread.Sleep(5); continue; }

                        // Envoi
                        bw.Write(System.Net.IPAddress.HostToNetworkOrder(jpgToSend.Length));
                        bw.Write(jpgToSend);

                        // Réception
                        int jsonSize = ReadInt32BE(br);
                        byte[] jsonBytes = ReadExact(br, jsonSize);
                        string json = System.Text.Encoding.UTF8.GetString(jsonBytes);

                        var wrapper = JsonUtility.FromJson<Wrapper>("{\"data\":" + json + "}");
                        if (wrapper?.data != null)
                        {
                            srcW = wrapper.data.w; srcH = wrapper.data.h;
                            latestBody3D = wrapper.data.body3d;
                            latestHands3D = wrapper.data.hands;
                        }
                    }
                }
            }
            catch (Exception)
            {
                Thread.Sleep(500);
            }
        }
    }

    [Serializable] class Wrapper { public Data data; }
    [Serializable] class Data { public int w, h; public Point3D[] body3d; public Hands3D hands; }
    [Serializable] public class Point3D { public float x, y, z, c; }
    [Serializable] public class Hands3D { public Point3D[] left; public Point3D[] right; }
}