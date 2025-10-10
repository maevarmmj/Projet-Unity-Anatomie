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

                Wrapper wrapper = JsonUtility.FromJson<Wrapper>("{\"points\":" + text + "}");

                if (wrapper != null && wrapper.points != null)
                {
                    // --- DEBUG : PARSING RÉUSSI ---
                    Debug.Log("Parsing JSON réussi !");
                    // -----------------------------
                    for (int i = 0; i < wrapper.points.Count && i < keypoints.Length; i++)
                    {
                        if (wrapper.points[i].Count >= 2)
                        {
                            keypoints[i] = new Vector2(wrapper.points[i][0], wrapper.points[i][1]);
                        }
                    }
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

    [System.Serializable]
    private class Wrapper
    {
        public List<List<float>> points;
    }
}