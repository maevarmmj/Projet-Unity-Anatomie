using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics; // Process
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(RawImage))]
public class Recuperation_Points_yolo : MonoBehaviour
{
    [Header("Python Server (Local)")]
    public string pythonExePath = @"C:\Path\to\python.exe";   // ← adapte
    public string pythonScriptPath = @"C:\Path\to\Yolo.py";   // ← adapte
    public string host = "127.0.0.1";
    public int port = 5053;

    [Header("Camera Capture")]
    public int targetWidth = 640;
    public int targetHeight = 480;
    [Range(1, 100)] public int jpegQuality = 70;
    public float sendFps = 15f;

    // en haut de la classe
    private RenderTexture rt;            // RT persistant, réutilisé
    private int rtW = -1, rtH = -1;      // pour détecter changement de taille

    // Preview Unity
    private RawImage preview;
    private WebCamTexture webcam;
    private Texture2D captureTex;

    // Réseau
    private Thread netThread;
    private volatile bool running;
    private Process pythonProcess;

    // Buffer inter-threads (capture → réseau)
    private readonly object frameLock = new object();
    private byte[] nextFrameJpg = null;      // écrit par capture (main thread), lu par thread réseau

    // Résultats
    private volatile List<Vector3> latestKps = new List<Vector3>(); // (x,y,conf)
    private volatile int srcW, srcH;
    // Ajoute ce champ en haut de la classe si tu veux forcer un miroir horizontal (optionnel)
    public bool forceHorizontalMirror = false;

    public bool debugLogs = true;
    private int dbgCounter = 0;
    public int lastPointCount = 0;

    public bool TryGetSourceSize(out int w, out int h)
    {
        w = srcW; h = srcH;
        return (w > 0 && h > 0);
    }

    int ReadInt32BE(BinaryReader br)
    {
        // Lit 4 octets big-endian avec contrôle
        byte[] b = ReadExact(br, 4);
        if (b == null || b.Length < 4) throw new EndOfStreamException("size header");
        return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
    }

    byte[] ReadExact(BinaryReader br, int len)
    {
        byte[] buf = new byte[len];
        int read = 0;
        while (read < len)
        {
            int r = br.Read(buf, read, len - read);
            if (r <= 0) return null; // socket fermé
            read += r;
        }
        return buf;
    }


