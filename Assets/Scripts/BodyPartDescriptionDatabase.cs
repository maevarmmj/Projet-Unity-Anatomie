using System;
using System.Collections.Generic;
using UnityEngine;
// Nécessite Newtonsoft.Json (voir explication plus bas)
using Newtonsoft.Json;

[Serializable]
public class BodyPartDescription
{
    public string nom;
    public string description;
    public string fun_fact;
}

public class BodyPartDescriptionDatabase : MonoBehaviour
{
    public static BodyPartDescriptionDatabase Instance { get; private set; }

    [Header("JSON des descriptions")]
    public TextAsset jsonFile;   // glisser unity_description.json ici

    private Dictionary<string, BodyPartDescription> _map;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (jsonFile == null)
        {
            Debug.LogError("BodyPartDescriptionDatabase : jsonFile non assigné !");
            _map = new Dictionary<string, BodyPartDescription>();
            return;
        }

        try
        {
            // 1) On lit le JSON brut
            var raw = JsonConvert.DeserializeObject<Dictionary<string, BodyPartDescription>>(jsonFile.text);

            // 2) On reconstruit un dictionnaire en normalisant les clés
            _map = new Dictionary<string, BodyPartDescription>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in raw)
            {
                var key = kvp.Key.Trim(); // supprime espaces / retours à la ligne accidentels
                if (!_map.ContainsKey(key))
                {
                    _map[key] = kvp.Value;
                }
            }

            // Debug optionnel : log des clés chargées
            // foreach (var k in _map.Keys) Debug.Log("Desc key: [" + k + "]");
        }
        catch (Exception e)
        {
            Debug.LogError("Erreur lors du parse du JSON : " + e.Message);
            _map = new Dictionary<string, BodyPartDescription>();
        }
    }


    public bool TryGet(string id, out BodyPartDescription desc)
    {
        desc = null;
        if (_map == null) return false;
        if (string.IsNullOrEmpty(id)) return false;

        string key = id.Trim();  // enlève les espaces parasites

        return _map.TryGetValue(key, out desc);
    }

}
