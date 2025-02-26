using UnityEngine;
using Klak.Ndi;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using System.Collections;
using System;

public class NDIStickDetectionScript : MonoBehaviour
{
    [SerializeField] private NdiReceiver ndiReceiver;
    [SerializeField] private MeshRenderer targetQuad;
    
    [Header("白色点検出設定")]
    [SerializeField] [Range(0, 180)] private int hue = 0;
    [SerializeField] [Range(0, 255)] private int saturation = 20;
    [SerializeField] [Range(0, 255)] private int value = 220;
    [SerializeField] [Range(10, 60)] private int hsvThreshold = 20;
    
    [Header("点サイズ設定")]
    [SerializeField] [Range(1, 50)] private int pointRadius = 10;
    
    private GameObject[] whiteCircles;
    private Texture2D processedTexture;
    private Mat rgbaMat;
    private bool isProcessing = false;

    private void Start()
    {
        if (ndiReceiver == null || targetQuad == null)
        {
            Debug.LogError("NDI Receiver or Target Quad not assigned!");
            return;
        }

        // NDIレシーバーの出力テクスチャをQuadのマテリアルに設定
        targetQuad.material.mainTexture = ndiReceiver.targetTexture;
        
        // 1秒待ってからWhiteCircleタグのついたオブジェクトを検知
        Invoke("FindWhiteCircleObjects", 1.0f);
        
        // 画像処理用のテクスチャを初期化
        StartCoroutine(ProcessFrames());
    }
    
    private void FindWhiteCircleObjects()
    {
        // WhiteCircleタグのついたオブジェクトを全て検索
        whiteCircles = GameObject.FindGameObjectsWithTag("WhiteCircle");
        
        // 検知結果をデバッグログに表示
        Debug.Log($"検知されたWhiteCircleオブジェクト数: {whiteCircles.Length}");
        
        foreach (GameObject circle in whiteCircles)
        {
            Debug.Log($"WhiteCircleオブジェクト検知: {circle.name}");
            // 初期色を白に設定
            if (circle.GetComponent<Renderer>() != null)
            {
                circle.GetComponent<Renderer>().material.color = Color.white;
            }
        }
    }
    
    private IEnumerator ProcessFrames()
    {
        // 最初のフレームを待機
        yield return new WaitForSeconds(1.5f);
        
        while (true)
        {
            if (ndiReceiver.targetTexture != null && !isProcessing)
            {
                isProcessing = true;
                
                // NDIテクスチャを処理用テクスチャにコピー
                if (processedTexture == null || 
                    processedTexture.width != ndiReceiver.targetTexture.width || 
                    processedTexture.height != ndiReceiver.targetTexture.height)
                {
                    processedTexture = new Texture2D(
                        ndiReceiver.targetTexture.width,
                        ndiReceiver.targetTexture.height,
                        TextureFormat.RGBA32, 
                        false);
                }
                
                // テクスチャを読み取りOpenCVのMatに変換
                Graphics.CopyTexture(ndiReceiver.targetTexture, processedTexture);
                
                if (rgbaMat == null)
                {
                    rgbaMat = new Mat(processedTexture.height, processedTexture.width, CvType.CV_8UC4);
                }
                
                Utils.texture2DToMat(processedTexture, rgbaMat);
                
                // 白色点の検出
                DetectWhitePoints(rgbaMat);
                
                isProcessing = false;
            }
            
            yield return new WaitForSeconds(1.0f); // 1秒ごとに処理
        }
    }
    
