using UnityEngine;
using UnityEngine.UI;

// OpenCVForUnity
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;

/// <summary>
/// MultiSource2MatHelper を使ってカメラ映像を取り込み、RawImage に表示する。
/// 表示用の Mat (displayMat) を常に保持し、毎フレームそこにコピーしたものをテクスチャに描画。
/// 他スクリプトから GetFrameMat() で参照し、SetFrameMat() で描画を上書きできる。
/// </summary>
[RequireComponent(typeof(MultiSource2MatHelper))]
public class RealCameraCalib : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("カメラ映像を表示する RawImage")]
    public RawImage previewImage;

    [Tooltip("アスペクト比を自動調整する場合はアタッチ")]
    public AspectRatioFitter aspectFitter;

    [Header("WebCam Settings")]
    [SerializeField] private int requestWidth = 1280;
    [SerializeField] private int requestHeight = 720;
    [SerializeField] private int requestFps = 30;
    [SerializeField] private bool requestIsFrontFacing = false;

    // Helper本体
    private MultiSource2MatHelper multiSource2MatHelper;

    // 表示用テクスチャ
    private Texture2D texture;

    // カメラ表示用に確保しておく Mat (DisposeはOnDestroyで)
    private Mat displayMat;

    void Start()
    {
        // 1) MultiSource2MatHelper の取得
        multiSource2MatHelper = GetComponent<MultiSource2MatHelper>();

        // 2) カメラ設定を事前にセット
        multiSource2MatHelper.requestedWidth = requestWidth;
        multiSource2MatHelper.requestedHeight = requestHeight;
        multiSource2MatHelper.requestedFPS = requestFps;
        multiSource2MatHelper.requestedIsFrontFacing = requestIsFrontFacing;

        // イベント登録（シグネチャが合わない場合はコード確認）
        multiSource2MatHelper.onInitialized.AddListener(OnSourceToMatHelperInitialized);
        multiSource2MatHelper.onDisposed.AddListener(OnSourceToMatHelperDisposed);
        multiSource2MatHelper.onErrorOccurred.AddListener(OnSourceToMatHelperErrorOccurred);

        // 3) 初期化開始（autoPlay が無い場合は、後で Play() を呼ぶ）
        multiSource2MatHelper.Initialize();
    }

    /// <summary>
    /// カメラ初期化が完了した時に呼ばれるコールバック
    /// </summary>
    public void OnSourceToMatHelperInitialized()
    {
        Debug.Log("OnSourceToMatHelperInitialized");

        Mat mat = multiSource2MatHelper.GetMat();
        if (mat == null || mat.empty())
        {
            Debug.LogWarning("Camera mat is null or empty.");
            return;
        }

        // displayMatを初期化（サイズ固定で確保）
        displayMat = new Mat(mat.rows(), mat.cols(), mat.type());

        // Texture2D の作成
        texture = new Texture2D(mat.cols(), mat.rows(), TextureFormat.RGBA32, false);

        if (previewImage != null)
        {
            previewImage.texture = texture;

            if (aspectFitter != null)
                aspectFitter.aspectRatio = (float)mat.width() / mat.height();
        }

        // カメラが自動再生されない場合はここでPlayを呼ぶ
        // multiSource2MatHelper.Play();
    }

    /// <summary>
    /// カメラがDisposedされた時に呼ばれるコールバック
    /// </summary>
    public void OnSourceToMatHelperDisposed()
    {
        Debug.Log("OnSourceToMatHelperDisposed");

        if (displayMat != null)
        {
            displayMat.Dispose();
            displayMat = null;
        }

        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }
    }

    /// <summary>
    /// カメラ利用時のエラー発生時に呼ばれるコールバック
    /// </summary>
    public void OnSourceToMatHelperErrorOccurred(Source2MatHelperErrorCode errorCode, string message)
    {
        Debug.LogError($"OnSourceToMatHelperErrorOccurred {errorCode}: {message}");
    }

    void Update()
    {
        // カメラが再生中＆新フレームが来たら更新
        if (multiSource2MatHelper.IsPlaying() && multiSource2MatHelper.DidUpdateThisFrame())
        {
            Mat mat = multiSource2MatHelper.GetMat();
            if (mat != null && !mat.empty())
            {
                // Helper から取得した Mat を displayMat にコピー
                mat.copyTo(displayMat);

                // テクスチャに反映
                if (texture != null)
                {
                    Utils.matToTexture2D(displayMat, texture);
                }
            }
        }
    }

    /// <summary>
    /// 他スクリプトが「現在のカメラ映像」を使いたいとき、displayMatを参照させる。
    /// ※返り値は常に同じインスタンスを返す（使う側はclone()して処理推奨）。
    /// </summary>
    public Mat GetFrameMat()
    {
        return displayMat;
    }

    /// <summary>
    /// 他スクリプトが検出結果などを上書きして「表示」させたい場合に使う。
    /// matの内容を displayMat にコピーし、テクスチャ更新。
    /// </summary>
    public void SetFrameMat(Mat mat)
    {
        if (displayMat == null || mat == null || mat.empty()) return;

        // mat の中身を displayMat にコピー
        mat.copyTo(displayMat);

        // テクスチャ更新
        if (texture != null)
        {
            Utils.matToTexture2D(displayMat, texture);
            texture.Apply();
        }
    }

    // ------------ UIボタン例 ------------
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
        multiSource2MatHelper.Initialize(); // カメラ切り替え
    }

    void OnDestroy()
    {
        if (multiSource2MatHelper != null)
            multiSource2MatHelper.Dispose();

        if (displayMat != null)
        {
            displayMat.Dispose();
            displayMat = null;
        }

        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }
    }
}
