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
    public ARAnchorManager anchorManager;
    public GameObject labelPrefab;
    public GameObject cubeOutlinePrefab;
    public Button scanButton;
    public RectTransform scanWindow;
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

            if (best != null && !string.IsNullOrEmpty(best.name) && best.confidence >= 0.55f)
            {
                DisplayBestDetection(best, conversionParams.outputDimensions.x, conversionParams.outputDimensions.y);
            }
        }
    }

    void DisplayBestDetection(DetectionResult det, int width, int height)
    {

        float centerX = (det.xmax + det.xmin) / 2f;
        float centerY = (det.ymax + det.ymin) / 2f;
        Vector2 imagePoint = new Vector2(centerX, centerY);

        float cropX = 0.25f;
        float cropY = 0.25f;
        float cropW = 0.5f;
        float cropH = 0.5f;

        float normX = imagePoint.x / width;
        float normY = imagePoint.y / height;
        float screenX = (cropX + normX * cropW) * Screen.width;
        float screenY = (1f - (cropY + normY * cropH)) * Screen.height;
        Vector2 screenPoint = new Vector2(screenX, screenY);

        Vector3 anchorPos;
        Quaternion anchorRot;
        float depth = 1.5f;

        List<ARRaycastHit> hits = new();
        bool hitDetected = raycastManager.Raycast(screenPoint, hits, TrackableType.Planes);

        if (hitDetected && hits.Count > 0)
        {
            ARRaycastHit selectedHit = hits[0];
            foreach (var hit in hits)
            {
                if (hit.trackable is ARPlane plane && plane.alignment == PlaneAlignment.HorizontalUp)
                {
                    selectedHit = hit;
                    break;
                }
            }
            anchorPos = selectedHit.pose.position;
            anchorRot = selectedHit.pose.rotation;
            depth = Vector3.Distance(Camera.main.transform.position, anchorPos);
        }
        else
        {
            Debug.LogWarning("[AR] No plane detected, using default depth.");
            anchorPos = Camera.main.ScreenPointToRay(screenPoint).GetPoint(depth);
            anchorRot = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
        }

        ARAnchor anchor = anchorManager.AddAnchor(new Pose(anchorPos, anchorRot));
        if (anchor == null)
        {
            Debug.LogWarning("[AR] Failed to create anchor.");
            return;
        }

        float bboxWidthNorm = (det.xmax - det.xmin) / width;
        float bboxHeightNorm = (det.ymax - det.ymin) / height;

        float worldWidth = Mathf.Abs(Camera.main.ViewportToWorldPoint(new Vector3(bboxWidthNorm, 0, depth)).x
                                   - Camera.main.ViewportToWorldPoint(new Vector3(0, 0, depth)).x);
        float worldHeight = Mathf.Abs(Camera.main.ViewportToWorldPoint(new Vector3(0, bboxHeightNorm, depth)).y
                                    - Camera.main.ViewportToWorldPoint(new Vector3(0, 0, depth)).y);

        GameObject cube = Instantiate(cubeOutlinePrefab, anchor.transform);
        cube.transform.localScale = new Vector3(worldWidth, 0.01f, worldHeight);
        cube.transform.localPosition = Vector3.zero;

        GameObject label = Instantiate(labelPrefab, anchor.transform);
        TextMeshPro textMesh = label.GetComponentInChildren<TextMeshPro>();
        if (textMesh != null)
        {
            string displayText = det.name;
            if (classDictionary.TryGetValue(displayText, out string translated))
                displayText = translated;
            float scaleFactor = depth * 0.2f;
            string capitalized = char.ToUpper(det.name[0]) + det.name.Substring(1).ToLower();
            textMesh.text = capitalized + " : " + displayText;
            textMesh.fontSize = Mathf.Clamp(scaleFactor * 10f, 0.2f, 5f);
            textMesh.alignment = TextAlignmentOptions.CenterGeoAligned;
        }

        label.transform.localPosition = new Vector3(0, worldHeight / 2f + 0.05f, 0);
        float labelScaleFactor = depth * 0.2f;
        label.transform.localScale = Vector3.one * labelScaleFactor;

        Vector3 directionToCamera = (Camera.main.transform.position - anchorPos).normalized;
        label.transform.rotation = Quaternion.LookRotation(directionToCamera, Vector3.up);
        label.transform.Rotate(0, 180f, 0);

        activeLabels.Add(anchor.gameObject);

        Debug.Log($"Anchor Rotation: {anchor.transform.rotation.eulerAngles}");
        Debug.Log($"Cube Rotation: {cube.transform.rotation.eulerAngles}");
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