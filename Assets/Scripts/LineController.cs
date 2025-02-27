using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using System;
using System.Collections.Generic;
using ibc.controller; // CueControllerを使用するための名前空間を追加

public class LineController : MonoBehaviour
{
    // WebカメラとQuadの参照
    private WebCamTexture webCamTexture;
    public GameObject quadObject;
    private Renderer quadRenderer;

    // カメラ名を指定するためのパラメータを追加
    public string cameraDeviceName = "Anker PowerConf C200";
    
    // OpenCV関連
    private Mat rgbaMat;
    private Texture2D texture;
    
    // 検出関連
    private GameObject targetBall; // WhiteBallタグのついた対象オブジェクト
    private OpenCVForUnity.CoreModule.Rect regionOfInterest; // 検出対象領域
    
    // 中心位置のオフセット（X, Y）
    public Vector2 centerOffset = new Vector2(0, 0);
    
    // 検出領域のサイズ倍率（1.0が通常サイズ）
    public float regionSizeMultiplier = 0.6f;
    
    // オフセット値の上昇率を追加
    [Tooltip("ボールの位置に応じたオフセット値の上昇率（単位あたりの増加量）")]
    public Vector2 offsetScaleFactor = new Vector2(50.0f, 60.0f);
    
    // CueControllerへの参照を追加
    public CueController cueController;
    
    // 最後に検出された角度を保持
    private double lastDetectedAngle = 0;
    
    // 角度のスムージング用
    public bool useSmoothing = true;
    public float smoothingFactor = 0.5f; // 0.0〜1.0 (1.0で完全に新しい値を使用)

    // 白色検出のためのパラメータ
    [Range(0, 255)]
    public int whiteThreshold = 200; // 白色と判定する閾値（0-255）
    
    // 検出する白色領域の最小サイズ（ピクセル数）
    public int minWhiteAreaSize = 150;
    
    // 検出する白色領域の最大サイズ（ピクセル数）を追加
    public int maxWhiteAreaSize = 200;
    
    // 動的オフセット用の基本オフセット
    private Vector2 baseOffset;

    void Start()
    {
        // 基本オフセットを保存
        baseOffset = centerOffset;
        
        // Quadのレンダラーを取得
        quadRenderer = quadObject.GetComponent<Renderer>();

        // WebCameraをセットアップ
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("Webカメラが見つかりません");
            return;
        }

        // 利用可能なカメラの一覧をログに出力
        Debug.Log("利用可能なカメラ一覧:");
        foreach (WebCamDevice device in devices)
        {
            Debug.Log("カメラ名: " + device.name);
        }

        // カメラ名が指定されている場合はその名前のカメラを使用
        // 指定がない場合はデフォルトカメラ（最初のカメラ）を使用
        if (!string.IsNullOrEmpty(cameraDeviceName))
        {
            bool foundSpecifiedCamera = false;
            foreach (WebCamDevice device in devices)
            {
                if (device.name == cameraDeviceName)
                {
                    webCamTexture = new WebCamTexture(cameraDeviceName, 1920, 1080, 30);
                    foundSpecifiedCamera = true;
                    Debug.Log("指定されたカメラを使用します: " + cameraDeviceName);
                    break;
                }
            }
            
            if (!foundSpecifiedCamera)
            {
                Debug.LogWarning("指定されたカメラ '" + cameraDeviceName + "' が見つかりません。デフォルトカメラを使用します。");
                webCamTexture = new WebCamTexture(devices[0].name, 1920, 1080, 30);
            }
        }
        else
        {
            // カメラ名が指定されていない場合はデフォルトカメラを使用
            webCamTexture = new WebCamTexture(devices[0].name, 1920, 1080, 30);
            Debug.Log("デフォルトカメラを使用します: " + devices[0].name);
        }
        
        webCamTexture.Play();

        // マットとテクスチャの初期化（サイズはカメラに合わせて後で更新）
        rgbaMat = new Mat();
        
