using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.UnityUtils;
using System.Collections.Generic;

public class VirtualTableTest : MonoBehaviour
{
    [Header("Set your main camera here")]
    public Camera targetCamera;       // InspectorでMainCameraなどを設定

    [Header("Virtual Table")]
    public Transform virtualTable;    // Planeなどを配置、(2.54m x 1.27m)を想定

    // 仮に: solvePnP結果を手動で入力するサンプル (テスト用)
    [Header("Pose Data (example)")]
    public Vector3 tvecExample = new Vector3(0, 0, 1.0f);
    public Vector3 rvecExample = new Vector3(0, 0, 0);

    void Start()
    {
        // Planeを (2.54,1,1.27)に拡大し、(0,0,0)に置くなど設定しておく想定
        Debug.Log("VirtualTableTest: Ready");
    }

    void Update()
    {
        // 例: 毎フレーム rvecExample, tvecExample を反映 (本来は solvePnP結果を受け取る)
        ApplyPoseToCamera(rvecExample, tvecExample);
    }

    /// <summary>
    /// 回転ベクトル + 並進ベクトルを カメラtransformに適用
    /// </summary>
    /// <param name="rvecVal">回転ベクトル(ロドリゲス)を (x,y,z) で入れる</param>
    /// <param name="tvecVal">並進ベクトル(x,y,z)</param>
    private void ApplyPoseToCamera(Vector3 rvecVal, Vector3 tvecVal)
    {
        if (targetCamera == null) return;

        // 1) rvecをCV_64FC1で作る
        Mat rvec = new Mat(3, 1, CvType.CV_64FC1);
        rvec.put(0, 0, rvecVal.x, rvecVal.y, rvecVal.z);

        // 2) Rodrigues (double型行列)
        Mat rotationMat = new Mat();
        Calib3d.Rodrigues(rvec, rotationMat);

        // 3) double[]で取り出す
        double[] rDataDouble = new double[9];
        rotationMat.get(0, 0, rDataDouble);

        // 4) double→floatキャスト
        float[] rDataFloat = new float[9];
        for (int i = 0; i < 9; i++)
        {
            rDataFloat[i] = (float)rDataDouble[i];
        }

        // 行列→Quaternion
        Quaternion q = MatrixToQuaternion(rDataFloat);

        // 5) tvec
        Vector3 pos = tvecVal;

        // 6) transformに適用
        targetCamera.transform.localPosition = pos;
        targetCamera.transform.localRotation = q;
    }


    // 行列9要素→Quaternion
    private Quaternion MatrixToQuaternion(float[] m)
    {
        // m= [m00, m01, m02, m10, m11, m12, m20, m21, m22]
        float trace = m[0] + m[4] + m[8];
        if (trace > 0f)
        {
            float s = 0.5f / Mathf.Sqrt(trace + 1f);
            float w = 0.25f / s;
            float x = (m[5] - m[7]) * s;
            float y = (m[6] - m[2]) * s;
            float z = (m[1] - m[3]) * s;
            return new Quaternion(x, y, z, w);
        }
        else
        {
            if (m[0] > m[4] && m[0] > m[8])
            {
                float s = 2f * Mathf.Sqrt(1f + m[0] - m[4] - m[8]);
                float w = (m[5] - m[7]) / s;
                float x = 0.25f * s;
                float y = (m[1] + m[3]) / s;
                float z = (m[6] + m[2]) / s;
                return new Quaternion(x, y, z, w);
            }
            else if (m[4] > m[8])
            {
                float s = 2f * Mathf.Sqrt(1f + m[4] - m[0] - m[8]);
                float w = (m[6] - m[2]) / s;
                float x = (m[1] + m[3]) / s;
                float y = 0.25f * s;
                float z = (m[5] + m[7]) / s;
                return new Quaternion(x, y, z, w);
            }
            else
            {
                float s = 2f * Mathf.Sqrt(1f + m[8] - m[0] - m[4]);
                float w = (m[1] - m[3]) / s;
                float x = (m[6] + m[2]) / s;
                float y = (m[5] + m[7]) / s;
                float z = 0.25f * s;
                return new Quaternion(x, y, z, w);
            }
        }
    }
}
