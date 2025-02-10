using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Rect = OpenCVForUnity.CoreModule.Rect;

namespace OpenCVForUnityExample
{
    /// <summary>
    /// BallDetection Example (一度検出したら円を消さない版)
    /// </summary>
    [RequireComponent(typeof(MultiSource2MatHelper))]
    public class BallDetection : MonoBehaviour
    {
        [Header("Output")]
        public RawImage resultPreview;

        FpsMonitor fpsMonitor;
        MultiSource2MatHelper multiSource2MatHelper;

        Texture2D texture;

        Mat grayMat;
        Mat circlesMat;

        CancellationTokenSource cts = new CancellationTokenSource();

        // ------------------------------------------------------
        // HoughCircles用パラメータ (Inspectorで調整)
        // ------------------------------------------------------
        [Header("HoughCircles Parameters")]
        [Tooltip("dp: 分解能の逆数 (例: 1.0f)")]
        public float dp = 1.0f;

        [Tooltip("minDistの係数 (実際には grayMat.rows() / minDistFactor) を使用")]
        public float minDistFactor = 8f;

        [Tooltip("Canny の上限閾値")]
        public float param1 = 100f;

        [Tooltip("円と判定する際の閾値。大きいほど検出厳しめ")]
        public float param2 = 50f;

        [Tooltip("検出する円の最小半径 (0なら制限なし)")]
        public int minRadius = 0;

        [Tooltip("検出する円の最大半径 (0なら制限なし)")]
        public int maxRadius = 0;

        // ------------------------------------------------------
        // カラー情報によるマスク（オプション）
        // ------------------------------------------------------
        [Header("Color Filtering (Optional)")]
        [Tooltip("特定色でマスクをかけるか？ (ボールの色が分かっている場合に有効)")]
        public bool doColorFiltering = false;

        [Tooltip("Color Filtering の下限値 (HSVやBGRなどフォーマットに注意)")]
        public Color32 lowerBoundColor = new Color32(0, 100, 100, 0);

        [Tooltip("Color Filtering の上限値")]
        public Color32 upperBoundColor = new Color32(10, 255, 255, 0);

        Mat maskMat;

        // ------------------------------------------------------
        // 結果の安定化用（最大円のみ + 前フレームとの比較）
        // ------------------------------------------------------
        [Header("Simple Stabilization")]
        [Tooltip("安定化処理を行うか？(最大円のみ採用＋前フレームとの位置補正)")]
        public bool doStabilization = true;

        [Tooltip("前フレームの円とどれぐらい離れていたら急激とみなすか？ (px)")]
        public float maxMoveDistance = 50f;

        [Tooltip("座標を Exponential Smoothing するか？(0.0fで無効、～1.0fで緩やか)")]
        [Range(0.0f, 1.0f)]
        public float smoothingFactor = 0.0f;

        // 安定化用の前フレーム円データ
        CircleData lastCircle;
        bool hasLastCircle = false; // 一度でも検出されたらtrueにし、以降消さない

        struct CircleData
        {
            public float centerX;
            public float centerY;
            public float radius;
        }

        async void Start()
        {
            fpsMonitor = GetComponent<FpsMonitor>();

            multiSource2MatHelper = gameObject.GetComponent<MultiSource2MatHelper>();
            multiSource2MatHelper.outputColorFormat = Source2MatHelperColorFormat.RGBA;

            // イベントリスナー登録 (重要)
            multiSource2MatHelper.onInitialized.AddListener(OnSourceToMatHelperInitialized);
            multiSource2MatHelper.onDisposed.AddListener(OnSourceToMatHelperDisposed);
            multiSource2MatHelper.onErrorOccurred.AddListener(OnSourceToMatHelperErrorOccurred);

            // カメラ初期化開始
            multiSource2MatHelper.Initialize();
        }

        public void OnSourceToMatHelperInitialized()
        {
            Debug.Log("OnSourceToMatHelperInitialized");

            Mat rgbaMat = multiSource2MatHelper.GetMat();

            texture = new Texture2D(rgbaMat.cols(), rgbaMat.rows(), TextureFormat.RGBA32, false);
            Utils.matToTexture2D(rgbaMat, texture);

            if (resultPreview != null)
            {
                resultPreview.texture = texture;
                AspectRatioFitter fitter = resultPreview.GetComponent<AspectRatioFitter>();
                if (fitter != null)
                {
                    fitter.aspectRatio = (float)texture.width / texture.height;
                }
            }

            if (fpsMonitor != null)
            {
                fpsMonitor.Add("width", rgbaMat.width().ToString());
                fpsMonitor.Add("height", rgbaMat.height().ToString());
                fpsMonitor.Add("orientation", Screen.orientation.ToString());
            }

            grayMat = new Mat(rgbaMat.rows(), rgbaMat.cols(), CvType.CV_8UC1);
            circlesMat = new Mat();

            if (doColorFiltering)
            {
                maskMat = new Mat(rgbaMat.rows(), rgbaMat.cols(), CvType.CV_8UC1);
            }

            hasLastCircle = false;
        }

        public void OnSourceToMatHelperDisposed()
        {
            Debug.Log("OnSourceToMatHelperDisposed");

            if (grayMat != null)
            {
                grayMat.Dispose();
                grayMat = null;
            }
            if (circlesMat != null)
            {
                circlesMat.Dispose();
                circlesMat = null;
            }
            if (maskMat != null)
            {
                maskMat.Dispose();
                maskMat = null;
            }
            if (texture != null)
            {
                Texture2D.Destroy(texture);
                texture = null;
            }
        }

