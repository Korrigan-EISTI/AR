using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.IO;
using Unity.Collections;

public class ARClientTCP : MonoBehaviour
{
    public ARCameraManager cameraManager;
    public ARRaycastManager raycastManager;
    public GameObject labelPrefab;
    public Button scanButton;
    public string serverIP = "172.31.208.1";
    public int serverPort = 5001;

    private List<GameObject> activeLabels = new();
    private Dictionary<string, string> classDictionary;

    void Start()
    {
        InitializeClassDictionary();
        if (scanButton != null)
            scanButton.onClick.AddListener(() => StartCoroutine(DetectFromServer()));
    }

    void InitializeClassDictionary()
    {
        classDictionary = new Dictionary<string, string>
        {
            {"person", "Personne"}, {"bicycle", "Vélo"}, {"car", "Voiture"}, {"motorbike", "Moto"},
            {"aeroplane", "Avion"}, {"bus", "Bus"}, {"train", "Train"}, {"truck", "Camion"},
            {"boat", "Bateau"}, {"traffic light", "Feu tricolore"}, {"fire hydrant", "Bouche incendie"},
            {"stop sign", "Panneau stop"}, {"parking meter", "Parcmètre"}, {"bench", "Banc"},
            {"bird", "Oiseau"}, {"cat", "Chat"}, {"dog", "Chien"}, {"horse", "Cheval"},
            {"sheep", "Mouton"}, {"cow", "Vache"}, {"elephant", "Éléphant"}, {"bear", "Ours"},
            {"zebra", "Zèbre"}, {"giraffe", "Girafe"}, {"backpack", "Sac à dos"}, {"umbrella", "Parapluie"},
            {"handbag", "Sac à main"}, {"tie", "Cravate"}, {"suitcase", "Valise"}, {"frisbee", "Frisbee"},
            {"skis", "Skis"}, {"snowboard", "Snowboard"}, {"sports ball", "Balle de sport"},
            {"kite", "Cerf-volant"}, {"baseball bat", "Batte de baseball"}, {"baseball glove", "Gant de baseball"},
            {"skateboard", "Skateboard"}, {"surfboard", "Planche de surf"}, {"tennis racket", "Raquette de tennis"},
            {"bottle", "Bouteille"}, {"wine glass", "Verre à vin"}, {"cup", "Tasse"}, {"fork", "Fourchette"},
            {"knife", "Couteau"}, {"spoon", "Cuillère"}, {"bowl", "Bol"}, {"banana", "Banane"},
            {"apple", "Pomme"}, {"sandwich", "Sandwich"}, {"orange", "Orange"}, {"broccoli", "Brocoli"},
            {"carrot", "Carotte"}, {"hot dog", "Hot-dog"}, {"pizza", "Pizza"}, {"donut", "Donut"},
            {"cake", "Gâteau"}, {"chair", "Chaise"}, {"sofa", "Canapé"}, {"pottedplant", "Plante en pot"},
            {"bed", "Lit"}, {"dining table", "Table à manger"}, {"toilet", "Toilettes"},
            {"tvmonitor", "Télévision"}, {"laptop", "Ordinateur portable"}, {"mouse", "Souris"},
            {"remote", "Télécommande"}, {"keyboard", "Clavier"}, {"cell phone", "Téléphone"},
            {"microwave", "Micro-ondes"}, {"oven", "Four"}, {"toaster", "Grille-pain"},
            {"sink", "Évier"}, {"refrigerator", "Réfrigérateur"}, {"book", "Livre"}, {"clock", "Horloge"},
            {"vase", "Vase"}, {"scissors", "Ciseaux"}, {"teddy bear", "Ours en peluche"},
            {"hair drier", "Sèche-cheveux"}, {"toothbrush", "Brosse à dents"}
        };
    }

    IEnumerator DetectFromServer()
    {
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
        {
            Debug.LogWarning("[AR] Failed to acquire CPU image.");
            yield break;
        }

        using (cpuImage)
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(cpuImage.width / 4, cpuImage.height / 4, cpuImage.width / 2, cpuImage.height / 2),
                outputDimensions = new Vector2Int(cpuImage.width / 2, cpuImage.height / 2),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            var rawTexture = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, TextureFormat.RGBA32, false);
            var rawData = new NativeArray<byte>(cpuImage.GetConvertedDataSize(conversionParams), Allocator.Temp);
            cpuImage.Convert(conversionParams, rawData);
            rawTexture.LoadRawTextureData(rawData);
            rawTexture.Apply();
            rawData.Dispose();

