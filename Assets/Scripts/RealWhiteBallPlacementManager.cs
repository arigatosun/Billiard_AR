using System.Collections.Generic;
using UnityEngine;
using OpenCVForUnity.CoreModule; // for 'Rect2d'
using YOLOv8WithOpenCVForUnity;
using YOLOv8WithOpenCVForUnityExample;
using ibc;
using ibc.unity;
using ibc.objects;
using Unity.Mathematics;

public class RealWhiteBallPlacementManager : MonoBehaviour
{
    [Header("Billiard References")]
    [SerializeField] private Billiard billiard;
    [SerializeField] private int whiteBallId = 0;

    [Header("Detection & Homography")]
    [SerializeField] private YOLOv8ObjectDetectionExample yoloScript;
    [SerializeField] private BilliardHomographyCalibrator homographyCalibrator;

    private const int WHITE_BALL_CLASS_ID = 0;

    [SerializeField] private bool setManualModeOnStart = true;
    [SerializeField] private bool continuousTracking = false;
    [SerializeField] private float ballY = 0f;

    private bool calibrationCompleted = false;

    void Start()
    {
        if (setManualModeOnStart)
        {
            billiard.IsManualPlacementMode = true;
            Debug.Log("ManualPlacementMode ON at start.");
        }
        calibrationCompleted = false;
    }

    void Update()
    {
        // キャリブ未完了 or Homographyがまだならスキップ
        if (!homographyCalibrator.IsHomographyReady())
            return;

        // 連続追従しないならスキップ
        if (!continuousTracking)
            return;

        if (yoloScript == null) return;
        Mat resultsMat = yoloScript.GetLatestResults();
        if (resultsMat == null) return;

        var whiteBallRect = FindWhiteBallRectFromYOLO(resultsMat, WHITE_BALL_CLASS_ID);
        if (whiteBallRect.width <= 0 || whiteBallRect.height <= 0)
            return;

        float cx = (float)(whiteBallRect.x + whiteBallRect.width * 0.5f);
        float cy = (float)(whiteBallRect.y + whiteBallRect.height * 0.5f);

        // Debugログ
        Debug.Log($"[RealWhiteBallPlacementManager] (Update) White ball center = ({cx}, {cy}), " +
                  $"rect=({whiteBallRect.x},{whiteBallRect.y},{whiteBallRect.width},{whiteBallRect.height})");

        // RawImageのテクスチャサイズを確認
        if (homographyCalibrator != null &&
            homographyCalibrator.cameraRawImage != null &&  // ← publicになった
            homographyCalibrator.cameraRawImage.texture != null)
        {
            float texW = homographyCalibrator.cameraRawImage.texture.width;
            float texH = homographyCalibrator.cameraRawImage.texture.height;
            Debug.Log($"[RealWhiteBallPlacementManager] cameraRawImage.texture=({texW}x{texH})");

            if (cx < 0 || cy < 0 || cx > texW || cy > texH)
            {
                Debug.LogWarning("[RealWhiteBallPlacementManager] White ball center is out of texture range (Update)!");
            }
        }

        Vector2 uv = new Vector2(cx, cy);
        Vector2 xz = homographyCalibrator.ImageToTable(uv);
        Debug.Log($"[RealWhiteBallPlacementManager] (Update) uv=({uv.x},{uv.y}) => xz=({xz.x},{xz.y})");

        MatchWhiteBallPosition(xz.x, xz.y);
    }

