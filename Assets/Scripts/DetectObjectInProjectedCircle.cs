using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// OpenCV for Unityの名前空間
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;

// KlakNDIの名前空間を追加
using Klak.Ndi;

public class DetectObjectInProjectedCircle : MonoBehaviour
{
    [Header("カメラ映像を表示するQuad")]
    public GameObject displayQuad;  // RawImageの代わりにQuadを使用

    [Header("使用するカメラのデバイスインデックス")]
    [Tooltip("0:OBS NDI, 1以上:Webカメラ")]
    public int deviceIndex = 1;

    [Header("NDI設定")]
    public string ndiSourceName = "OBS"; // NDIソース名
    public NdiReceiver ndiReceiver; // NDIレシーバーコンポーネント

    [Header("カメラ映像上での円の中心(x, y) [ピクセル単位]")]
    public Vector2 circleCenter = new Vector2(320, 240);

    [Header("円の半径(ピクセル単位)")]
    public float circleRadius = 100f;

    [Header("差分のしきい値 (0~255)")]
    public float diffThreshold = 30f;

    [Header("差分領域の割合しきい値(0~1)")]
    public float areaThreshold = 0.1f;

    [Header("検出用の円オブジェクト")]
    private GameObject whiteCircle;  // privateに変更し、インスペクターから非表示に

    private WebCamTexture webCamTexture;   // Unity標準のWebCam映像
    private Mat backgroundMat;             // 背景キャプチャ用
    private Mat currentMat;                // 毎フレームのWebCam映像
    private Mat diffMat;                   // 差分結果用
    private Texture2D outputTexture;       // 画面表示用に変換するためのテクスチャ
    
    private bool usingNdi = false;         // NDIを使用中かどうか
    private Texture ndiTexture;            // NDIからの映像

    [Header("ビリヤード制御")]
    public ibc.controller.CueController cueController; // CueControllerへの参照

    [Header("角度制御設定")]
    public float jawAngleOffset = 0f; // 角度調整用のオフセット
    public bool invertJawAngle = false; // 角度を反転するかどうか

    private float lastSetJawAngle = 0f;

    void Start()
    {
        if (displayQuad == null)
        {
            Debug.LogError("displayQuadが設定されていません。インスペクターで設定してください。");
            return;
        }

        // インデックス0の場合はNDIを使用、それ以外はWebカメラを使用
        if (deviceIndex == 0)
        {
            SetupNdiCamera();
        }
        else
        {
            SetupWebCamera();
        }
        
        // カメラが開始するまで待機
        Invoke(nameof(InitializeMats), 0.5f);
        
        // Whiteタグの付いた円オブジェクトを探す
        StartCoroutine(FindWhiteCircle());
    }

    private void SetupNdiCamera()
    {
        usingNdi = true;
        
        // NDIレシーバーの確認
        if (ndiReceiver == null)
        {
            Debug.LogError("NDI Receiverコンポーネントが設定されていません。インスペクターで設定してください。");
            return;
        }
        
        // NDIレシーバーの設定
        ndiReceiver.ndiName = ndiSourceName;
        Debug.Log("NDIカメラをセットアップしました。ソース名: " + ndiSourceName);
    }

    private void SetupWebCamera()
    {
        usingNdi = false;
        
        // 利用可能なWebカメラの一覧を取得
        WebCamDevice[] devices = WebCamTexture.devices;
        
        if (devices.Length == 0)
        {
            Debug.LogError("Webカメラが接続されていません。");
            return;
        }

        // デバイスインデックスの範囲チェック
        if (deviceIndex >= devices.Length)
        {
            Debug.LogWarning($"指定されたデバイスインデックス({deviceIndex})が範囲外です。インデックス1のカメラを使用します。");
            deviceIndex = 1;
            
            // インデックス1も範囲外の場合はインデックス0を使用
            if (deviceIndex >= devices.Length)
            {
                deviceIndex = 0;
            }
        }

        // 指定されたインデックスのカメラで初期化
        webCamTexture = new WebCamTexture(devices[deviceIndex].name);
        webCamTexture.Play();

        // QuadのマテリアルにWebCamTextureを設定
        MeshRenderer quadRenderer = displayQuad.GetComponent<MeshRenderer>();
        Material quadMaterial = quadRenderer.material;
        quadMaterial.mainTexture = webCamTexture;
    }

