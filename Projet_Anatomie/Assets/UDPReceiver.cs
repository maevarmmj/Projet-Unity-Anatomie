using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class UDPReceiver : MonoBehaviour
{
    Thread receiveThread;
    UdpClient client;
    public int port = 5052;

    public volatile Vector2[] keypoints;
    private volatile bool isRunning = false;

    void Start()
    {
        keypoints = new Vector2[17];
        isRunning = true;
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        // --- DEBUG : CONFIRMER QUE LE SCRIPT DÉMARRE ---
        Debug.Log("UDPReceiver a démarré, le thread d'écoute est lancé.");
        // ---------------------------------------------
    }

    private void ReceiveData()
    {
        client = new UdpClient(port);
        // --- DEBUG : CONFIRMER QUE LE PORT EST OUVERT ---
        Debug.Log($"Écoute sur le port {port}...");
        // ---------------------------------------------

        while (isRunning)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);
                
                // --- DEBUG : ON A REÇU QUELQUE CHOSE ! ---
                string text = Encoding.UTF8.GetString(data);
                Debug.Log($"PAQUET REÇU : {text}");
                // ------------------------------------------

                if (string.IsNullOrEmpty(text) || text == "[]") continue;

                // Le format reçu est directement [[x,y], [x,y], ...] 
                // JsonUtility d'Unity ne gère pas bien ce format, utilisons le parsing manuel directement
                Debug.Log("Utilisation du parsing manuel pour le format [[x,y], [x,y], ...]");
                
                try
                {
                    ParseManually(text);
                }
                catch (Exception manualError)
                {
                    Debug.LogError($"Parsing manuel échoué: {manualError.Message}");
                    Debug.LogError($"Données reçues: {text}");
                    continue;
                }
            }
            catch (SocketException)
            {
                Debug.Log("SocketException: Le client UDP a été fermé (normal à l'arrêt).");
            }
            catch (Exception err)
            {
                Debug.LogError($"Erreur dans le thread UDP: {err.ToString()}");
            }
        }
    }
    
    // ... le reste du script ne change pas ...
    void OnApplicationQuit()
    {
        isRunning = false;
        if (client != null)
        {
            client.Close();
        }
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join();
        }
    }

    // Parsing manuel pour le format [[x,y], [x,y], ...]
    private void ParseManually(string jsonText)
    {
        Debug.Log($"🔧 Parsing manuel de: {jsonText.Substring(0, Math.Min(150, jsonText.Length))}...");
        
        // Nettoyer le texte JSON
        jsonText = jsonText.Trim();
        
        // Vérifier le format attendu
        if (!jsonText.StartsWith("[") || !jsonText.EndsWith("]"))
        {
            throw new Exception($"Format JSON invalide - doit commencer par [ et finir par ]. Reçu: {jsonText.Substring(0, Math.Min(50, jsonText.Length))}");
        }
        
        // Enlever les crochets externes
        jsonText = jsonText.Substring(1, jsonText.Length - 2);
        
        Vector2[] newKeypoints = new Vector2[keypoints.Length];
        
        // Diviser par "], [" pour séparer chaque point
        string[] points = jsonText.Split(new string[] { "], [" }, StringSplitOptions.RemoveEmptyEntries);
        
        Debug.Log($"📊 Nombre de points trouvés: {points.Length}");
        
        for (int i = 0; i < points.Length && i < newKeypoints.Length; i++)
        {
            // Nettoyer chaque point
            string point = points[i].Replace("[", "").Replace("]", "").Trim();
            string[] coords = point.Split(',');
            
            if (coords.Length >= 2)
            {
                // Essayer de parser les coordonnées
                string xStr = coords[0].Trim();
                string yStr = coords[1].Trim();
                
                if (float.TryParse(xStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) && 
                    float.TryParse(yStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                {
                    newKeypoints[i] = new Vector2(x, y);
                    
                    // Debug des premiers points
                    if (i < 5)
                    {
                        Debug.Log($"✅ Point {i}: \"{xStr}\" + \"{yStr}\" -> Vector2({x:F2}, {y:F2})");
                    }
                }
                else
                {
                    Debug.LogWarning($"❌ Impossible de parser le point {i}: x=\"{xStr}\", y=\"{yStr}\"");
                }
            }
            else
            {
                Debug.LogWarning($"❌ Point {i} invalide: {coords.Length} coordonnées au lieu de 2. Contenu: \"{point}\"");
            }
        }
        
        // Assigner le nouveau tableau
        keypoints = newKeypoints;
        
        // Compter les points valides
        int validCount = 0;
        for (int i = 0; i < newKeypoints.Length; i++)
        {
            if (newKeypoints[i] != Vector2.zero) validCount++;
        }
        
        Debug.Log($"🎯 Parsing manuel terminé ! {validCount}/{newKeypoints.Length} points valides");
        Debug.Log($"📍 Exemples: Point[0]={newKeypoints[0]}, Point[5]={newKeypoints[5]}, Point[6]={newKeypoints[6]}");
    }

    // Méthode pour debug depuis le thread principal
    void Update()
    {
        // Debug occasionnel pour vérifier l'état des keypoints
        if (Time.frameCount % 60 == 0) // Toutes les secondes
        {
            if (keypoints != null)
            {
                int validPoints = 0;
                for (int i = 0; i < keypoints.Length; i++)
                {
                    if (keypoints[i] != Vector2.zero) validPoints++;
                }
                Debug.Log($"[UDPReceiver] État keypoints: {validPoints}/{keypoints.Length} valides");
                
                // Afficher quelques exemples
                if (validPoints > 0)
                {
                    Debug.Log($"[UDPReceiver] Exemples - Point 0: {keypoints[0]}, Point 5: {keypoints[5]}, Point 6: {keypoints[6]}");
                }
            }
            else
            {
                Debug.LogError("[UDPReceiver] Keypoints array est null !");
            }
        }
    }

    [System.Serializable]
    private class Wrapper
    {
        public List<List<float>> points;
    }
}