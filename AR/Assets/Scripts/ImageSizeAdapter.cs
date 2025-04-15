using System.Drawing;
using UnityEngine;
using UnityEngine.UI;

public class ScanZoneOverlay : MonoBehaviour
{
    void Start()
    {
        RectTransform scanZone = gameObject.GetComponent<RectTransform>(); 

        // Taille = 50% de l'écran
        float width = Screen.width * 0.5f;
        float height = Screen.height * 0.5f;

        scanZone.sizeDelta = new Vector2(width, height);
        scanZone.anchoredPosition = Vector2.zero;
        scanZone.anchorMin = new Vector2(0.5f, 0.5f);
        scanZone.anchorMax = new Vector2(0.5f, 0.5f);
        scanZone.pivot = new Vector2(0.5f, 0.5f);
    }
}