    private void InitializeMats()
    {
        if (usingNdi)
        {
            // NDIの場合、テクスチャを取得
            if (ndiReceiver != null && ndiReceiver.texture != null)
            {
                ndiTexture = ndiReceiver.texture;
                int width = ndiTexture.width;
                int height = ndiTexture.height;
                
                // NDIテクスチャをQuadに設定
                MeshRenderer quadRenderer = displayQuad.GetComponent<MeshRenderer>();
                Material quadMaterial = quadRenderer.material;
                quadMaterial.mainTexture = ndiTexture;
                
                // MatsをNDIテクスチャのサイズで初期化
                currentMat = new Mat(height, width, CvType.CV_8UC4);
                diffMat = new Mat(height, width, CvType.CV_8UC1);
                
                // 出力表示用Texture2Dを作成
                outputTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                
                // 数秒後に「背景」をキャプチャ
                Invoke(nameof(CaptureBackground), 2f);
            }
            else
            {
                // NDIテクスチャがまだ準備できていない場合は再試行
                Invoke(nameof(InitializeMats), 0.5f);
            }
        }
        else
        {
            // Webカメラの場合
            if (!webCamTexture.isPlaying || webCamTexture.width <= 16)
            {
                // カメラがまだ準備できていない場合は再試行
                Invoke(nameof(InitializeMats), 0.5f);
                return;
            }

            // Webカメラの実際の解像度に合わせてMatを初期化
            currentMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);
            diffMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC1);

