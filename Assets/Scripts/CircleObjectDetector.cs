using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

/// <summary>
/// NDI映像内の円形領域（WhiteCircle）に入った物体を検出するコンポーネント
/// </summary>
public class CircleObjectDetector : MonoBehaviour
{
    [Header("検出設定")]
    [Tooltip("NDIレシーバーコンポーネント")]
    public NDIReceiver ndiReceiver;

    [Tooltip("検出間隔（秒）")]
    public float detectionInterval = 0.1f;

    [Header("検出パラメータ")]
    [Tooltip("検出する色相の範囲（最小値）")]
    [Range(0f, 1f)]
    public float hueMin = 0f;

    [Tooltip("検出する色相の範囲（最大値）")]
    [Range(0f, 1f)]
    public float hueMax = 1f;

    [Tooltip("検出する彩度の範囲（最小値）")]
    [Range(0f, 1f)]
    public float saturationMin = 0.5f;

    [Tooltip("検出する彩度の範囲（最大値）")]
    [Range(0f, 1f)]
    public float saturationMax = 1f;

    [Tooltip("検出する明度の範囲（最小値）")]
    [Range(0f, 1f)]
    public float valueMin = 0.5f;

    [Tooltip("検出する明度の範囲（最大値）")]
    [Range(0f, 1f)]
    public float valueMax = 1f;

    [Tooltip("検出する物体の最小サイズ（ピクセル数）")]
    public int minObjectSize = 100;

    [Header("デバッグ")]
    [Tooltip("デバッグ表示を有効にする")]
    public bool showDebug = true;

    [Tooltip("検出結果を表示するテクスチャ")]
    public RenderTexture debugTexture;

    // イベント
    [System.Serializable]
    public class ObjectDetectedEvent : UnityEvent<Vector2, float> { }

    [Header("イベント")]
    [Tooltip("物体が検出されたときに発火するイベント（位置、サイズ）")]
    public ObjectDetectedEvent onObjectDetected = new ObjectDetectedEvent();

    // 内部変数
    private float _lastDetectionTime;
    private Texture2D _sourceTexture;
    private Texture2D _debugTexture;
    private GameObject _circleObject;
    private RectTransform _circleRectTransform;
    private Vector2 _circleCenter;
    private float _circleRadius;
    private bool _initialized = false;
    private ComputeBuffer _pixelBuffer;
    private ComputeShader _computeShader;
    private bool _circleDetectionLogged = false;
    private bool _objectDetectionLogged = false;

    void Start()
    {
        InitializeDetector();
    }

    void Update()
    {
        if (!_initialized)
        {
            InitializeDetector();
            return;
        }

        // 検出間隔に基づいて検出を実行
        if (Time.time - _lastDetectionTime >= detectionInterval)
        {
            DetectObjectsInCircle();
            _lastDetectionTime = Time.time;
        }
    }

    void OnDestroy()
    {
        CleanupResources();
    }

    /// <summary>
    /// 検出器の初期化
    /// </summary>
    private void InitializeDetector()
    {
        // WhiteCircleオブジェクトを自動検索
        _circleObject = GameObject.Find("WhiteCircle");
        if (_circleObject == null)
        {
            // タグで検索を試みる
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag("WhiteCircle");
            if (taggedObjects.Length > 0)
            {
                _circleObject = taggedObjects[0];
            }
            
            // 名前に "Circle" を含むオブジェクトを検索
            if (_circleObject == null)
            {
                GameObject[] allObjects = FindObjectsOfType<GameObject>();
                foreach (GameObject obj in allObjects)
                {
                    if (obj.name.Contains("Circle") || obj.name.Contains("circle"))
                    {
                        _circleObject = obj;
                        break;
                    }
                }
            }
            
            if (_circleObject == null)
            {
                Debug.LogError("CircleObjectDetector: 'WhiteCircle'または円形オブジェクトが見つかりません");
                return;
            }
        }
        
        if (!_circleDetectionLogged)
        {
            Debug.Log($"CircleObjectDetector: '{_circleObject.name}'オブジェクトを自動検出しました");
            _circleDetectionLogged = true;
        }

        if (ndiReceiver == null)
        {
            // NDIReceiverを自動検索
            ndiReceiver = FindObjectOfType<NDIReceiver>();
            if (ndiReceiver == null)
            {
                Debug.LogError("CircleObjectDetector: NDIReceiverが見つかりません");
                return;
            }
            else if (!_circleDetectionLogged)
            {
                Debug.Log("CircleObjectDetector: NDIReceiverを自動検出しました");
            }
        }

        // 円形オブジェクトのRectTransformを取得
        _circleRectTransform = _circleObject.GetComponent<RectTransform>();
        if (_circleRectTransform == null)
        {
            // RectTransformがない場合はTransformを使用
            Transform transform = _circleObject.transform;
            _circleCenter = transform.position;
            
            // スケールから半径を推定
            _circleRadius = Mathf.Max(transform.localScale.x, transform.localScale.y) / 2f;
            
            if (!_circleDetectionLogged)
            {
                Debug.Log($"CircleObjectDetector: Transformから円情報を取得 - 中心: {_circleCenter}, 半径: {_circleRadius}");
            }
        }
        else
        {
            // 円の中心と半径を計算
            _circleCenter = _circleRectTransform.position;
            _circleRadius = Mathf.Min(_circleRectTransform.rect.width, _circleRectTransform.rect.height) / 2f;
            if (!_circleDetectionLogged)
            {
                Debug.Log($"CircleObjectDetector: RectTransformから円情報を取得 - 中心: {_circleCenter}, 半径: {_circleRadius}");
            }
        }

        // デバッグテクスチャの初期化
        if (showDebug && debugTexture == null)
        {
            debugTexture = new RenderTexture(ndiReceiver.resolution.x, ndiReceiver.resolution.y, 0);
            debugTexture.Create();
        }

        // ソーステクスチャの初期化
        _sourceTexture = new Texture2D(ndiReceiver.resolution.x, ndiReceiver.resolution.y, TextureFormat.RGBA32, false);

        // デバッグ用テクスチャの初期化
        if (showDebug)
        {
            _debugTexture = new Texture2D(ndiReceiver.resolution.x, ndiReceiver.resolution.y, TextureFormat.RGBA32, false);
        }

        _initialized = true;
        if (!_circleDetectionLogged)
        {
            Debug.Log("CircleObjectDetector: 初期化完了");
        }
    }

