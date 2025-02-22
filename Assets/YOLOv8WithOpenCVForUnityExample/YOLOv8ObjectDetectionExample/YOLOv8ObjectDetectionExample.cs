#if !(PLATFORM_LUMIN && !UNITY_EDITOR)

#if !UNITY_WSA_10_0

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ImgcodecsModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UnityUtils.Helper;
using YOLOv8WithOpenCVForUnity;
using System.Threading;
using UnityEngine.UI;
using System.IO;

namespace YOLOv8WithOpenCVForUnityExample
{
    /// <summary>
    /// YOLOv8 Object Detection Example
    /// Referring to https://github.com/ultralytics/ultralytics/
    /// https://github.com/ultralytics/ultralytics/tree/main/examples/YOLOv8-OpenCV-ONNX-Python
    /// </summary>
    [RequireComponent(typeof(MultiSource2MatHelper))]
    public class YOLOv8ObjectDetectionExample : MonoBehaviour
    {
        [Header("Output")]
        public RawImage resultPreview;

        [Space(10)]
        public string modelPath = "yolov11_pool.onnx";
        public string classesPath = "class.names";

        public float confThreshold = 0.25f;
        public float nmsThreshold = 0.45f;
        public int topK = 300;

        public int inpWidth = 640;
        public int inpHeight = 640;

        protected Texture2D texture;
        protected MultiSource2MatHelper multiSource2MatHelper;
        protected Mat bgrMat;

        YOLOv8ObjectDetector objectDetector;
        protected FpsMonitor fpsMonitor;

        protected string classes_filepath;
        protected string model_filepath;

        CancellationTokenSource cts = new CancellationTokenSource();

        // 最新の推論結果を保持
        private Mat latestResults;

        async void Start()
        {
            fpsMonitor = GetComponent<FpsMonitor>();

            multiSource2MatHelper = gameObject.GetComponent<MultiSource2MatHelper>();
            multiSource2MatHelper.outputColorFormat = Source2MatHelperColorFormat.RGBA;

            if (fpsMonitor != null)
                fpsMonitor.consoleText = "Preparing file access...";

            // StreamingAssetsからファイルパスを取得
            string streamingAssetsPath = Application.streamingAssetsPath;
            if (!string.IsNullOrEmpty(classesPath))
                classes_filepath = Path.Combine(streamingAssetsPath, classesPath);
            if (!string.IsNullOrEmpty(modelPath))
                model_filepath = Path.Combine(streamingAssetsPath, modelPath);

            if (fpsMonitor != null)
                fpsMonitor.consoleText = "";

            Run();
        }

        protected virtual void Run()
        {
            //if true, The error log of the Native side OpenCV will be displayed on the Unity Editor Console.
            Utils.setDebugMode(true);

            if (string.IsNullOrEmpty(model_filepath))
            {
                Debug.LogError("model: " + modelPath + " is not loaded.");
            }
            else
            {
                objectDetector = new YOLOv8ObjectDetector(
                    model_filepath,
                    classes_filepath,
                    new Size(inpWidth, inpHeight),
                    confThreshold,
                    nmsThreshold,
                    topK
                );
            }

            multiSource2MatHelper.Initialize();
        }

        public virtual void OnSourceToMatHelperInitialized()
        {
            Debug.Log("OnSourceToMatHelperInitialized");

            Mat rgbaMat = multiSource2MatHelper.GetMat();

            texture = new Texture2D(rgbaMat.cols(), rgbaMat.rows(), TextureFormat.RGBA32, false);

            resultPreview.texture = texture;
            resultPreview.GetComponent<AspectRatioFitter>().aspectRatio = (float)texture.width / texture.height;

            if (fpsMonitor != null)
            {
                fpsMonitor.Add("width", rgbaMat.width().ToString());
                fpsMonitor.Add("height", rgbaMat.height().ToString());
                fpsMonitor.Add("orientation", Screen.orientation.ToString());
            }

            bgrMat = new Mat(rgbaMat.rows(), rgbaMat.cols(), CvType.CV_8UC3);
        }