    void Start()
    {
        // 1) Lancer Python
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExePath,
                Arguments = $"\"{pythonScriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            pythonProcess = new Process();
            pythonProcess.StartInfo = psi;
            pythonProcess.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"[PY] {e.Data}"); };
            pythonProcess.ErrorDataReceived  += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogError($"[PY ERR] {e.Data}"); };
            pythonProcess.Start();
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();
            Debug.Log($"✅ Script Python lancé : {pythonScriptPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Erreur lancement Python : {ex.Message}");
        }

        // 2) Démarrer la caméra locale (SUR LE THREAD PRINCIPAL)
        preview = GetComponent<RawImage>();
        webcam = new WebCamTexture(targetWidth, targetHeight);
        preview.texture = webcam;
        preview.uvRect = new Rect(0, 1, 1, -1); // flip vertical (OpenCV)
        webcam.Play();

        captureTex = new Texture2D(2, 2, TextureFormat.RGB24, false);

        // 3) Lancer la boucle de capture (coroutine main thread)
        running = true;
        StartCoroutine(CaptureLoop());

        // 4) Lancer le thread réseau (ne touche pas aux API Unity)
        netThread = new Thread(NetLoop) { IsBackground = true };
        netThread.Start();
    }

    void OnDestroy()
    {
        running = false;
        try { netThread?.Join(300); } catch {}

        if (webcam != null && webcam.isPlaying) webcam.Stop();

        // Libération RenderTexture propre
        if (rt != null)
        {
            if (RenderTexture.active == rt) RenderTexture.active = null;
            rt.Release();
            Destroy(rt);
            rt = null;
        }

        try
        {
            if (pythonProcess != null && !pythonProcess.HasExited)
            {
                pythonProcess.Kill();
                pythonProcess.Dispose();
                Debug.Log("🛑 Script Python arrêté.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Erreur fermeture Python: {ex.Message}");
        }
    }


    // --------- CAPTURE (MAIN THREAD) ----------
    System.Collections.IEnumerator CaptureLoop()
    {
        // On envoie à cadence sendFps, mais on capte en fin de frame pour ReadPixels
        float interval = 1f / Mathf.Max(1f, sendFps);
        float nextSend = 0f;

        while (running)
        {
            // Attendre la fin de la frame pour garantir que la texture caméra est prête
            yield return new WaitForEndOfFrame();

            if (webcam == null || !webcam.isPlaying || webcam.width <= 16 || webcam.height <= 16)
                continue;

            if (Time.unscaledTime < nextSend)
                continue;

            // (Ré)allocation si nécessaire
            if (rt == null || rtW != webcam.width || rtH != webcam.height)
            {
                // Libérer l'ancien RT si besoin (en s'assurant qu'il n'est pas actif)
                if (rt != null)
                {
                    if (RenderTexture.active == rt) RenderTexture.active = null;
                    rt.Release();
                    Destroy(rt);
                }

                rtW = webcam.width;
                rtH = webcam.height;
                rt = new RenderTexture(rtW, rtH, 0, RenderTextureFormat.ARGB32);
                rt.Create();

                // Ajuster la Texture2D de capture si la taille change
                if (captureTex == null || captureTex.width != rtW || captureTex.height != rtH)
                    captureTex = new Texture2D(rtW, rtH, TextureFormat.RGB24, false);
            }

            // Copier la WebCam dans le RT persistant
            Graphics.Blit(webcam, rt);

            // Lire les pixels du RT dans la Texture2D (sur le thread principal)
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            captureTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            captureTex.Apply(false);
            RenderTexture.active = prev; // très important

            // Encoder en JPG (toujours sur main thread)
            byte[] jpg = captureTex.EncodeToJPG(jpegQuality);

            // Déposer la frame pour le thread réseau
            lock (frameLock)
            {
                nextFrameJpg = jpg; // on écrase l’ancienne si non lue, ok pour POC
            }

            nextSend = Time.unscaledTime + interval;
        }
    }

    void Update()
    {
        ApplyPreviewOrientation(); // ajuste l’affichage dans l’Editor
    }

    // Adapter l'affichage du RawImage en fonction des propriétés de la webcam
    void ApplyPreviewOrientation()
    {
        if (webcam == null || !webcam.isPlaying) return;

        // Rotation (0, 90, 180, 270)
        preview.rectTransform.localEulerAngles = new Vector3(0, 0, -webcam.videoRotationAngle);

        // Flip vertical automatique selon le driver
        Rect uv = preview.uvRect;
        bool vMirrored = webcam.videoVerticallyMirrored;
        uv = vMirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);

        // (Optionnel) miroir horizontal si l'image est inversée gauche/droite
        if (forceHorizontalMirror)
            uv = new Rect(1, uv.y, -1, uv.height);

        preview.uvRect = uv;
    }



    // --------- RESEAU (BACKGROUND THREAD) ----------
    void NetLoop()
    {
        while (running)
        {
            TcpClient client = null;
            try
            {
                // -- Connexion avec retry --
                while (running && client == null)
                {
                    try
                    {
                        client = new TcpClient();
                        client.NoDelay = true;
                        client.Connect(host, port);
                    }
                    catch
                    {
                        client = null;
                        Thread.Sleep(500); // backoff
                    }
                }
                if (!running || client == null) break;
                Debug.Log("✅ Connecté au serveur Python.");

                using (client)
                using (var stream = client.GetStream())
                using (var bw = new BinaryWriter(stream))
                using (var br = new BinaryReader(stream))
                {
                    while (running)
                    {
                        // Prendre la dernière frame dispo (sinon patienter)
                        byte[] jpgToSend = null;
                        lock (frameLock)
                        {
                            if (nextFrameJpg != null) { jpgToSend = nextFrameJpg; nextFrameJpg = null; }
                        }
                        if (jpgToSend == null) { Thread.Sleep(1); continue; }

                        // Envoyer taille + payload
                        bw.Write(System.Net.IPAddress.HostToNetworkOrder(jpgToSend.Length));
                        bw.Write(jpgToSend);

                        // Lire taille + JSON (robuste)
                        int jsonSize = ReadInt32BE(br);
                        if (jsonSize <= 0 || jsonSize > 1_000_000)
                            throw new EndOfStreamException("invalid size");

                        byte[] jsonBytes = ReadExact(br, jsonSize);
                        if (jsonBytes == null)
                            throw new EndOfStreamException("payload closed");

                        string json = System.Text.Encoding.UTF8.GetString(jsonBytes);

                        var wrapper = JsonUtility.FromJson<Wrapper>("{\"data\":" + json + "}");
                        if (wrapper?.data != null)
                        {
                            srcW = wrapper.data.w;
                            srcH = wrapper.data.h;
                            var pts = new List<Vector3>();
                            if (wrapper.data.points != null)
                                foreach (var p in wrapper.data.points)
                                    pts.Add(new Vector3(p.x, p.y, p.c));

                            latestKps = pts;
                            lastPointCount = pts.Count;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TCP] arrêt: " + e.Message + " → reconnexion…");
                Thread.Sleep(300); // petit backoff puis on retente
            }
        }
    }


    // --- API publique : récupérer les keypoints normalisés (viewport 0..1) ---
    public bool TryGetKeypointsViewport(out Vector2[] vp, float confThreshold = 0.2f)
    {
        vp = null;
        var pts = latestKps; int w = srcW, h = srcH;
        if (pts == null || pts.Count == 0 || w <= 0 || h <= 0) return false;

        int n = Mathf.Min(17, pts.Count);
        var arr = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            var p = pts[i];
            if (p.z < confThreshold) { arr[i] = new Vector2(-1, -1); continue; }
            float nx = Mathf.Clamp01(p.x / w);
            float ny = 1f - Mathf.Clamp01(p.y / h); // flip Y pour Unity
            arr[i] = new Vector2(nx, ny);
        }
        vp = arr;
        return true;
    }

    [Serializable] class Wrapper { public Data data; }

    [Serializable] class Data
    {
        public int w;
        public int h;
        public Point[] points;
    }

    [Serializable] class Point
    {
        public float x;
        public float y;
        public float c; // confidence
    }

}
