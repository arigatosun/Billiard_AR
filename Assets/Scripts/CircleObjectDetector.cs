using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

/// <summary>
/// NDI映像内の円形領域（WhiteCircle）に入った棒状の物体を検出するコンポーネント
/// </summary>
public class CircleObjectDetector : MonoBehaviour
{
    [Header("検出設定")]
    [Tooltip("NDIレシーバーコンポーネント")]
    public NDIReceiver ndiReceiver;

    [Tooltip("検出間隔（秒）")]
    public float detectionInterval = 0.1f;

    [Header("棒状オブジェクト検出設定")]
    [Tooltip("棒状と判定する最小の縦横比")]
    [Range(1.5f, 10f)]
    public float minAspectRatio = 3.0f; // 棒状の判定基準（縦横比）

    [Tooltip("検出する物体の最小サイズ（ピクセル数）")]
    public int minObjectSize = 50; // 棒状のものは小さくても検出できるように調整

    [Tooltip("棒の向きを検出する精度（角度）")]
    [Range(1f, 45f)]
    public float orientationPrecision = 5f;

    [Header("表示設定")]
    [Tooltip("サイズ目安の表示")]
    public bool showSizeGuide = true;

    [Tooltip("サイズ目安の色")]
    public Color guideColor = Color.yellow;

    [Tooltip("サイズ目安の太さ")]
    [Range(1, 10)]
    public int guideThickness = 2;

    [Header("デバッグ")]
    [Tooltip("デバッグ表示を有効にする")]
    public bool showDebug = true;

    [Tooltip("検出結果を表示するテクスチャ")]
    public RenderTexture debugTexture;

    // イベント
    [System.Serializable]
    public class StickDetectedEvent : UnityEvent<Vector2, float, float> { } // 位置、長さ、角度

    [Header("イベント")]
    [Tooltip("棒状の物体が検出されたときに発火するイベント（位置、長さ、角度）")]
    public StickDetectedEvent onStickDetected = new StickDetectedEvent();

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

    // 色検出用のデフォルトHSV値（内部で使用）
    private float hueMin = 0.5f;
    private float hueMax = 0.7f;
    private float saturationMin = 0.4f;
    private float saturationMax = 1f;
    private float valueMin = 0.3f;
    private float valueMax = 1f;

