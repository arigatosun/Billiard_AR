using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// OpenCV for Unity の必要な名前空間
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.ImgprocModule;

public class BilliardHomographyCalibrator : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private RawImage cameraRawImage;  // カメラ映像表示用
    [SerializeField] private Text infoText;            // クリック情報や結果表示（任意）

    [Header("Table Corners in Scene (x,z)")]
    // テーブル四隅のシーン座標(メートル単位等)
    [SerializeField]
    private Vector2[] tableCorners = new Vector2[]
    {
        new Vector2(-1.081f, -0.518f), // 左下
        new Vector2( 1.081f, -0.518f), // 右下
        new Vector2( 1.081f,  0.518f), // 右上
        new Vector2(-1.081f,  0.518f), // 左上
    };

    // ユーザークリックで集めた (u,v)
    private List<Vector2> clickedUVs = new List<Vector2>();

    // Homography 行列 (3x3)
    private Mat homographyMat = null;
    private bool isHomographyReady = false;

    // Start() メソッドを追加してデバッグログを出力
    private void Start()
    {
        if (cameraRawImage == null)
        {
            Debug.LogError("Camera RawImage is not assigned!");
            return;
        }

        Debug.Log($"Calibrator initialized. RawImage size: {cameraRawImage.rectTransform.rect.size}");
    }

    // IPointerClickHandler: RawImage 上のクリックを拾う
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("Click detected at: " + eventData.position); // この行を追加

        if (!cameraRawImage || cameraRawImage.texture == null)
        {
            Debug.LogError("RawImage or Texture is missing!");
            return;
        }

        // 1) RawImageのRectTransform座標を得る
        RectTransform rt = cameraRawImage.GetComponent<RectTransform>();
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt,
            eventData.position, eventData.pressEventCamera, out localPoint))
        {
            return; // クリックがRawImage範囲外
        }

        // 2) localPointはRawImage中心が(0,0)
        float w = rt.rect.width;
        float h = rt.rect.height;
        float px = localPoint.x + w * 0.5f;
        float py = localPoint.y + h * 0.5f;

        // 3) Textureピクセル座標に変換
        Texture tex = cameraRawImage.texture;
        float texW = tex.width;
        float texH = tex.height;

        float u = Mathf.Clamp(px / w * texW, 0, texW - 1);
        float v = Mathf.Clamp(py / h * texH, 0, texH - 1);

        Vector2 uv = new Vector2(u, v);
        clickedUVs.Add(uv);

        // 情報表示
        if (infoText)
        {
            infoText.text = $"Clicked Corner {clickedUVs.Count}: (u={uv.x:F1}, v={uv.y:F1})";
        }

        Debug.Log($"Clicked corner {clickedUVs.Count} => {uv}");
    }

    // ボタン等から呼ぶ想定: ホモグラフィ計算
    public void ComputeHomography()
    {
        if (clickedUVs.Count < 4)
        {
            Debug.LogError("Need 4 corners to compute homography!");
            return;
        }
        if (tableCorners.Length < 4)
        {
            Debug.LogError("tableCorners is not set properly!");
            return;
        }

        // 1) (u,v) -> MatOfPoint2f
        Point[] srcPointsArr = new Point[4];
        for (int i = 0; i < 4; i++)
        {
            Vector2 uv = clickedUVs[i];
            srcPointsArr[i] = new Point(uv.x, uv.y);
        }
        MatOfPoint2f srcMat = new MatOfPoint2f(srcPointsArr);

        // 2) (x,z) -> MatOfPoint2f
        Point[] dstPointsArr = new Point[4];
        for (int i = 0; i < 4; i++)
        {
            Vector2 sc = tableCorners[i];
            dstPointsArr[i] = new Point(sc.x, sc.y);
        }
        MatOfPoint2f dstMat = new MatOfPoint2f(dstPointsArr);

        // 3) findHomography with RANSAC
        homographyMat = Calib3d.findHomography(srcMat, dstMat, Calib3d.RANSAC, 5.0);

        if (homographyMat == null || homographyMat.empty())
        {
            Debug.LogError("Homography calculation failed.");
            isHomographyReady = false;
        }
        else
        {
            isHomographyReady = true;
            Debug.Log("Homography found:\n" + homographyMat.dump());
            if (infoText)
            {
                infoText.text = "Homography Computed!";
            }
        }
    }

    // 計算したHomographyで (u,v) -> (x,z) を変換
    public Vector2 ImageToTable(Vector2 uv)
    {
        if (!isHomographyReady || homographyMat == null || homographyMat.empty())
        {
            Debug.LogWarning("Homography not ready.");
            return Vector2.zero;
        }

        // 1) 入力をMatOfPoint2f化
        MatOfPoint2f srcPt = new MatOfPoint2f(new Point(uv.x, uv.y));
        MatOfPoint2f dstPt = new MatOfPoint2f();

        // 2) 透視変換
        Core.perspectiveTransform(srcPt, dstPt, homographyMat);
        Point[] outPts = dstPt.toArray();

        // (x,z)を返す
        return new Vector2((float)outPts[0].x, (float)outPts[0].y);
    }
}
