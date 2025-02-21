using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.DnnModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.VideoioModule;  // VideoCapture用
using Microsoft.ML.OnnxRuntime;  // ONNX Runtime
using Microsoft.ML.OnnxRuntime.Tensors;  // DenseTensor

public class YoloV8OnnxWithOpenCVExample : MonoBehaviour
{
    [Header("UI")]
    public RawImage cameraView;    // Webカメラ映像のプレビュー用
    public Text detectionText;     // 検出結果をリスト表示

    [Header("Model Settings")]
    public string modelFileName = "yolov11_pool.onnx";
    [Tooltip("Must be in StreamingAssets folder or a valid path.")]
    public int inputWidth = 640;
    public int inputHeight = 640;
    public float confThreshold = 0.25f;
    public float nmsThreshold = 0.45f;

    [Header("Class Names (for YOLOv8 default ~80 classes)")]
    public string[] classLabels = new string[] {
        "person","bicycle","car","motorcycle","airplane","bus","train","truck","boat","traffic light",
        "fire hydrant","stop sign","parking meter","bench","bird","cat","dog","horse","sheep","cow",
        "elephant","bear","zebra","giraffe","backpack","umbrella","handbag","tie","suitcase","frisbee",
        "skis","snowboard","sports ball","kite","baseball bat","baseball glove","skateboard","surfboard","tennis racket","bottle",
        "wine glass","cup","fork","knife","spoon","bowl","banana","apple","sandwich","orange",
        "broccoli","carrot","hot dog","pizza","donut","cake","chair","couch","potted plant","bed",
        "dining table","toilet","tv","laptop","mouse","remote","keyboard","cell phone","microwave","oven",
        "toaster","sink","refrigerator","book","clock","vase","scissors","teddy bear","hair drier","toothbrush"
    };

    // 内部処理用
    private WebCamTexture webcamTexture;
    private Texture2D outputTexture;
    private InferenceSession session;
    private float[] inputData;
    private List<GameObject> boxOverlays = new List<GameObject>();

    // OpenCV Mats
    private Mat bgrMat;  // カメラフレーム(BGR)
    private Mat blobMat; // DNN入力用blob

    void Start()
    {
        // 1) Webカメラ起動
        var devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No webcam found.");
            return;
        }
        webcamTexture = new WebCamTexture("OBS Virtual Camera", 1280, 720, 30);
        webcamTexture.Play();

        cameraView.texture = webcamTexture;
        cameraView.material.mainTexture = webcamTexture;

        // 2) ONNXモデルをロード
        string modelPath = Path.Combine(Application.streamingAssetsPath, modelFileName);
        if (!File.Exists(modelPath))
        {
            Debug.LogError($"Model file not found at: {modelPath}");
            return;
        }
        var options = new SessionOptions();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try
        {
            options.AppendExecutionProvider("DML"); // Windows GPU
            Debug.Log("Using DirectML Execution Provider.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to enable DirectML: {e.Message}. Fallback to CPU.");
        }
#endif
        session = new InferenceSession(modelPath, options);
        Debug.Log($"ONNX model loaded: {modelPath}");

        // 3) 出力先テクスチャを生成
        outputTexture = new Texture2D(webcamTexture.width, webcamTexture.height, TextureFormat.RGBA32, false);

        // OpenCV mat 初期化
        bgrMat = new Mat(webcamTexture.height, webcamTexture.width, CvType.CV_8UC3);

