using UnityEngine;
using System;
using System.Diagnostics;  // <== nécessaire
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;  // pour éviter conflit avec System.Diagnostics.Debug

public class UDPReceiver : MonoBehaviour
{
    Thread receiveThread;
    UdpClient client;
    public int port = 5052;

    [Header("Python launcher")]
    public string pythonExePath = @"C:\Users\ton_nom\AppData\Local\Programs\Python\Python312\python.exe";
    public string pythonScriptPath = @"C:\Users\ton_nom\Documents\YOLO_Pose\yolo_sender.py";

    private Process pythonProcess;
    private volatile bool isRunning = false;
    public volatile Vector2[] keypoints;

    void Start()
    {
        // --- Lancer le script Python automatiquement ---
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = pythonExePath,
                Arguments = $"\"{pythonScriptPath}\"", // met les guillemets au cas où il y a des espaces
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            pythonProcess = new Process();
            pythonProcess.StartInfo = psi;
            pythonProcess.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log($"[Python] {e.Data}"); };
            pythonProcess.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogError($"[Python ERR] {e.Data}"); };
            pythonProcess.Start();
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();

            Debug.Log($"✅ Script Python lancé : {pythonScriptPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ Erreur lancement Python : {ex.Message}");
        }

        // --- Initialisation UDP ---
        keypoints = new Vector2[17];
        isRunning = true;
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log("UDPReceiver a démarré, écoute sur le port " + port);
    }

    private void ReceiveData()
    {
        client = new UdpClient(port);
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
        while (isRunning)
        {
            try
            {
                byte[] data = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);
                if (string.IsNullOrEmpty(text) || text == "[]") continue;

                Wrapper wrapper = JsonUtility.FromJson<Wrapper>("{\"points\":" + text + "}");
                if (wrapper != null && wrapper.points != null)
                {
                    for (int i = 0; i < wrapper.points.Count && i < keypoints.Length; i++)
                    {
                        if (wrapper.points[i].Count >= 2)
                            keypoints[i] = new Vector2(wrapper.points[i][0], wrapper.points[i][1]);
                    }
                }
            }
            catch (SocketException)
            {
                Debug.Log("SocketException: UDP fermé.");
            }
            catch (Exception err)
            {
                Debug.LogError($"Erreur UDP: {err}");
            }
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        client?.Close();

        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Join();

        // --- Arrêter le process Python ---
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
            Debug.LogWarning($"Erreur à la fermeture Python: {ex.Message}");
        }
    }

    [Serializable]
    private class Wrapper
    {
        public List<List<float>> points;
    }
}