    private void DetectWhitePoints(Mat imgMat)
    {
        // HSV色空間に変換
        Mat hsvMat = new Mat();
        Imgproc.cvtColor(imgMat, hsvMat, Imgproc.COLOR_RGBA2BGR);
        Imgproc.cvtColor(hsvMat, hsvMat, Imgproc.COLOR_BGR2HSV);
        
        // HSV閾値によるマスク作成（目標HSV値から閾値範囲を計算）
        Mat mask = new Mat();
        Scalar lowerBound = new Scalar(
            Math.Max(0, hue - hsvThreshold), 
            Math.Max(0, saturation - hsvThreshold), 
            Math.Max(0, value - hsvThreshold));
        Scalar upperBound = new Scalar(
            Math.Min(180, hue + hsvThreshold), 
            Math.Min(255, saturation + hsvThreshold), 
            Math.Min(255, value + hsvThreshold));
        Core.inRange(hsvMat, lowerBound, upperBound, mask);
        
        // ノイズ除去
        Mat kernel = Imgproc.getStructuringElement(Imgproc.MORPH_ELLIPSE, new Size(5, 5));
        Imgproc.morphologyEx(mask, mask, Imgproc.MORPH_OPEN, kernel);
        
        // 輪郭検出
        Mat hierarchy = new Mat();
        System.Collections.Generic.List<MatOfPoint> contours = new System.Collections.Generic.List<MatOfPoint>();
        Imgproc.findContours(mask, contours, hierarchy, Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);
        
        bool pointDetected = false;
        
        // WhiteCircleの画面上の位置とサイズを取得
        if (whiteCircles != null && whiteCircles.Length > 0)
        {
            foreach (GameObject circle in whiteCircles)
            {
                if (circle == null) continue;
                
                // 円の位置を画面空間に変換
                Vector3 screenPos = Camera.main.WorldToScreenPoint(circle.transform.position);
                
                // 円のサイズを概算（レンダラーのバウンディングボックスを使用）
                Renderer renderer = circle.GetComponent<Renderer>();
                if (renderer == null) continue;
                
                // 円の半径を画面空間に変換（近似値）
                float worldRadius = renderer.bounds.extents.x;
                Vector3 edgeWorld = circle.transform.position + new Vector3(worldRadius, 0, 0);
                Vector3 edgeScreen = Camera.main.WorldToScreenPoint(edgeWorld);
                float screenRadius = Vector3.Distance(screenPos, edgeScreen);
                
                // スクリーン座標から画像座標に変換
                float imageX = screenPos.x * imgMat.width() / Screen.width;
                float imageY = (Screen.height - screenPos.y) * imgMat.height() / Screen.height; // Y座標は反転
                float imageRadius = screenRadius * imgMat.width() / Screen.width;
                
                Point circleCenter = new Point(imageX, imageY);
                
                // 円内の白い点を検出
                foreach (MatOfPoint contour in contours)
                {
                    // 輪郭の面積から半径を計算
                    double area = Imgproc.contourArea(contour);
                    double radius = Math.Sqrt(area / Math.PI);
                    
                    // 点のサイズと大きく異なる場合はスキップ
                    double radiusDiff = Math.Abs(radius - pointRadius);
                    if (radiusDiff > pointRadius * 0.5) continue;
                    
                    // 輪郭を囲む矩形を取得（中心点計算用）
                    OpenCVForUnity.CoreModule.Rect rect = Imgproc.boundingRect(contour);
                    
                    // 矩形の中心点
                    Point rectCenter = new Point(rect.x + rect.width/2, rect.y + rect.height/2);
                    
                    // 円の中にあるか判定
                    double distanceToCircle = Math.Sqrt(
                        Math.Pow(rectCenter.x - circleCenter.x, 2) + 
                        Math.Pow(rectCenter.y - circleCenter.y, 2));
                    
                    if (distanceToCircle < imageRadius)
                    {
                        // 白い点を検出
                        Debug.Log($"白い点を検出: 半径={radius:F1}");
                        pointDetected = true;
                        
                        // 円を赤く変更
                        renderer.material.color = Color.red;
                        break;
                    }
                }
                
                // このCircleで検出されたら次のCircleは処理しない
                if (pointDetected) break;
                
                // 検出されなかった場合は白に戻す
                renderer.material.color = Color.white;
            }
        }
        
        // メモリ解放
        hsvMat.release();
        mask.release();
        kernel.release();
        hierarchy.release();
        foreach (MatOfPoint contour in contours)
        {
            contour.release();
        }
    }
    
    private void OnDestroy()
    {
        // メモリ解放
        if (rgbaMat != null)
            rgbaMat.release();
    }
}