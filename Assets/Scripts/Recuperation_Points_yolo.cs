using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video; // Nécessaire pour la vidéo
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(RawImage))]
public class Recuperation_Points_yolo : MonoBehaviour
{
    [Header("Mode Test")]
    public bool useVideoFile = true; // COCHEZ CA POUR TESTER AVEC LA VIDEO
    public VideoPlayer videoPlayer;  // Glissez le composant Video Player ici
    public RenderTexture videoTexture; // Glissez la Render Texture "VideoOutput" ici
    [Header("Configuration Réseau")]
    public string host = "10.21.23.123"; // Remettez 127.0.0.1 pour tester sur le PC !
    public int port = 5053;

    [Header("Camera Capture")]
    public int targetWidth = 2560;
    public int targetHeight = 1600;
    [Range(1, 100)] public int jpegQuality = 75;
    public float sendFps = 20f; // Un peu plus fluide pour la vidéo

    [Header("Options Miroir")]
    public bool forceHorizontalMirror = false;

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
        preview = GetComponent<RawImage>();
        captureTex = new Texture2D(2, 2, TextureFormat.RGB24, false);

        if (useVideoFile)
        {
            // MODE VIDEO
            if (videoPlayer != null && videoTexture != null)
            {
                preview.texture = videoTexture; // On affiche la vidéo sur l'écran
                videoPlayer.Play();
                Debug.Log("🎬 Mode Vidéo Activé");
            }
            else
            {
                Debug.LogError("Mode Vidéo activé mais VideoPlayer ou RenderTexture manquant !");
            }
        }
        else
        {
            // MODE WEBCAM (Classique)
            WebCamDevice[] devices = WebCamTexture.devices;
            string camName = (devices.Length > 0) ? devices[0].name : "";
            webcam = new WebCamTexture(camName, targetWidth, targetHeight);
            preview.texture = webcam;
            webcam.Play();
        }

        // Démarrage Threads
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
            if (Time.unscaledTime < nextSend) continue;

            Texture sourceTexture = null;

            if (useVideoFile)
            {
                // Source = La Render Texture de la vidéo
                if (videoTexture != null && videoTexture.IsCreated()) sourceTexture = videoTexture;
            }
            else
            {
                // Source = La Webcam
                if (webcam != null && webcam.isPlaying && webcam.width > 16) sourceTexture = webcam;
            }

            if (sourceTexture == null) continue;

            // Encodage JPG
            // On utilise un RenderTexture temporaire pour lire les pixels
            RenderTexture currentRT = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(sourceTexture, currentRT);

            var prev = RenderTexture.active;
            RenderTexture.active = currentRT;

            if (captureTex.width != currentRT.width || captureTex.height != currentRT.height)
                captureTex = new Texture2D(currentRT.width, currentRT.height, TextureFormat.RGB24, false);

            captureTex.ReadPixels(new Rect(0, 0, currentRT.width, currentRT.height), 0, 0);
            captureTex.Apply(false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(currentRT);

            byte[] jpg = captureTex.EncodeToJPG(jpegQuality);
            lock (frameLock) { nextFrameJpg = jpg; }

            nextSend = Time.unscaledTime + interval;
        }
    }

    void Update()
    {
        if (!useVideoFile) ApplyPreviewOrientation();
    }


    void ApplyPreviewOrientation()
    {
        if (webcam == null || !webcam.isPlaying) return;
        preview.rectTransform.localEulerAngles = new Vector3(0, 0, -webcam.videoRotationAngle);
        preview.uvRect = webcam.videoVerticallyMirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
        if (forceHorizontalMirror) preview.uvRect = new Rect(1, preview.uvRect.y, -1, preview.uvRect.height);
    }

    void NetLoop()
    {
        while (running)
        {
            TcpClient client = null;
            try
            {
                // Connexion Standard
                client = new TcpClient();
                var result = client.BeginConnect(host, port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(1000);

                if (!success || !client.Connected) {
                    client.Close(); Thread.Sleep(1000); continue;
                }
                client.EndConnect(result);
                client.NoDelay = true;

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

                        bw.Write(System.Net.IPAddress.HostToNetworkOrder(jpgToSend.Length));
                        bw.Write(jpgToSend);

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
            catch (Exception) { Thread.Sleep(500); }
        }
    }

    [Serializable] class Wrapper { public Data data; }
    [Serializable] class Data { public int w, h; public Point3D[] body3d; public Hands3D hands; }
    [Serializable] public class Point3D { public float x, y, z, c; }
    [Serializable] public class Hands3D { public Point3D[] left; public Point3D[] right; }
}