    // 棒状オブジェクトの情報
    private struct StickInfo
    {
        public Vector2 position;  // 中心位置
        public float length;      // 長さ
        public float angle;       // 角度（ラジアン）
        public float aspectRatio; // 縦横比
    }

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
            DetectSticksInCircle();
            _lastDetectionTime = Time.time;
        }
    }

    void OnGUI()
    {
        if (showSizeGuide && _initialized && _circleObject != null)
        {
            // 画面上にサイズ目安を表示
            DrawSizeGuideOnScreen();
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
    /// 画面上にサイズ目安を表示
    /// </summary>
    private void DrawSizeGuideOnScreen()
    {
        if (Camera.main == null) return;

        // 円の中心位置をスクリーン座標に変換
        Vector3 screenCenter = Camera.main.WorldToScreenPoint(_circleCenter);
        
        // 円の半径をスクリーン座標に変換
        float screenRadius = _circleRadius * Screen.height / 10f; // 適当なスケール係数
        
        // 棒の長さと幅の目安を計算
        float stickLength = screenRadius * 0.8f; // 円の80%の長さ
        float stickWidth = stickLength / minAspectRatio; // 縦横比から幅を計算
        
        // 棒の目安を描画
        Color originalColor = GUI.color;
        GUI.color = guideColor;
        
        // 中心位置
        float centerX = screenCenter.x;
        float centerY = Screen.height - screenCenter.y; // GUIの座標系はY軸が反転している
        
        // 棒の長さ方向の線
        float halfLength = stickLength / 2;
        DrawGuiLine(
            new Vector2(centerX, centerY - halfLength), 
            new Vector2(centerX, centerY + halfLength), 
            guideThickness
        );
        
        // 棒の幅方向の線
        float halfWidth = stickWidth / 2;
        DrawGuiLine(
            new Vector2(centerX - halfWidth, centerY), 
            new Vector2(centerX + halfWidth, centerY), 
            guideThickness
        );
        
        // サイズ情報のテキスト表示
        string sizeText = $"最小サイズ: {minObjectSize}px\n最小縦横比: {minAspectRatio:F1}";
        GUI.Label(new Rect(centerX + halfWidth + 10, centerY - 30, 200, 60), sizeText);
        
        GUI.color = originalColor;
    }
    
    /// <summary>
    /// GUI上に線を描画
    /// </summary>
    private void DrawGuiLine(Vector2 start, Vector2 end, float thickness)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * thickness;
        
        Vector3[] points = new Vector3[4];
        points[0] = start + perpendicular;
        points[1] = end + perpendicular;
        points[2] = end - perpendicular;
        points[3] = start - perpendicular;
        
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, guideColor);
        texture.Apply();
        
        GUI.skin.box.normal.background = texture;
        GUI.skin.box.border = new RectOffset(0, 0, 0, 0);
        
        Matrix4x4 matrixBackup = GUI.matrix;
        
        GUIUtility.RotateAroundPivot(
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
            start
        );
        
        GUI.Box(new Rect(
            start.x, 
            start.y - thickness, 
            Vector2.Distance(start, end), 
            thickness * 2
        ), GUIContent.none);
        
        GUI.matrix = matrixBackup;
    }

    /// <summary>
    /// 円内の棒状物体を検出
    /// </summary>
    private void DetectSticksInCircle()
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

        // 円内のピクセルを分析して棒状物体を検出
        StickInfo stickInfo = AnalyzePixelsForStick(_sourceTexture);

        // 棒状物体が検出された場合
        if (stickInfo.length > 0 && stickInfo.aspectRatio >= minAspectRatio)
        {
            // イベント発火
            onStickDetected.Invoke(stickInfo.position, stickInfo.length, stickInfo.angle * Mathf.Rad2Deg);

            if (showDebug && !_objectDetectionLogged)
            {
                Debug.Log($"CircleObjectDetector: 棒状物体を検出 - 位置: {stickInfo.position}, 長さ: {stickInfo.length}, 角度: {stickInfo.angle * Mathf.Rad2Deg}度, 縦横比: {stickInfo.aspectRatio}");
                _objectDetectionLogged = true;
            }
        }
    }

    /// <summary>
    /// 円内のピクセルを分析して棒状物体の情報を取得
    /// </summary>
    private StickInfo AnalyzePixelsForStick(Texture2D texture)
    {
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

        // 棒状物体の情報を初期化
        StickInfo stickInfo = new StickInfo();
        stickInfo.length = 0;
        stickInfo.angle = 0;
        stickInfo.aspectRatio = 1;

        // フィルタリングされたピクセルから棒状物体を検出
        if (filteredPixels.Count >= minObjectSize)
        {
            // 中心位置を計算
            Vector2 center = Vector2.zero;
            foreach (Vector2 pos in filteredPixels)
            {
                center += pos;
            }
            center /= filteredPixels.Count;
            stickInfo.position = center;

            // 主成分分析で棒の向きを検出
            float xx = 0, xy = 0, yy = 0;
            foreach (Vector2 pos in filteredPixels)
            {
                Vector2 diff = pos - center;
                xx += diff.x * diff.x;
                xy += diff.x * diff.y;
                yy += diff.y * diff.y;
            }

            // 共分散行列の固有値と固有ベクトルを計算
            float det = xx * yy - xy * xy;
            float trace = xx + yy;
            float lambda1 = (trace + Mathf.Sqrt(trace * trace - 4 * det)) / 2;
            float lambda2 = (trace - Mathf.Sqrt(trace * trace - 4 * det)) / 2;
            
            // 縦横比を計算
            stickInfo.aspectRatio = lambda1 > lambda2 ? 
                Mathf.Sqrt(lambda1 / Mathf.Max(0.001f, lambda2)) : 
                Mathf.Sqrt(lambda2 / Mathf.Max(0.001f, lambda1));

            // 棒の向きを計算
            if (lambda1 > lambda2)
            {
                stickInfo.angle = Mathf.Atan2(lambda1 - xx, xy);
            }
            else
            {
                stickInfo.angle = Mathf.Atan2(xy, lambda2 - yy);
            }

            // 棒の長さを計算（主軸方向の最大距離）
            float maxDist = 0;
            Vector2 direction = new Vector2(Mathf.Cos(stickInfo.angle), Mathf.Sin(stickInfo.angle));
            foreach (Vector2 pos in filteredPixels)
            {
                Vector2 diff = pos - center;
                float dist = Mathf.Abs(Vector2.Dot(diff, direction));
                maxDist = Mathf.Max(maxDist, dist);
            }
            stickInfo.length = maxDist * 2; // 中心からの距離なので2倍

            // デバッグ表示（最初の検出時のみ）
            if (showDebug && _debugTexture != null && !_objectDetectionLogged)
            {
                DrawStickDebugVisualization(texture, filteredPixels, stickInfo);
            }
        }

        return stickInfo;
    }

    /// <summary>
    /// 棒状物体のデバッグ表示を描画
    /// </summary>
    private void DrawStickDebugVisualization(Texture2D sourceTexture, List<Vector2> filteredPixels, StickInfo stickInfo)
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
        
        // 棒の中心を赤い十字で表示
        int centerX = Mathf.RoundToInt(stickInfo.position.x);
        int centerY = Mathf.RoundToInt(stickInfo.position.y);
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
        
        // 棒の向きを線で表示
        float halfLength = stickInfo.length / 2;
        Vector2 direction = new Vector2(Mathf.Cos(stickInfo.angle), Mathf.Sin(stickInfo.angle));
        Vector2 start = stickInfo.position - direction * halfLength;
        Vector2 end = stickInfo.position + direction * halfLength;
        
        // 線を描画
        DrawLine(_debugTexture, start, end, Color.yellow, 2);
        
        _debugTexture.Apply();
        
        // デバッグテクスチャに適用
        Graphics.Blit(_debugTexture, debugTexture);
    }

    /// <summary>
    /// テクスチャに線を描画
    /// </summary>
    private void DrawLine(Texture2D texture, Vector2 start, Vector2 end, Color color, int thickness = 1)
    {
        int x0 = Mathf.RoundToInt(start.x);
        int y0 = Mathf.RoundToInt(start.y);
        int x1 = Mathf.RoundToInt(end.x);
        int y1 = Mathf.RoundToInt(end.y);
        
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        
        while (true)
        {
            // 太さを考慮して周囲のピクセルも塗る
            for (int tx = -thickness/2; tx <= thickness/2; tx++)
            {
                for (int ty = -thickness/2; ty <= thickness/2; ty++)
                {
                    int px = x0 + tx;
                    int py = y0 + ty;
                    if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                    {
                        texture.SetPixel(px, py, color);
                    }
                }
            }
            
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
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
    public void UpdateDetectionParameters(int newMinSize, float newMinAspectRatio)
    {
        minObjectSize = Mathf.Max(1, newMinSize);
        minAspectRatio = Mathf.Max(1.5f, newMinAspectRatio);
        
        // パラメータ更新時にログフラグをリセット
        _objectDetectionLogged = false;
        
        Debug.Log($"CircleObjectDetector: 検出パラメータを更新 - MinSize:{minObjectSize}, MinAspectRatio:{minAspectRatio}");
    }
    
    /// <summary>
    /// 色検出パラメータを設定（必要な場合のみ使用）
    /// </summary>
    public void SetColorParameters(float newHueMin, float newHueMax, float newSatMin, float newSatMax, float newValMin, float newValMax)
    {
        hueMin = Mathf.Clamp01(newHueMin);
        hueMax = Mathf.Clamp01(newHueMax);
        saturationMin = Mathf.Clamp01(newSatMin);
        saturationMax = Mathf.Clamp01(newSatMax);
        valueMin = Mathf.Clamp01(newValMin);
        valueMax = Mathf.Clamp01(newValMax);
        
        // パラメータ更新時にログフラグをリセット
        _objectDetectionLogged = false;
        
        Debug.Log($"CircleObjectDetector: 色検出パラメータを更新 - H:[{hueMin}-{hueMax}], S:[{saturationMin}-{saturationMax}], V:[{valueMin}-{valueMax}]");
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