        public void OnSourceToMatHelperErrorOccurred(Source2MatHelperErrorCode errorCode, string message)
        {
            Debug.Log("OnSourceToMatHelperErrorOccurred " + errorCode + ":" + message);
            if (fpsMonitor != null)
            {
                fpsMonitor.consoleText = "ErrorCode: " + errorCode + ":" + message;
            }
        }

        void Update()
        {
            if (multiSource2MatHelper.IsPlaying() && multiSource2MatHelper.DidUpdateThisFrame())
            {
                Mat rgbaMat = multiSource2MatHelper.GetMat();
                if (rgbaMat == null || rgbaMat.empty()) return;

                // (1) カラー情報フィルタ (オプション)
                if (doColorFiltering && maskMat != null)
                {
                    // 簡易的に RGBA のまま inRange
                    Scalar lower = new Scalar(lowerBoundColor.r, lowerBoundColor.g, lowerBoundColor.b, lowerBoundColor.a);
                    Scalar upper = new Scalar(upperBoundColor.r, upperBoundColor.g, upperBoundColor.b, upperBoundColor.a);
                    Core.inRange(rgbaMat, lower, upper, maskMat);

                    // マスク結果を grayMat へ (1ch)
                    maskMat.copyTo(grayMat);
                }
                else
                {
                    // RGBA → GRAY
                    Imgproc.cvtColor(rgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);
                }

                // (2) ブラーでノイズ除去
                Imgproc.GaussianBlur(grayMat, grayMat, new Size(9, 9), 2, 2);

                // (3) HoughCircles
                circlesMat.release();
                double minDist = grayMat.rows() / minDistFactor;
                Imgproc.HoughCircles(
                    grayMat,
                    circlesMat,
                    Imgproc.HOUGH_GRADIENT,
                    dp,
                    minDist,
                    param1,
                    param2,
                    minRadius,
                    maxRadius
                );

                // (4) 最大円 or 全円を取得し、安定化処理
                if (circlesMat.cols() > 0)
                {
                    // 検出があった
                    if (doStabilization)
                    {
                        // 最大半径の円を1つ選ぶ
                        float maxR = -1f;
                        CircleData bestCircle = new CircleData();

                        float[] circleData = new float[3];
                        for (int i = 0; i < circlesMat.cols(); i++)
                        {
                            circlesMat.get(0, i, circleData);
                            float r = circleData[2];
                            if (r > maxR)
                            {
                                maxR = r;
                                bestCircle.centerX = circleData[0];
                                bestCircle.centerY = circleData[1];
                                bestCircle.radius = r;
                            }
                        }

                        if (hasLastCircle)
                        {
                            // 前フレームとの急激な差を補正
                            float dx = bestCircle.centerX - lastCircle.centerX;
                            float dy = bestCircle.centerY - lastCircle.centerY;
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            if (dist > maxMoveDistance)
                            {
                                // 前フレーム位置を使う
                                bestCircle.centerX = lastCircle.centerX;
                                bestCircle.centerY = lastCircle.centerY;
                            }

                            // Exponential Smoothing (smoothingFactor > 0)
                            if (smoothingFactor > 0f && smoothingFactor < 1f)
                            {
                                bestCircle.centerX = smoothingFactor * bestCircle.centerX + (1f - smoothingFactor) * lastCircle.centerX;
                                bestCircle.centerY = smoothingFactor * bestCircle.centerY + (1f - smoothingFactor) * lastCircle.centerY;
                                bestCircle.radius = smoothingFactor * bestCircle.radius + (1f - smoothingFactor) * lastCircle.radius;
                            }
                        }

                        lastCircle = bestCircle;
                        hasLastCircle = true;
                    }
                    else
                    {
                        // 安定化しない → すべて描画（下の描画フェーズで行う）
                    }

                }
                else
                {
                    // 検出なし（ここでは円を消すロジックは入れない！）
                    // → hasLastCircle が true のままなら、前フレームの位置を表示し続ける
                }

                // (5) 描画
                if (doStabilization)
                {
                    // 安定化: hasLastCircle がtrueなら lastCircle を常に描画
                    if (hasLastCircle)
                    {
                        Point center = new Point(lastCircle.centerX, lastCircle.centerY);
                        Imgproc.circle(rgbaMat, center, 3, new Scalar(0, 255, 0, 255), -1);
                        Imgproc.circle(rgbaMat, center, (int)lastCircle.radius, new Scalar(255, 0, 0, 255), 3);
                    }
                }
                else
                {
                    // 安定化無効の場合、フレームごとに全ての円を描画
                    float[] circleData = new float[3];
                    for (int i = 0; i < circlesMat.cols(); i++)
                    {
                        circlesMat.get(0, i, circleData);
                        float cx = circleData[0];
                        float cy = circleData[1];
                        float r = circleData[2];

                        Point c = new Point(cx, cy);
                        Imgproc.circle(rgbaMat, c, 3, new Scalar(0, 255, 0, 255), -1);
                        Imgproc.circle(rgbaMat, c, (int)r, new Scalar(255, 0, 0, 255), 3);
                    }

                    // ただし「一度でも検出した円をずっと表示」なら、ここにも同様に
                    // lastCircle を重ね描画するロジックを入れてもいい (必要なら)
                }

                // (6) テクスチャに反映
                Utils.matToTexture2D(rgbaMat, texture);
            }
        }

        void OnDestroy()
        {
            multiSource2MatHelper.Dispose();
            if (cts != null) cts.Dispose();
        }

        // ------------------------------
        // UIボタン例 (任意)
        // ------------------------------
        public void OnBackButtonClick()
        {
            SceneManager.LoadScene("OpenCVForUnityExample");
        }
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
        }
    }
}