    /// <summary>
    /// 円内の物体を検出
    /// </summary>
    private void DetectObjectsInCircle()
    {
        if (ndiReceiver == null || !_initialized)
            return;

        // NDIレシーバーからテクスチャを取得
        RenderTexture ndiTexture = ndiReceiver.GetTargetTexture();
        if (ndiTexture == null)
            return;

        // テクスチャからピクセルデータを読み取り
        RenderTexture.active = ndiTexture;
        _sourceTexture.ReadPixels(new Rect(0, 0, ndiTexture.width, ndiTexture.height), 0, 0);
        _sourceTexture.Apply();
        RenderTexture.active = null;

        // 円内のピクセルを分析
        List<Vector2> objectPositions = AnalyzePixelsInCircle(_sourceTexture);

        // 検出結果の処理
        if (objectPositions.Count > 0)
        {
            // 最大の物体を選択（この部分は要件に応じて変更可能）
            Vector2 largestObjectPosition = objectPositions[0];
            float objectSize = CalculateObjectSize(_sourceTexture, largestObjectPosition);

            // イベント発火
            onObjectDetected.Invoke(largestObjectPosition, objectSize);

            if (showDebug && !_objectDetectionLogged)
            {
                Debug.Log($"CircleObjectDetector: 物体を検出 - 位置: {largestObjectPosition}, サイズ: {objectSize}");
                _objectDetectionLogged = true;
            }
        }
    }

