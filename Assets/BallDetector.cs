using UnityEngine;
using UnityEngine.UI;

// OpenCVForUnity
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;

using System.Collections.Generic;

[RequireComponent(typeof(MultiSource2MatHelper))]
public class IntegratedBallDetectorExample : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("カメラ映像を表示する RawImage")]
    public RawImage previewImage;

    [Tooltip("アスペクト比を自動調整する場合アタッチ")]
    public AspectRatioFitter aspectFitter;

    [Header("Camera Settings")]
    [SerializeField] private int requestWidth = 1280;
    [SerializeField] private int requestHeight = 720;
    [SerializeField] private int requestFps = 30;
    [SerializeField] private bool requestIsFrontFacing = false;

    [Header("HoughCircles Params")]
    [Range(1, 300)]
    public int param1 = 100; // 内部のCanny上限閾値
    [Range(1, 100)]
    public int param2 = 50;  // 円と判定する累積投票のしきい値
    [Range(1, 100)]
    public int minRadius = 10;
    [Range(1, 200)]
    public int maxRadius = 100;

    [Header("Performance Settings")]
    [Tooltip("何フレームごとに円検出を行うか")]
    public int skipFrames = 5;

    [Tooltip("検出結果をログに出すか")]
    public bool logDetection = true;

    // Helper本体
    private MultiSource2MatHelper multiSource2MatHelper;

    // カメラ映像を転送するテクスチャ
    private Texture2D texture;

    // 検出結果を保持するための簡易構造体
    private struct CircleData
    {
        public float cx;
        public float cy;
        public float r;
        public CircleData(float cx, float cy, float r)
        {
            this.cx = cx;
            this.cy = cy;
            this.r = r;
        }
    }
    // 前回検出した円のリスト
    private List<CircleData> lastCircles = new List<CircleData>();

    void Start()
    {
        // MultiSource2MatHelper の取得
        multiSource2MatHelper = GetComponent<MultiSource2MatHelper>();

        // カメラ解像度・FPS・前面/背面カメラ等のリクエスト設定
        multiSource2MatHelper.requestedWidth = requestWidth;
        multiSource2MatHelper.requestedHeight = requestHeight;
        multiSource2MatHelper.requestedFPS = requestFps;
        multiSource2MatHelper.requestedIsFrontFacing = requestIsFrontFacing;

        // 必要に応じて出力フォーマット等を調整
        // multiSource2MatHelper.outputColorFormat = Source2MatHelperColorFormat.RGBA;

        // イベントの登録
        multiSource2MatHelper.onInitialized.AddListener(OnSourceToMatHelperInitialized);
        multiSource2MatHelper.onDisposed.AddListener(OnSourceToMatHelperDisposed);
        multiSource2MatHelper.onErrorOccurred.AddListener(OnSourceToMatHelperErrorOccurred);

        // 初期化開始
        multiSource2MatHelper.Initialize();
    }

    /// <summary>
    /// カメラ初期化完了時
    /// </summary>
    public void OnSourceToMatHelperInitialized()
    {
        Debug.Log("OnSourceToMatHelperInitialized");

        // 初期フレーム取得
        Mat rgbaMat = multiSource2MatHelper.GetMat();
        if (rgbaMat == null || rgbaMat.empty())
        {
            Debug.LogWarning("rgbaMat is null or empty.");
            return;
        }

        // テクスチャ生成
        texture = new Texture2D(rgbaMat.cols(), rgbaMat.rows(), TextureFormat.RGBA32, false);

        // RawImage にアサイン
        if (previewImage != null)
        {
            previewImage.texture = texture;

            if (aspectFitter != null)
            {
                aspectFitter.aspectRatio = (float)rgbaMat.width() / rgbaMat.height();
            }
        }

        // 必要ならここで Play() を開始
        // multiSource2MatHelper.Play();
    }

    /// <summary>
    /// カメラがDisposedされたとき
    /// </summary>
    public void OnSourceToMatHelperDisposed()
    {
        Debug.Log("OnSourceToMatHelperDisposed");

        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }

        // lastCircles は消してもよい
        lastCircles.Clear();
    }

    /// <summary>
    /// カメラ利用時にエラーが発生
    /// </summary>
    public void OnSourceToMatHelperErrorOccurred(Source2MatHelperErrorCode errorCode, string message)
    {
        Debug.LogError($"OnSourceToMatHelperErrorOccurred {errorCode}: {message}");
    }

    void Update()
    {
        // カメラ再生中＆フレーム更新があった時のみ
        if (multiSource2MatHelper.IsPlaying() && multiSource2MatHelper.DidUpdateThisFrame())
        {
            Mat rgbaMat = multiSource2MatHelper.GetMat();
            if (rgbaMat == null || rgbaMat.empty()) return;

            // ---- (1) skipFrames 毎に円を検出 -> lastCircles を更新 ----
            if (Time.frameCount % skipFrames == 0)
            {
                DetectCircles(rgbaMat);
            }

            // ---- (2) 毎フレーム、lastCircles の円を描画 ----
            DrawCircles(rgbaMat, lastCircles);

            // ---- (3) テクスチャに転送 (1フレーム1回だけ) ----
            if (texture != null)
            {
                Utils.matToTexture2D(rgbaMat, texture);
            }
        }
    }

    /// <summary>
    /// 円検出 (HoughCircles) を行い、lastCircles に保存する
    /// </summary>
    /// <param name="rgbaMat">カメラフレーム</param>
    private void DetectCircles(Mat rgbaMat)
    {
        // 画像サイズ
        int w = rgbaMat.width();
        int h = rgbaMat.height();

        // ダウンスケール
        // 例: 半分の解像度で検出（軽量化）
        int downW = w / 2;
        int downH = h / 2;

        // smallMat 作成
        Mat smallMat = new Mat();
        Imgproc.resize(rgbaMat, smallMat, new Size(downW, downH));

        // グレースケール
        Mat gray = new Mat();
        Imgproc.cvtColor(smallMat, gray, Imgproc.COLOR_RGBA2GRAY);

        // ブラー
        Imgproc.GaussianBlur(gray, gray, new Size(5, 5), 0);

        // エッジ検出
        Mat edges = new Mat();
        Imgproc.Canny(gray, edges, 50, 150);

        // HoughCircles
        Mat circles = new Mat();
        Imgproc.HoughCircles(
            edges,
            circles,
            Imgproc.HOUGH_GRADIENT,
            1.0,
            edges.rows() / 8,  // 最小距離
            param1,
            param2,
            minRadius / 2,
            maxRadius / 2
        );

        // 結果を lastCircles に格納（クリアしてから追加）
        lastCircles.Clear();

        for (int i = 0; i < circles.cols(); i++)
        {
            double[] data = circles.get(0, i);
            float cx = (float)data[0];
            float cy = (float)data[1];
            float r = (float)data[2];

            // スケールを元に戻す
            float scaleX = (float)w / downW;
            float scaleY = (float)h / downH;

            float cxFull = cx * scaleX;
            float cyFull = cy * scaleY;
            float rFull = r * scaleX; // 円の半径は等倍率のみで十分

            lastCircles.Add(new CircleData(cxFull, cyFull, rFull));

            if (logDetection)
            {
                Debug.Log($"[DetectCircles] Center=({cxFull:F1},{cyFull:F1}), R={rFull:F1}");
            }
        }

        // 後始末
        smallMat.Dispose();
        gray.Dispose();
        edges.Dispose();
        circles.Dispose();
    }

    /// <summary>
    /// 保存した円を描画 (毎フレーム呼び出される)
    /// </summary>
    /// <param name="rgbaMat">カメラ映像</param>
    /// <param name="circles">検出結果</param>
    private void DrawCircles(Mat rgbaMat, List<CircleData> circles)
    {
        foreach (var c in circles)
        {
            Imgproc.circle(
                rgbaMat,
                new Point(c.cx, c.cy),
                (int)c.r,
                new Scalar(0, 0, 255, 255),
                3
            );
        }
    }

    // ----------- UIボタン例 -----------
    public void OnPlayButtonClick()
    {
        multiSource2MatHelper.Play();
    }

    public void OnPauseButtonClick()
    {
        multiSource2MatHelper.Pause();
    }

    public void OnStopButtonClick()
    {
        multiSource2MatHelper.Stop();
    }

    public void OnChangeCameraButtonClick()
    {
        multiSource2MatHelper.requestedIsFrontFacing = !multiSource2MatHelper.requestedIsFrontFacing;
        multiSource2MatHelper.Initialize();
    }

    void OnDestroy()
    {
        if (multiSource2MatHelper != null)
            multiSource2MatHelper.Dispose();

        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }
    }
}
