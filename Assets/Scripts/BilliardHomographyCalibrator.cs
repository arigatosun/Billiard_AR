using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.ImgprocModule;

public class BilliardHomographyCalibrator : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    // ★ private → public に変更
    [SerializeField] public RawImage cameraRawImage;  // ← ここが変更点
    [SerializeField] private Text infoText;

    [Header("Table Corners in Scene (x,z)")]
    [SerializeField]
    private Vector2[] tableCorners = new Vector2[]
    {
        new Vector2(1.072f, 0.526f), // 左下
        new Vector2( -1.072f, 0.526f), // 右下
        new Vector2( -1.072f,  -0.526f), // 右上
        new Vector2(1.072f,   -0.526f), // 左上
    };

    // ユーザークリックで集めた (u,v)
    private List<Vector2> clickedUVs = new List<Vector2>();

    // Homography 行列 (3x3)
    private Mat homographyMat = null;
    private bool isHomographyReady = false;

    private void Start()
    {
        if (cameraRawImage == null)
        {
            Debug.LogError("Camera RawImage is not assigned!");
            return;
        }

        Debug.Log($"Calibrator initialized. RawImage size: {cameraRawImage.rectTransform.rect.size}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("Click detected at: " + eventData.position);

        if (!cameraRawImage || cameraRawImage.texture == null)
        {
            Debug.LogError("RawImage or Texture is missing!");
            return;
        }

        RectTransform rt = cameraRawImage.GetComponent<RectTransform>();
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint))
        {
            return; // クリックがRawImage範囲外
        }

        float w = rt.rect.width;
        float h = rt.rect.height;
        float px = localPoint.x + w * 0.5f;
        float py = localPoint.y + h * 0.5f;

        Texture tex = cameraRawImage.texture;
        float texW = tex.width;
        float texH = tex.height;

        float u = Mathf.Clamp(px / w * texW, 0, texW - 1);
        float v = Mathf.Clamp(py / h * texH, 0, texH - 1);

        Vector2 uv = new Vector2(u, v);
        clickedUVs.Add(uv);

        if (infoText)
        {
            infoText.text = $"Clicked Corner {clickedUVs.Count}: (u={uv.x:F1}, v={uv.y:F1})";
        }

        Debug.Log($"Clicked corner {clickedUVs.Count} => {uv}");
    }

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

        // (u,v) -> MatOfPoint2f
        Point[] srcPointsArr = new Point[4];
        for (int i = 0; i < 4; i++)
        {
            Vector2 uv = clickedUVs[i];
            srcPointsArr[i] = new Point(uv.x, uv.y);
        }
        MatOfPoint2f srcMat = new MatOfPoint2f(srcPointsArr);

        // (x,z) -> MatOfPoint2f
        Point[] dstPointsArr = new Point[4];
        for (int i = 0; i < 4; i++)
        {
            Vector2 sc = tableCorners[i];
            dstPointsArr[i] = new Point(sc.x, sc.y);
        }
        MatOfPoint2f dstMat = new MatOfPoint2f(dstPointsArr);

        // findHomography
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

    public Vector2 ImageToTable(Vector2 uv)
    {
        if (!isHomographyReady || homographyMat == null || homographyMat.empty())
        {
            Debug.LogWarning("Homography not ready.");
            return Vector2.zero;
        }

        MatOfPoint2f srcPt = new MatOfPoint2f(new Point(uv.x, uv.y));
        MatOfPoint2f dstPt = new MatOfPoint2f();

        Core.perspectiveTransform(srcPt, dstPt, homographyMat);
        Point[] outPts = dstPt.toArray();

        return new Vector2((float)outPts[0].x, (float)outPts[0].y);
    }

    public bool IsHomographyReady()
    {
        return isHomographyReady;
    }
}