        // 1秒後にWhiteBallオブジェクトの位置を取得
        Invoke("SetupDetectionRegion", 1.0f);
        
        // CueControllerが設定されていない場合は自動検索
        if (cueController == null)
        {
            cueController = FindObjectOfType<CueController>();
            if (cueController == null)
            {
                Debug.LogWarning("CueControllerが見つかりません。角度の設定ができません。");
            }
        }
    }

    void Update()
    {
        if (webCamTexture != null && webCamTexture.isPlaying && webCamTexture.didUpdateThisFrame)
        {
            // WebCameraのテクスチャがまだ初期化されていない場合はスキップ
            if (webCamTexture.width < 100)
                return;

            // サイズ変更の確認と初期化
            if (rgbaMat.empty() || rgbaMat.width() != webCamTexture.width || rgbaMat.height() != webCamTexture.height)
            {
                rgbaMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);
                texture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);
                quadRenderer.material.mainTexture = texture;
            }

            // WhiteBallの位置に応じてオフセットを更新
            UpdateOffsetBasedOnBallPosition();

            // WebCameraの映像をMatに変換
            Utils.webCamTextureToMat(webCamTexture, rgbaMat);
            
            // 検出領域が設定されていれば物体検出を行う
            if (targetBall != null && regionOfInterest.width > 0 && regionOfInterest.height > 0)
            {
                DetectWhiteObjects();
            }
            
            // MatをTextureに変換してQuadに表示
            Utils.matToTexture2D(rgbaMat, texture);
        }
    }
    
    // ボールの位置に応じてオフセットを更新するメソッドを追加
    void UpdateOffsetBasedOnBallPosition()
    {
        if (targetBall != null)
        {
            // ボールのワールド座標を取得
            Vector3 ballPosition = targetBall.transform.position;
            
            // ワールド座標に基づいてオフセットを計算
            // 見下ろし視点: xは左右、zは上下として扱う
            // 原点(0,0,0)からの距離に応じてオフセットを増加させる
            Vector2 dynamicOffset = new Vector2(
                ballPosition.x * offsetScaleFactor.x,
                -ballPosition.z * offsetScaleFactor.y  // Z軸方向の符号を反転
            );
            
            // 基本オフセットに動的オフセットを加算
            centerOffset = baseOffset + dynamicOffset;
            
            // 検出領域を更新
            UpdateDetectionRegion();
        }
    }
    
    // 検出領域を更新するメソッド
    void UpdateDetectionRegion()
    {
        if (targetBall != null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                // 3D空間のボールの位置とサイズを取得
                Vector3 screenPos = mainCamera.WorldToScreenPoint(targetBall.transform.position);
                
                // ボールのスケールを取得
                float radius = targetBall.transform.localScale.x * 50 * regionSizeMultiplier;
                
                // OpenCVの座標系に変換（Y軸が逆）
                float x = screenPos.x;
                float y = webCamTexture.height - screenPos.y;
                
                // 検出領域を設定（現在のオフセットを適用）
                regionOfInterest = new OpenCVForUnity.CoreModule.Rect(
                    (int)(x - radius + centerOffset.x), 
                    (int)(y - radius + centerOffset.y), 
                    (int)(radius * 2), 
                    (int)(radius * 2)
                );
            }
        }
    }
    
    void SetupDetectionRegion()
    {
        // WhiteBallタグのついたオブジェクトを検索
        GameObject[] whiteBalls = GameObject.FindGameObjectsWithTag("WhiteBall");
        
        if (whiteBalls.Length > 0)
        {
            Debug.Log("WhiteBallタグのオブジェクトが見つかりました: " + whiteBalls.Length + "個");
            targetBall = whiteBalls[0]; // 最初のボールを対象とする
            
            // オブジェクトのスクリーン座標を取得
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                // 3D空間のボールの位置とサイズを取得
                Vector3 screenPos = mainCamera.WorldToScreenPoint(targetBall.transform.position);
                
                // ボールのスケールを取得（仮定：ボールはほぼ球体でXスケールが半径に相当）
                float radius = targetBall.transform.localScale.x * 50 * regionSizeMultiplier; // 半径をピクセル単位に変換し、倍率を適用
                
                // OpenCVの座標系に変換（Y軸が逆）
                float x = screenPos.x;
                float y = webCamTexture.height - screenPos.y;
                
                // 検出領域を設定
                regionOfInterest = new OpenCVForUnity.CoreModule.Rect(
                    (int)(x - radius + centerOffset.x), 
                    (int)(y - radius + centerOffset.y), 
                    (int)(radius * 2), 
                    (int)(radius * 2)
                );
                
                Debug.Log("検出領域を設定: " + regionOfInterest);
            }
            else
            {
                Debug.LogError("メインカメラが見つかりません");
            }
        }
        else
        {
            Debug.LogError("WhiteBallタグのオブジェクトが見つかりません");
        }
    }
    
    void DetectWhiteObjects()
    {
        try
        {
            // 検出領域が画像内に収まるように調整
            OpenCVForUnity.CoreModule.Rect safeROI = new OpenCVForUnity.CoreModule.Rect(
                Mathf.Max(0, regionOfInterest.x),
                Mathf.Max(0, regionOfInterest.y),
                Mathf.Min(regionOfInterest.width, rgbaMat.width() - regionOfInterest.x),
                Mathf.Min(regionOfInterest.height, rgbaMat.height() - regionOfInterest.y)
            );
            
            // 領域が有効かチェック
            if (safeROI.width <= 0 || safeROI.height <= 0 || 
                safeROI.x >= rgbaMat.width() || safeROI.y >= rgbaMat.height())
            {
                Debug.LogWarning("有効な検出領域がありません");
                return;
            }
            
            // 円の中心座標と半径を計算
            double circleCenterX = safeROI.x + safeROI.width / 2.0;
            double circleCenterY = safeROI.y + safeROI.height / 2.0;
            double circleRadius = safeROI.width / 2.0; // 矩形の幅の半分を円の半径として使用
            
            // 関心領域を抽出
            Mat roiMat = new Mat(rgbaMat, safeROI);
            
            // 円形マスクを作成
            Mat mask = new Mat(roiMat.size(), CvType.CV_8UC1, new Scalar(0));
            Imgproc.circle(mask, new Point(safeROI.width / 2, safeROI.height / 2), 
                           (int)circleRadius, new Scalar(255), -1);
            
            // マスクを適用
            Mat maskedRoiMat = new Mat();
            roiMat.copyTo(maskedRoiMat, mask);
            
            // グレースケールに変換
            Mat grayMat = new Mat();
            Imgproc.cvtColor(maskedRoiMat, grayMat, Imgproc.COLOR_RGBA2GRAY);
            
            // しきい値処理で白い部分を抽出
            Mat thresholdMat = new Mat();
            Imgproc.threshold(grayMat, thresholdMat, whiteThreshold, 255, Imgproc.THRESH_BINARY);
            
            // 輪郭検出
            List<MatOfPoint> contours = new List<MatOfPoint>();
            Mat hierarchy = new Mat();
            Imgproc.findContours(thresholdMat, contours, hierarchy, Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);
            
            // 最も大きな輪郭を探す（または特定のサイズ以上の全ての輪郭）
            Point largestContourCenter = new Point();
            double largestContourArea = 0;
            bool foundValidContour = false;
            
            // 輪郭を処理
            for (int i = 0; i < contours.Count; i++)
            {
                double area = Imgproc.contourArea(contours[i]);
                
                // 最小サイズ以上かつ最大サイズ以下の輪郭のみ処理
                if (area > minWhiteAreaSize && area < maxWhiteAreaSize)
                {
                    // 輪郭の重心を計算
                    Moments moments = Imgproc.moments(contours[i]);
                    double centerX = moments.m10 / moments.m00;
                    double centerY = moments.m01 / moments.m00;
                    
                    // 輪郭の中心点（ROI座標系）
                    Point contourCenter = new Point(centerX, centerY);
                    
                    // ROI座標からグローバル座標に変換
                    Point globalContourCenter = new Point(contourCenter.x + safeROI.x, contourCenter.y + safeROI.y);
                    
                    // 輪郭を描画
                    Imgproc.drawContours(rgbaMat, contours, i, new Scalar(0, 0, 255, 255), 2, Imgproc.LINE_8, hierarchy, 0, new Point(safeROI.x, safeROI.y));
                    
                    // 中心点を描画
                    Imgproc.circle(rgbaMat, globalContourCenter, 5, new Scalar(255, 0, 0, 255), -1);
                    
                    // 白い物体の中心からWhiteBallの中心に線を引く
                    Imgproc.line(rgbaMat, 
                        new Point(circleCenterX, circleCenterY),
                        globalContourCenter,
                        new Scalar(0, 255, 0, 255), 2);
                    
                    // 物体と円の中心との角度を計算
                    double dx = globalContourCenter.x - circleCenterX;
                    double dy = circleCenterY - globalContourCenter.y; // Y軸は反転している点に注意
                    
                    // 角度を計算（真下を0度として反時計回りに）
                    double angleRadians = Math.Atan2(-dx, -dy);
                    double angleDegrees = angleRadians * (180.0 / Math.PI);
                    
                    // 角度が負の場合は360度に変換
                    if (angleDegrees < 0)
                    {
                        angleDegrees += 360.0;
                    }
                    
                    Debug.Log("検出された白い物体 " + i + " の角度: " + angleDegrees.ToString("F1") + "度 (面積: " + area + ")");
                    
                    // より大きい領域が見つかった場合、それを最適な領域として記録
                    if (area > largestContourArea)
                    {
                        largestContourArea = area;
                        largestContourCenter = globalContourCenter;
                        foundValidContour = true;
                        
                        // 最後に検出された角度を更新
                        if (useSmoothing)
                        {
                            // 新しい角度と前回の角度の間で補間
                            double smoothedAngle = lastDetectedAngle * (1.0 - smoothingFactor) + angleDegrees * smoothingFactor;
                            lastDetectedAngle = smoothedAngle;
                        }
                        else
                        {
                            lastDetectedAngle = angleDegrees;
                        }
                    }
                }
            }
            
            // 有効な輪郭が見つかった場合、その角度をCueControllerに設定
            if (foundValidContour && cueController != null)
            {
                // CueControllerのjawを設定
                cueController.SetJaw((float)lastDetectedAngle);
                
                Debug.Log("CueControllerのJawを設定: " + lastDetectedAngle.ToString("F1") + "度");
            }
            
            // WhiteBallの検出領域を可視化
            Imgproc.circle(rgbaMat, 
                new Point(circleCenterX, circleCenterY), 
                (int)circleRadius, 
                new Scalar(0, 255, 255, 128), 2);
            
            // WhiteBallの中心を表示
            Imgproc.circle(rgbaMat, 
                new Point(circleCenterX, circleCenterY), 
                5, new Scalar(0, 255, 255, 255), -1);
            
            // オフセット情報の表示は削除
            
            // メモリ解放
            roiMat.release();
            maskedRoiMat.release();
            mask.release();
            grayMat.release();
            thresholdMat.release();
            hierarchy.release();
            for (int i = 0; i < contours.Count; i++)
            {
                contours[i].release();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("検出処理でエラーが発生しました: " + e.Message);
        }
    }

    void OnDestroy()
    {
        // リソースの解放
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            webCamTexture = null;
        }

        if (rgbaMat != null)
        {
            rgbaMat.Dispose();
            rgbaMat = null;
        }
    }
}