            // 出力表示用Texture2Dを作成
            outputTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);

            // 数秒後に「背景」をキャプチャ
            Invoke(nameof(CaptureBackground), 2f);
        }
    }

    void Update()
    {
        if (currentMat == null || displayQuad == null) return;

        // NDIかWebCamかで処理を分岐
        if (usingNdi)
        {
            UpdateWithNdi();
        }
        else
        {
            UpdateWithWebCam();
        }
    }

    private void UpdateWithNdi()
    {
        if (ndiReceiver != null && ndiReceiver.texture != null)
        {
            // NDIテクスチャを取得
            ndiTexture = ndiReceiver.texture;
            
            // テクスチャをQuadに直接表示（処理前の映像）
            MeshRenderer quadRenderer = displayQuad.GetComponent<MeshRenderer>();
            if (quadRenderer != null)
            {
                Material quadMaterial = quadRenderer.material;
                quadMaterial.mainTexture = ndiTexture;
            }
            
            // NDIテクスチャを処理用のMatに変換
            try
            {
                // RenderTextureの場合はアクティブにしてからピクセルを読み取る
                if (ndiTexture is RenderTexture rt)
                {
                    // 既存のoutputTextureサイズを確認・更新
                    if (outputTexture == null || outputTexture.width != rt.width || outputTexture.height != rt.height)
                    {
                        if (outputTexture != null) Destroy(outputTexture);
                        outputTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                        
                        // Matも再初期化
                        if (currentMat != null) currentMat.release();
                        currentMat = new Mat(rt.height, rt.width, CvType.CV_8UC4);
                    }
                    
                    // アクティブなRenderTextureを一時保存
                    RenderTexture prevRT = RenderTexture.active;
                    RenderTexture.active = rt;
                    
                    // テクスチャの左下から読み取り（OpenGLの座標系）
                    outputTexture.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0);
                    outputTexture.Apply();
                    
                    // 元のRenderTextureに戻す
                    RenderTexture.active = prevRT;
                    
                    // Texture2DをMatに変換
                    Utils.texture2DToMat(outputTexture, currentMat);
                    
                    // 画像処理
                    // 注意: 白色検出に時間がかかるため、フレームレートに影響を与える可能性あり
                    ProcessCurrentFrame();
                }
                else
                {
                    Debug.LogWarning("NDIテクスチャがRenderTextureではありません");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("NDIテクスチャ処理中にエラーが発生しました: " + e.Message);
            }
        }
    }

    private void UpdateWithWebCam()
    {
        if (webCamTexture == null) return;

        if (webCamTexture.didUpdateThisFrame)
        {
            // WebCamTexture → Mat へ変換
            Utils.webCamTextureToMat(webCamTexture, currentMat);
            
            // 画像処理
            ProcessCurrentFrame();
        }
    }

    private void ProcessCurrentFrame()
    {
        // 色検出処理
        Mat hsvMat = new Mat();
        Imgproc.cvtColor(currentMat, hsvMat, Imgproc.COLOR_RGBA2BGR);
        Imgproc.cvtColor(hsvMat, hsvMat, Imgproc.COLOR_BGR2HSV);

        // 白色の範囲を定義（HSV色空間）- 検出範囲を拡大
        Scalar lowerWhite = new Scalar(0, 0, 150);  // 明度の閾値を下げる
        Scalar upperWhite = new Scalar(180, 60, 255); // 彩度の上限を上げる

        // 白色領域のマスクを作成
        Mat whiteMask = new Mat();
        Core.inRange(hsvMat, lowerWhite, upperWhite, whiteMask);

        // デバッグ用：マスクを表示して検出状況を確認
        Mat debugMat = new Mat();
        currentMat.copyTo(debugMat);
        Imgproc.circle(debugMat, new Point(circleCenter.x, circleCenter.y), (int)circleRadius, new Scalar(0, 255, 0, 255), 2);

        // 円領域内の白色ピクセルの割合を計算し、白い物体の位置も取得
        Vector2 detectedPosition;
        float whiteRatio = ComputeDiffInCircle(whiteMask, circleCenter, circleRadius, out detectedPosition);

        // デバッグログを削除（負荷軽減）
        // Debug.Log($"白色の検出状態: ratio={whiteRatio:F3}, threshold={areaThreshold:F3}, 位置=({detectedPosition.x:F1}, {detectedPosition.y:F1})");

        // 一定割合を超えたら「円の中に白い物体がある」と判定
        if (whiteRatio > areaThreshold)
        {
            // ログ頻度を抑える（毎フレーム出力しない）
            if (Time.frameCount % 30 == 0)
            {
                Debug.Log($"円の中に白い物体が入りました。 whiteRatio = {whiteRatio:F3}");
            }
            
            // 検出された位置からCueControllerのJaw角度を設定
            if (cueController != null)
            {
                // 円の中心から見た物体の相対位置
                Vector2 relativePos = detectedPosition - circleCenter;
                
                // 角度を計算
                float angleInRadians = Mathf.Atan2(relativePos.y, relativePos.x);
                float angleInDegrees = angleInRadians * Mathf.Rad2Deg;
                
                // 必要に応じて角度を調整
                float jawAngle = angleInDegrees + jawAngleOffset;
                if (invertJawAngle) jawAngle = -jawAngle;
                
                // 前回設定した角度と比較して大きく変わった場合のみ更新（チャタリング防止）
                if (Mathf.Abs(lastSetJawAngle - jawAngle) > 5.0f) // 閾値を5度に増加
                {
                    lastSetJawAngle = jawAngle;
                    
                    try
                    {
                        // CueControllerに角度を設定
                        cueController.SetJaw(jawAngle);
                        
                        // 更新の頻度を制限（毎フレーム更新しない）
                        if (Time.frameCount % 3 == 0) // 3フレームに1回だけ更新
                        {
                            // 軌道更新を遅延実行
                            StartCoroutine(DelayedUpdateTrigger());
                        }
                        
                        // ログ頻度を抑える
                        if (Time.frameCount % 30 == 0)
                        {
                            Debug.Log($"キューの角度を {jawAngle} 度に設定しました");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"キュー角度設定中にエラー: {e.Message}");
                    }
                }
            }
            
            // 検出を視覚化するために円の色を変更
            if (whiteCircle != null)
            {
                Renderer renderer = whiteCircle.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.red; // 検出時に赤色にする
                    // 検出時にサイズを少し大きくする（注目効果）
                    float currentScale = circleRadius / 100f;
                    whiteCircle.transform.localScale = new Vector3(currentScale * 1.1f, currentScale * 1.1f, 1f);
                }
            }
        }
        else if (whiteCircle != null)
        {
            // 非検出時は元の色に戻す
            Renderer renderer = whiteCircle.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.white;
                // 通常のサイズに戻す
                float currentScale = circleRadius / 100f;
                whiteCircle.transform.localScale = new Vector3(currentScale, currentScale, 1f);
            }
        }

        // 円オブジェクトの位置を更新
        if (whiteCircle != null)
        {
            try
            {
                // カメラ映像の座標からワールド座標に変換
                Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(circleCenter.x, circleCenter.y, 10f));
                whiteCircle.transform.position = new Vector3(worldPosition.x, worldPosition.y, whiteCircle.transform.position.z);
                
                // 円のスケールも半径に合わせて調整
                float scaleFactor = circleRadius / 100f;
                whiteCircle.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"円の位置更新中にエラー: {e.Message}");
            }
        }

        // カメラ映像をテクスチャに変換して表示
        try
        {
            // 通常表示
            Utils.matToTexture2D(currentMat, outputTexture);

            // Quadのマテリアルに表示
            MeshRenderer quadRenderer = displayQuad.GetComponent<MeshRenderer>();
            if (quadRenderer != null)
            {
                Material quadMaterial = quadRenderer.material;
                quadMaterial.mainTexture = outputTexture;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"テクスチャ表示中にエラー: {e.Message}");
        }

        // リソースの解放（重要：メモリリーク防止）
        try
        {
            if (hsvMat != null && !hsvMat.empty()) hsvMat.release();
            if (whiteMask != null && !whiteMask.empty()) whiteMask.release();
            if (debugMat != null && !debugMat.empty()) debugMat.release();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"リソース解放中にエラー: {e.Message}");
        }
    }

    /// <summary>
    /// 現在の映像を「背景」としてキャプチャ
    /// （投影円内に何も置いていない状態で呼び出す）
    /// </summary>
    void CaptureBackground()
    {
        if (currentMat == null || currentMat.empty())
        {
            Debug.LogWarning("現在のカメラ映像が未初期化です。背景キャプチャをリトライします。");
            Invoke(nameof(CaptureBackground), 1f);
            return;
        }

        backgroundMat = currentMat.clone();
        Debug.Log("背景キャプチャ完了");
    }

    /// <summary>
    /// 差分マスク(diffMat) のうち、円領域のピクセルをカウントし、
    /// 差分(白ピクセル)の割合を返す。また白い物体の位置も計算する
    /// </summary>
    float ComputeDiffInCircle(Mat diffMat, Vector2 center, float radius, out Vector2 objectPosition)
    {
        int width = diffMat.width();
        int height = diffMat.height();
        
        // 初期値は円の中心
        objectPosition = center;

        // 安全対策
        if (radius <= 0f) return 0f;
        if (center.x < 0 || center.y < 0 || center.x >= width || center.y >= height) return 0f;

        int diffCount = 0;
        int totalCount = 0;
        float sumX = 0, sumY = 0; // 白いピクセルの重心計算用

        // 円領域だけを処理するための範囲を計算（効率化）
        int startX = Mathf.Max(0, (int)(center.x - radius));
        int endX = Mathf.Min(width - 1, (int)(center.x + radius));
        int startY = Mathf.Max(0, (int)(center.y - radius));
        int endY = Mathf.Min(height - 1, (int)(center.y + radius));

        try
        {
            // 画像データに直接アクセス
            byte[] pixels = new byte[width * height];
            diffMat.get(0, 0, pixels);

            float radiusSq = radius * radius;

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float distSq = dx*dx + dy*dy;
                    
                    if (distSq <= radiusSq)
                    {
                        totalCount++;
                        int index = y * width + x;
                        if (index >= 0 && index < pixels.Length && pixels[index] > 100) // 安全チェック追加
                        {
                            diffCount++;
                            sumX += x; // 白いピクセルのX座標を累積
                            sumY += y; // 白いピクセルのY座標を累積
                        }
                    }
                }
            }

            if (diffCount > 0)
            {
                // 白いピクセルの重心を計算
                objectPosition = new Vector2(sumX / diffCount, sumY / diffCount);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"白色検出処理中にエラー: {e.Message}");
            return 0f; // エラー時は検出なしとする
        }

        // デバッグログ削除（パフォーマンス向上）
        // Debug.Log($"検出情報: 白ピクセル数={diffCount}, 合計ピクセル数={totalCount}, 比率={(float)diffCount/totalCount:F3}");

        if (totalCount == 0) return 0f;
        return (float)diffCount / totalCount;
    }

    private IEnumerator FindWhiteCircle()
    {
        while (true)
        {
            // Whiteタグの付いたオブジェクトを探す
            GameObject foundCircle = GameObject.FindGameObjectWithTag("White");
            if (foundCircle != null)
            {
                whiteCircle = foundCircle;
                Debug.Log("Whiteタグの付いた円オブジェクトが見つかりました: " + foundCircle.name);
                break;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void OnDestroy()
    {
        // リソースの解放
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }
        
        if (currentMat != null)
        {
            currentMat.release();
        }
        
        if (diffMat != null)
        {
            diffMat.release();
        }
        
        if (backgroundMat != null)
        {
            backgroundMat.release();
        }
    }

    private IEnumerator DelayedUpdateTrigger()
    {
        // 次のフレームを待つ
        yield return new WaitForSeconds(0.05f); // 50ミリ秒待機
        
        // 更新を強制的にトリガー
        if (cueController != null && cueController.OnStrikeCommandChange != null)
        {
            try
            {
                cueController.OnStrikeCommandChange.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"軌道更新中にエラー: {e.Message}");
            }
        }
    }
}