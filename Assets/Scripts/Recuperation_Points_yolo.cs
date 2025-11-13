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
    public string pythonExePath = "/Users/tomroyer/Desktop/Eseo/I3/S9/UNITY/Projet/Anatomie/.venv/bin/python3.11";   // ← adapte
    public string pythonScriptPath = "/Users/tomroyer/Desktop/Eseo/I3/S9/UNITY/Projet/Anatomie/Yolo.py";   // ← adapte
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

    // --- MODIFIÉ : Résultats 3D ---
    private volatile int srcW, srcH;
    public bool forceHorizontalMirror = false;
    public bool debugLogs = true;
    public int lastPointCount = 0;
    public int SrcW => srcW;
    public int SrcH => srcH;

    // NOUVELLES VARIABLES pour stocker les données 3D
    public volatile Point3D[] latestBody3D;
    public volatile Hands3D latestHands3D;
    // --- FIN MODIFIÉ ---


    public bool TryGetSourceSize(out int w, out int h)
    {
        w = srcW; h = srcH;
        return (w > 0 && h > 0);
    }

    int ReadInt32BE(BinaryReader br)
    {
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
        float interval = 1f / Mathf.Max(1f, sendFps);
        float nextSend = 0f;

        while (running)
        {
            yield return new WaitForEndOfFrame();
            if (webcam == null || !webcam.isPlaying || webcam.width <= 16 || webcam.height <= 16)
                continue;

            if (Time.unscaledTime < nextSend)
                continue;

            if (rt == null || rtW != webcam.width || rtH != webcam.height)
            {
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

                if (captureTex == null || captureTex.width != rtW || captureTex.height != rtH)
                    captureTex = new Texture2D(rtW, rtH, TextureFormat.RGB24, false);
            }

            Graphics.Blit(webcam, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            captureTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            captureTex.Apply(false);
            RenderTexture.active = prev;
            byte[] jpg = captureTex.EncodeToJPG(jpegQuality);

            lock (frameLock)
            {
                nextFrameJpg = jpg;
            }
            nextSend = Time.unscaledTime + interval;
        }
    }

    void Update()
    {
        ApplyPreviewOrientation();
    }

    void ApplyPreviewOrientation()
    {
        if (webcam == null || !webcam.isPlaying) return;
        preview.rectTransform.localEulerAngles = new Vector3(0, 0, -webcam.videoRotationAngle);
        Rect uv = preview.uvRect;
        bool vMirrored = webcam.videoVerticallyMirrored;
        uv = vMirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
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
                Debug.Log("✅ Connecté au serveur Python 3D."); // Modifié

                using (client)
                using (var stream = client.GetStream())
                using (var bw = new BinaryWriter(stream))
                using (var br = new BinaryReader(stream))
                {
                    while (running)
                    {
                        byte[] jpgToSend = null;
                        lock (frameLock)
                        {
                            if (nextFrameJpg != null) { jpgToSend = nextFrameJpg; nextFrameJpg = null; }
                        }
                        if (jpgToSend == null) { Thread.Sleep(1); continue; }

                        bw.Write(System.Net.IPAddress.HostToNetworkOrder(jpgToSend.Length));
                        bw.Write(jpgToSend);

                        int jsonSize = ReadInt32BE(br);
                        if (jsonSize <= 0 || jsonSize > 1_000_000)
                            throw new EndOfStreamException("invalid size");

                        byte[] jsonBytes = ReadExact(br, jsonSize);
                        if (jsonBytes == null)
                            throw new EndOfStreamException("payload closed");

                        string json = System.Text.Encoding.UTF8.GetString(jsonBytes);

                        // --- MODIFIÉ : Décodage du nouveau JSON 3D ---
                        var wrapper = JsonUtility.FromJson<Wrapper>("{\"data\":" + json + "}");
                        if (wrapper?.data != null)
                        {
                            srcW = wrapper.data.w; srcH = wrapper.data.h;

                            // Stocke les nouvelles données 3D
                            latestBody3D = wrapper.data.body3d;
                            latestHands3D = wrapper.data.hands;
                            

                            lastPointCount = (latestBody3D != null) ? latestBody3D.Length : 0;
                        }
                        // --- FIN MODIFIÉ ---
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


    // --- MODIFIÉ : API publique pour 2D supprimée ---
    // L'ancienne fonction TryGetKeypointsViewport a été supprimée
    // car elle était liée à l'overlay 2D et aux données YOLO (body17).
    // Nous la remplacerons par un script 3D.


    // --- MODIFIÉ : Classes de données C# pour correspondre au JSON 3D ---
    [Serializable] class Wrapper { public Data data; }

    [Serializable]
    class Data
    {
        public int w; public int h;
        // "body17" et "feet" ont disparu
        public Point3D[] body3d;   // NOUVEAU: 33 points du corps 3D
        public Hands3D hands;      // MODIFIÉ: utilise Point3D
    }

    [Serializable]
    public class Point3D // MODIFIÉ: Ancien "Point"
    {
        public float x, y, z, c; // Ajout de 'z'
    }

    [Serializable]
    public class Hands3D // MODIFIÉ: Ancien "Hands"
    {
        public Point3D[] left;
        public Point3D[] right;
    }

    // Les classes "Feet" et "FeetSide" ont été supprimées.
}