    /// <summary>
    /// 円内のピクセルを分析して物体の位置を検出
    /// </summary>
    private List<Vector2> AnalyzePixelsInCircle(Texture2D texture)
    {
        List<Vector2> objectPositions = new List<Vector2>();
        Color[] pixels = texture.GetPixels();
        int width = texture.width;
        int height = texture.height;

        // 円内のピクセルをフィルタリング
        List<Vector2> filteredPixels = new List<Vector2>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // テクスチャ座標をワールド座標に変換
                Vector2 pixelPos = new Vector2(x, y);
                Vector2 normalizedPos = new Vector2((float)x / width, (float)y / height);
                Vector2 worldPos = Camera.main.ViewportToWorldPoint(normalizedPos);

                // 円内かどうかをチェック
                if (Vector2.Distance(worldPos, _circleCenter) <= _circleRadius)
                {
                    // ピクセルの色を取得
                    Color pixelColor = pixels[y * width + x];
                    
                    // HSV色空間に変換
                    Color.RGBToHSV(pixelColor, out float h, out float s, out float v);
                    
                    // 指定された色範囲内かチェック
                    bool hueInRange = (hueMin <= hueMax) ? 
                        (h >= hueMin && h <= hueMax) : 
                        (h >= hueMin || h <= hueMax);
                    
                    if (hueInRange && 
                        s >= saturationMin && s <= saturationMax && 
                        v >= valueMin && v <= valueMax)
                    {
                        filteredPixels.Add(pixelPos);
                    }
                }
            }
        }

        // フィルタリングされたピクセルから物体を検出
        if (filteredPixels.Count > 0)
        {
            // 簡易的なクラスタリング（実際のアプリケーションではより高度なアルゴリズムを使用）
            Vector2 averagePos = Vector2.zero;
            foreach (Vector2 pos in filteredPixels)
            {
                averagePos += pos;
            }
            averagePos /= filteredPixels.Count;
            
            // 最小サイズ以上の場合のみ検出とみなす
            if (filteredPixels.Count >= minObjectSize)
            {
                objectPositions.Add(averagePos);
                
                // デバッグ表示（最初の検出時のみ）
                if (showDebug && _debugTexture != null && !_objectDetectionLogged)
                {
                    DrawDebugVisualization(texture, filteredPixels, averagePos);
                }
            }
        }

        return objectPositions;
    }

    /// <summary>
    /// 物体のサイズを計算
    /// </summary>
    private float CalculateObjectSize(Texture2D texture, Vector2 position)
    {
        // 簡易的なサイズ計算（実際のアプリケーションではより高度な方法を使用）
        Color[] pixels = texture.GetPixels();
        int width = texture.width;
        int height = texture.height;
        int count = 0;
        int maxRadius = Mathf.Min(width, height) / 4; // 最大探索半径

        for (int r = 1; r <= maxRadius; r++)
        {
            bool foundBoundary = false;
            
            // 円周上のピクセルをチェック
            for (int angle = 0; angle < 360; angle += 10)
            {
                int x = Mathf.RoundToInt(position.x + r * Mathf.Cos(angle * Mathf.Deg2Rad));
                int y = Mathf.RoundToInt(position.y + r * Mathf.Sin(angle * Mathf.Deg2Rad));
                
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    Color pixelColor = pixels[y * width + x];
                    Color.RGBToHSV(pixelColor, out float h, out float s, out float v);
                    
                    bool hueInRange = (hueMin <= hueMax) ? 
                        (h >= hueMin && h <= hueMax) : 
                        (h >= hueMin || h <= hueMax);
                    
                    if (!(hueInRange && 
                          s >= saturationMin && s <= saturationMax && 
                          v >= valueMin && v <= valueMax))
                    {
                        foundBoundary = true;
                        break;
                    }
                }
            }
            
            if (foundBoundary)
            {
                return r;
            }
        }
        
        return maxRadius;
    }

    /// <summary>
    /// デバッグ表示を描画
    /// </summary>
    private void DrawDebugVisualization(Texture2D sourceTexture, List<Vector2> filteredPixels, Vector2 objectCenter)
    {
        if (!showDebug || debugTexture == null)
            return;

        // ソーステクスチャをコピー
        Color[] pixels = sourceTexture.GetPixels();
        _debugTexture.SetPixels(pixels);
        
        // 検出されたピクセルをハイライト
        foreach (Vector2 pos in filteredPixels)
        {
            int x = Mathf.RoundToInt(pos.x);
            int y = Mathf.RoundToInt(pos.y);
            if (x >= 0 && x < _debugTexture.width && y >= 0 && y < _debugTexture.height)
            {
                _debugTexture.SetPixel(x, y, Color.green);
            }
        }
        
        // 物体の中心を赤い十字で表示
        int centerX = Mathf.RoundToInt(objectCenter.x);
        int centerY = Mathf.RoundToInt(objectCenter.y);
        int crossSize = 10;
        
        for (int i = -crossSize; i <= crossSize; i++)
        {
            int x = centerX + i;
            int y = centerY;
            if (x >= 0 && x < _debugTexture.width && y >= 0 && y < _debugTexture.height)
            {
                _debugTexture.SetPixel(x, y, Color.red);
            }
            
            x = centerX;
            y = centerY + i;
            if (x >= 0 && x < _debugTexture.width && y >= 0 && y < _debugTexture.height)
            {
                _debugTexture.SetPixel(x, y, Color.red);
            }
        }
        
        _debugTexture.Apply();
        
        // デバッグテクスチャに適用
        Graphics.Blit(_debugTexture, debugTexture);
    }

    /// <summary>
    /// リソースのクリーンアップ
    /// </summary>
    private void CleanupResources()
    {
        if (_sourceTexture != null)
        {
            Destroy(_sourceTexture);
        }
        
        if (_debugTexture != null)
        {
            Destroy(_debugTexture);
        }
        
        if (_pixelBuffer != null)
        {
            _pixelBuffer.Release();
            _pixelBuffer = null;
        }
    }

    /// <summary>
    /// 検出パラメータを動的に更新
    /// </summary>
    public void UpdateDetectionParameters(float newHueMin, float newHueMax, float newSatMin, float newSatMax, float newValMin, float newValMax, int newMinSize)
    {
        hueMin = Mathf.Clamp01(newHueMin);
        hueMax = Mathf.Clamp01(newHueMax);
        saturationMin = Mathf.Clamp01(newSatMin);
        saturationMax = Mathf.Clamp01(newSatMax);
        valueMin = Mathf.Clamp01(newValMin);
        valueMax = Mathf.Clamp01(newValMax);
        minObjectSize = Mathf.Max(1, newMinSize);
        
        // パラメータ更新時にログフラグをリセット
        _objectDetectionLogged = false;
        
        Debug.Log($"CircleObjectDetector: 検出パラメータを更新 - H:[{hueMin}-{hueMax}], S:[{saturationMin}-{saturationMax}], V:[{valueMin}-{valueMax}], MinSize:{minObjectSize}");
    }
    
    /// <summary>
    /// デバッグログのリセット
    /// </summary>
    public void ResetDebugLogs()
    {
        _objectDetectionLogged = false;
        Debug.Log("CircleObjectDetector: デバッグログをリセットしました");
    }
} 