        public virtual void OnSourceToMatHelperDisposed()
        {
            Debug.Log("OnSourceToMatHelperDisposed");

            if (bgrMat != null)
                bgrMat.Dispose();

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

        protected virtual void Update()
        {
            if (multiSource2MatHelper.IsPlaying() && multiSource2MatHelper.DidUpdateThisFrame())
            {
                Mat rgbaMat = multiSource2MatHelper.GetMat();

                if (objectDetector == null)
                {
                    Imgproc.putText(rgbaMat, "model file is not loaded.",
                        new Point(5, rgbaMat.rows() - 30),
                        Imgproc.FONT_HERSHEY_SIMPLEX,
                        0.7,
                        new Scalar(255, 255, 255, 255),
                        2,
                        Imgproc.LINE_AA,
                        false);
                    Imgproc.putText(rgbaMat, "Please read console message.",
                        new Point(5, rgbaMat.rows() - 10),
                        Imgproc.FONT_HERSHEY_SIMPLEX,
                        0.7,
                        new Scalar(255, 255, 255, 255),
                        2,
                        Imgproc.LINE_AA,
                        false);
                }
                else
                {
                    // ★ 推論前 bgrMat に変換
                    Imgproc.cvtColor(rgbaMat, bgrMat, Imgproc.COLOR_RGBA2BGR);

                    // デバッグ: bgrMat.size()
                    Debug.Log($"[YOLOv8ObjectDetectionExample] bgrMat size = ({bgrMat.width()} x {bgrMat.height()})");

                    if (latestResults != null) latestResults.Dispose();
                    latestResults = objectDetector.infer(bgrMat);

                    // ★ ここで DebugBBoxInfo を呼び出して、検出結果をログ
                    DebugBBoxInfo(latestResults, bgrMat.size());

                    // 可視化
                    Imgproc.cvtColor(bgrMat, rgbaMat, Imgproc.COLOR_BGR2RGBA);
                    objectDetector.visualize(rgbaMat, latestResults, false, true);
                }

                Utils.matToTexture2D(rgbaMat, texture);
            }
        }

        protected virtual void OnDestroy()
        {
            multiSource2MatHelper.Dispose();

            if (objectDetector != null)
                objectDetector.dispose();

            Utils.setDebugMode(false);

            if (cts != null)
                cts.Dispose();

            if (latestResults != null)
            {
                latestResults.Dispose();
                latestResults = null;
            }
        }

        public virtual void OnBackButtonClick()
        {
            SceneManager.LoadScene("YOLOv8WithOpenCVForUnityExample");
        }

        public virtual void OnPlayButtonClick()
        {
            multiSource2MatHelper.Play();
        }

        public virtual void OnPauseButtonClick()
        {
            multiSource2MatHelper.Pause();
        }

        public virtual void OnStopButtonClick()
        {
            multiSource2MatHelper.Stop();
        }

        public virtual void OnChangeCameraButtonClick()
        {
            multiSource2MatHelper.requestedIsFrontFacing = !multiSource2MatHelper.requestedIsFrontFacing;
        }

        public Mat GetLatestResults()
        {
            return latestResults;
        }

        /// <summary>
        /// デバッグ用に推論結果 (results) の BBox 情報をログに出す
        /// </summary>
        /// <param name="results">infer() で得られた Mat [n,6] (xyxy, conf, class)</param>
        /// <param name="imageSize">推論に使った bgrMat.size()</param>
        private void DebugBBoxInfo(Mat results, Size imageSize)
        {
            if (results == null || results.empty())
            {
                Debug.Log("[DebugBBoxInfo] results is empty. No detections.");
                return;
            }
            Debug.Log($"[DebugBBoxInfo] results rows = {results.rows()} (imageSize={imageSize.width}x{imageSize.height})");

            // サンプルとして1番目だけチェック
            float[] row = new float[6];
            results.get(0, 0, row);
            float x1 = row[0], y1 = row[1], x2 = row[2], y2 = row[3];
            float conf = row[4], clsId = row[5];
            Debug.Log($"[DebugBBoxInfo] first bbox => (x1={x1}, y1={y1}, x2={x2}, y2={y2}), conf={conf}, cls={clsId}");

            // xy2がimageSize超えてれば注意
            if (x2 > imageSize.width || y2 > imageSize.height)
            {
                Debug.LogWarning("[DebugBBoxInfo] The bounding box is outside the imageSize. Check scaling!");
            }
        }
    }
}

#endif

#endif