            byte[] jpg = rawTexture.EncodeToJPG();
            Destroy(rawTexture);

            DetectionResult best = null;

            try
            {
                using (var client = new TcpClient(serverIP, serverPort))
                using (var stream = client.GetStream())
                {
                    stream.Write(jpg, 0, jpg.Length);
                    client.Client.Shutdown(SocketShutdown.Send);

                    using var ms = new MemoryStream();
                    byte[] buffer = new byte[4096];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        ms.Write(buffer, 0, read);

                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    Debug.Log("[TCP] JSON reçu : " + json);
                    best = JsonUtility.FromJson<DetectionResult>(json);
                }
            }
            catch (SocketException ex)
            {
                Debug.LogWarning("[TCP] Socket error: " + ex.Message);
                yield break;
            }

            if (best != null && !string.IsNullOrEmpty(best.name) && best.confidence >= 0.4f)
                DisplayBestDetection(best, conversionParams.outputDimensions.x, conversionParams.outputDimensions.y);
        }
    }

    void DisplayBestDetection(DetectionResult det, int width, int height)
    {
        ClearLabels();

        float centerX = (det.xmax + det.xmin) / 2f;
        float topY = det.ymin;
        Vector2 imagePoint = new Vector2(centerX, topY);

        float normX = imagePoint.x / width;
        float normY = 1f - (imagePoint.y / height);
        Vector2 viewportPoint = new Vector2(normX, normY);

        float depth = 1.5f;
        List<ARRaycastHit> hits = new();
        if (raycastManager.Raycast(new Vector2(viewportPoint.x * Screen.width, viewportPoint.y * Screen.height), hits, TrackableType.Planes))
        {
            Vector3 hitPos = hits[0].pose.position;
            Vector3 cameraPos = Camera.main.transform.position;
            depth = Vector3.Distance(cameraPos, hitPos);
            Debug.Log($"[AR] Plane detected at depth: {depth}");
        }
        else
        {
            Debug.LogWarning("[AR] No plane detected, using default depth: " + depth);
        }

        Vector3 worldPos = Camera.main.ViewportToWorldPoint(new Vector3(viewportPoint.x, viewportPoint.y, depth));

        float labelHeightOffset = 0.1f;
        float labelHorizontalOffset = 0.05f;
        worldPos += Camera.main.transform.up * labelHeightOffset;
        worldPos += Camera.main.transform.right * labelHorizontalOffset;

        GameObject label = Instantiate(labelPrefab, worldPos, Quaternion.identity);
        TextMeshPro textMesh = label.GetComponentInChildren<TextMeshPro>();
        if (textMesh != null)
        {
            string displayText = det.name;
            if (classDictionary.TryGetValue(displayText, out string translated))
            {
                displayText = translated;
            }
            textMesh.text = displayText;
            float scaleFactor = depth * 0.2f;
            textMesh.fontSize = Mathf.Clamp(scaleFactor * 10f, 0.2f, 5f);
            textMesh.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            Debug.LogWarning("[AR] TextMeshPro component not found in label prefab.");
        }

        float distance = Vector3.Distance(Camera.main.transform.position, worldPos);
        float labelScaleFactor = distance * 0.2f;
        label.transform.localScale = Vector3.one * labelScaleFactor;

        Vector3 directionToCamera = (Camera.main.transform.position - worldPos).normalized;
        label.transform.rotation = Quaternion.LookRotation(directionToCamera, Vector3.up);
        label.transform.Rotate(0, 180f, 0);

        activeLabels.Add(label);
    }

    void ClearLabels()
    {
        foreach (var label in activeLabels)
            Destroy(label);
        activeLabels.Clear();
    }

    [System.Serializable]
    public class DetectionResult
    {
        public float xmin;
        public float ymin;
        public float xmax;
        public float ymax;
        public float confidence;
        public string name;
    }
}