    public void OneTimeSyncWhiteBall()
    {
        Debug.Log("OneTimeSyncWhiteBall called");

        if (!homographyCalibrator.IsHomographyReady())
        {
            Debug.LogWarning("[OneTimeSyncWhiteBall] Homography not ready.");
            return;
        }
        Debug.Log("[OneTimeSyncWhiteBall] Homography ready!");

        Mat resultsMat = yoloScript.GetLatestResults();
        if (resultsMat == null)
        {
            Debug.LogWarning("[OneTimeSyncWhiteBall] resultsMat is null.");
            return;
        }

        var whiteBallRect = FindWhiteBallRectFromYOLO(resultsMat, WHITE_BALL_CLASS_ID);

        float x2 = (float)(whiteBallRect.x + whiteBallRect.width);
        float y2 = (float)(whiteBallRect.y + whiteBallRect.height);
        Debug.Log($"[OneTimeSyncWhiteBall] rect=({whiteBallRect.x},{whiteBallRect.y},{whiteBallRect.width},{whiteBallRect.height}) => bottomRight=({x2},{y2})");

        if (whiteBallRect.width <= 0 || whiteBallRect.height <= 0)
        {
            Debug.LogWarning("[OneTimeSyncWhiteBall] No valid white ball detected. Aborting.");
            return;
        }

        float cx = (float)(whiteBallRect.x + whiteBallRect.width * 0.5f);
        float cy = (float)(whiteBallRect.y + whiteBallRect.height * 0.5f);
        Debug.Log($"[OneTimeSyncWhiteBall] White ball center = ({cx}, {cy})");

        // ★ RawImageのテクスチャサイズをチェック
        if (homographyCalibrator != null &&
            homographyCalibrator.cameraRawImage != null &&  // ← publicになった
            homographyCalibrator.cameraRawImage.texture != null)
        {
            float texW = homographyCalibrator.cameraRawImage.texture.width;
            float texH = homographyCalibrator.cameraRawImage.texture.height;
            Debug.Log($"[OneTimeSyncWhiteBall] cameraRawImage.texture=({texW}x{texH})");

            if (cx < 0 || cy < 0 || cx > texW || cy > texH)
            {
                Debug.LogWarning("[OneTimeSyncWhiteBall] White ball center is out of RawImage texture range!");
                // ここでスケーリングが必要か検討
            }
        }

        Vector2 uv = new Vector2(cx, cy);
        Debug.Log($"[OneTimeSyncWhiteBall] uv=({uv.x},{uv.y})");

        Vector2 xz = homographyCalibrator.ImageToTable(uv);
        Debug.Log($"[OneTimeSyncWhiteBall] xz=({xz.x},{xz.y})");

        MatchWhiteBallPosition(xz.x, xz.y);
        Debug.Log("[OneTimeSyncWhiteBall] White ball position matched successfully!");
    }

    private Rect2d FindWhiteBallRectFromYOLO(Mat results, int targetClassId)
    {
        int rows = results.rows();
        float bestConf = 0f;
        Rect2d bestRect = new Rect2d(0, 0, 0, 0);

        for (int i = 0; i < rows; i++)
        {
            float[] row = new float[7];
            results.get(i, 0, row);

            float x1 = row[0]; // left
            float y1 = row[1]; // top
            float x2 = row[2]; // right
            float y2 = row[3]; // bottom
            float conf = row[4];
            float classId = row[5];

            if ((int)classId == targetClassId && conf > 0.2f)
            {
                // 幅と高さを x2 - x1, y2 - y1 で計算
                float w = x2 - x1;
                float h = y2 - y1;

                if (conf > bestConf)
                {
                    bestConf = conf;

                    // (x1, y1, w, h)
                    bestRect = new Rect2d(x1, y1, w, h);
                }
            }
        }
        return bestRect;
    }


    private void MatchWhiteBallPosition(float x, float z)
    {
        UnityBall whiteBallObj = GetWhiteBallObject();
        if (whiteBallObj == null) return;

        Vector3 newPos = new Vector3(x, ballY, z);
        whiteBallObj.transform.position = newPos;

        Ball physBall = billiard.State.GetPhysicsBall(whiteBallId);
        physBall.Position = new double3(x, ballY, z);
        physBall.Velocity = double3.zero;
        physBall.AngularVelocity = double3.zero;
        physBall.Motion = Ball.MotionType.Stationary;
        physBall.State = Ball.StateType.Normal;

        billiard.State.SetPhysicsBall(physBall);
    }

    private UnityBall GetWhiteBallObject()
    {
        UnityBall selected = billiard.SelectedBall;
        if (selected != null && selected.Identifier == whiteBallId)
        {
            return selected;
        }
        else
        {
            BilliardUnityScene unityScene = new BilliardUnityScene()
            {
                Balls = FindObjectsOfType<UnityBall>(),
                Holes = FindObjectsOfType<UnityHole>(),
                Cushions = FindObjectsOfType<UnityCushion>(),
                Polygons = FindObjectsOfType<UnityPolygon>(),
                Cues = FindObjectsOfType<UnityCue>()
            };
            foreach (var ub in unityScene.Balls)
            {
                if (ub.Identifier == whiteBallId)
                {
                    return ub;
                }
            }
        }
        return null;
    }

    public void OnConfirmPlacement()
    {
        billiard.IsManualPlacementMode = false;
        Debug.Log("ManualPlacementMode OFF. Now the ball can be shot with the existing system.");
    }

    public void OnCalibrationComplete()
    {
        Debug.Log("Calibration done. Ready to track or place the ball now!");
        calibrationCompleted = true;
    }
}
