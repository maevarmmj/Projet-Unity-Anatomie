using UnityEngine;
using UnityEngine.UI;

namespace UI.Script
{
    public class CameraBg : MonoBehaviour
    {
        WebCamTexture _webcamTexture;
        private RawImage _rawImage;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _rawImage = GetComponent<RawImage>();
            _webcamTexture = new WebCamTexture();
            _webcamTexture.Play();
            _rawImage.texture = _webcamTexture;
        }
        
        void OnDisable()
        {
            if (_webcamTexture != null && _webcamTexture.isPlaying)
            {
                _webcamTexture.Stop();
            }
        }
    }
}