        // UIチェック
        if (detectionText != null) detectionText.text = "Ready.";
    }

    void Update()
    {
        if (webcamTexture == null || !webcamTexture.didUpdateThisFrame) return;

        // 1) カメラ画像をOpenCV Matに取り込む (Color32[]→BGR Mat)
        Color32[] colors = webcamTexture.GetPixels32();
        Mat rgbaMat = new Mat(webcamTexture.height, webcamTexture.width, CvType.CV_8UC4);
        rgbaMat.put(0, 0, colors);
        Imgproc.cvtColor(rgbaMat, bgrMat, Imgproc.COLOR_RGBA2BGR);
        rgbaMat.Dispose();

        // 2) OpenCV DNN の blobFromImage 相当を実行 -> blobMat
        blobMat = Dnn.blobFromImage(
            bgrMat,                        // source Mat
            1.0 / 255.0,                  // scale factor
            new Size(inputWidth, inputHeight),  // resize
            new Scalar(0, 0, 0),            // mean
            true,                         // swapRB
            false                         // crop
        );

        // 3) blobMat の float[] データを取得して ONNX Runtime の DenseTensor<float> に詰める
        float[] blobData = new float[blobMat.total()];
        blobMat.get(0, 0, blobData);

        var inputTensor = new DenseTensor<float>(blobData, new int[] { 1, 3, inputHeight, inputWidth });

        // 4) 推論実行
        string inputName = session.InputMetadata.Keys.First();
        using var results = session.Run(new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor<float>(inputName, inputTensor)
        });
        float[] output = results.First().AsEnumerable<float>().ToArray();

        // 5) 後処理(NMS含む)
        int rowSize = 85;
        int numDetections = output.Length / rowSize;
        List<Detection> rawDetections = new List<Detection>();

        for (int i = 0; i < numDetections; i++)
        {
            int offset = i * rowSize;
            float xCenter = output[offset + 0];
            float yCenter = output[offset + 1];
            float w = output[offset + 2];
            float h = output[offset + 3];
            float conf = output[offset + 4];

            int bestClass = -1;
            float bestScore = 0f;
            for (int c = 0; c < classLabels.Length; c++)
            {
                float clsScore = output[offset + 5 + c];
                if (clsScore > bestScore)
                {
                    bestScore = clsScore;
                    bestClass = c;
                }
            }
            float finalScore = conf * bestScore;
            if (finalScore < confThreshold) continue;

            float x1 = xCenter - w / 2f;
            float y1 = yCenter - h / 2f;
            float x2 = xCenter + w / 2f;
            float y2 = yCenter + h / 2f;

            float scaleX = (float)bgrMat.width() / inputWidth;
            float scaleY = (float)bgrMat.height() / inputHeight;

            x1 *= scaleX;
            x2 *= scaleX;
            y1 *= scaleY;
            y2 *= scaleY;

            x1 = Mathf.Clamp(x1, 0, bgrMat.width() - 1);
            x2 = Mathf.Clamp(x2, 0, bgrMat.width() - 1);
            y1 = Mathf.Clamp(y1, 0, bgrMat.height() - 1);
            y2 = Mathf.Clamp(y2, 0, bgrMat.height() - 1);

            UnityEngine.Rect boxRect = new UnityEngine.Rect(x1, y1, x2 - x1, y2 - y1);
            rawDetections.Add(new Detection(bestClass, finalScore, boxRect));
        }

        // 6) NMS
        List<int> nmsIndices = new List<int>();
        List<float> scoresList = new List<float>();
        List<Rect2d> boxesList = new List<Rect2d>();
        for (int i = 0; i < rawDetections.Count; i++)
        {
            scoresList.Add(rawDetections[i].score);
            var r = rawDetections[i].box;
            boxesList.Add(new Rect2d(r.x, r.y, r.width, r.height));
        }
        MatOfRect2d boxesMat = new MatOfRect2d();
        boxesMat.fromList(boxesList);
        MatOfFloat scoresMat = new MatOfFloat();
        scoresMat.fromList(scoresList);
        MatOfInt indicesMat = new MatOfInt();
        Dnn.NMSBoxes(boxesMat, scoresMat, confThreshold, nmsThreshold, indicesMat);

        int[] indicesArray = indicesMat.toArray();
        List<Detection> finalDetections = new List<Detection>();
        foreach (int idx in indicesArray)
        {
            finalDetections.Add(rawDetections[idx]);
        }

        // 7) 表示 (UI Overlays + detectionText)
        ClearPreviousOverlays();
        if (finalDetections.Count > 0)
        {
            if (detectionText) detectionText.text = "";
        }
        else
        {
            if (detectionText) detectionText.text = "No detections";
        }

        // UIスケールの計算
        float uiScaleX = cameraView.rectTransform.rect.width / (float)webcamTexture.width;
        float uiScaleY = cameraView.rectTransform.rect.height / (float)webcamTexture.height;

        foreach (var det in finalDetections)
        {
            // テキストラベル
            string clsName = (det.classIndex >= 0 && det.classIndex < classLabels.Length) ?
                                classLabels[det.classIndex] : "Unknown";
            string label = $"{clsName} ({det.score * 100f:0.0}%)";

            // detectionTextに追加
            if (detectionText)
                detectionText.text += label + "\n";

            // バウンディングボックスのUI座標を計算
            float rx = det.box.x * uiScaleX;
            float ry = (webcamTexture.height - det.box.y - det.box.height) * uiScaleY; // Y座標を反転
            float rw = det.box.width * uiScaleX;
            float rh = det.box.height * uiScaleY;

            // バウンディングボックス表示（枠線のみ）
            GameObject boxObject = new GameObject("DetectionBox");
            boxObject.transform.SetParent(cameraView.transform, false);
            var image = boxObject.AddComponent<Image>();
            image.color = new Color(1, 0, 0, 0.5f); // 半透明赤
            image.type = Image.Type.Sliced; // 枠線を表示するためにSlicedを使用
            image.fillCenter = false; // 中央を塗りつぶさない

            // RectTransformの設定
            RectTransform rt = image.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(rx, ry);
            rt.sizeDelta = new Vector2(rw, rh);

            // ラベルText
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(boxObject.transform, false);
            var textComp = labelObject.AddComponent<Text>();
            textComp.text = label;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = 16;
            textComp.color = Color.white;
            textComp.alignment = TextAnchor.UpperLeft;

            // ラベルテキストのRectTransform設定
            RectTransform labelRt = textComp.rectTransform;
            labelRt.anchorMin = new Vector2(0, 1);
            labelRt.anchorMax = new Vector2(0, 1);
            labelRt.pivot = new Vector2(0, 1);
            labelRt.anchoredPosition = new Vector2(0, 20); // バウンディングボックスの上に配置
            labelRt.sizeDelta = new Vector2(200, 20); // 適切なサイズに設定

            boxOverlays.Add(boxObject);
        }
    }

    /// <summary>
    /// フレームごとに生成したUIを破棄
    /// </summary>
    private void ClearPreviousOverlays()
    {
        foreach (var o in boxOverlays)
        {
            Destroy(o);
        }
        boxOverlays.Clear();
    }

    void OnDestroy()
    {
        if (webcamTexture != null) webcamTexture.Stop();
        if (session != null) session.Dispose();
        if (bgrMat != null) bgrMat.Dispose();
        if (blobMat != null) blobMat.Dispose();
    }

    /// <summary>
    /// 検出情報構造体
    /// </summary>
    private struct Detection
    {
        public int classIndex;
        public float score;
        public UnityEngine.Rect box;
        public Detection(int ci, float sc, UnityEngine.Rect b) { classIndex = ci; score = sc; box = b; }
    }
}