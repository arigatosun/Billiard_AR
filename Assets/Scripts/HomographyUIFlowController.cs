using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 手動キャリブレーションのUIフローを管理するサンプル。
/// シーン開始 → パネル表示「4隅をクリック→Computeボタン押してね」
/// → クリック完了後Compute → OKなら完了→パネル閉じる
/// </summary>
public class HomographyUIFlowController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private Text instructionText;
    [SerializeField] private Button computeButton;
    [SerializeField] private Button closeButton;

    [Header("Calibrator Reference")]
    [SerializeField] private BilliardHomographyCalibrator calibrator;

    // ★ 追加: RealWhiteBallPlacementManager への参照をInspectorで設定
    [Header("Placement Manager")]
    [SerializeField] private RealWhiteBallPlacementManager placementManager;

    private bool calibrationCompleted = false;

    void Start()
    {
        // 必要な参照のチェック
        if (placementManager == null)
        {
            Debug.LogError("RealWhiteBallPlacementManager reference is not set in HomographyUIFlowController!");
        }

        instructionPanel.SetActive(true);
        instructionText.text = "Step 1: Click on 4 corners of the table.\nStep 2: Press [Compute].";
        computeButton.interactable = true;
        closeButton.gameObject.SetActive(false);
        calibrationCompleted = false;
    }

    /// <summary>
    /// [Compute] ボタン押下
    /// </summary>
    public void OnComputeButtonClick()
    {
        calibrator.ComputeHomography();

        if (calibrator.IsHomographyReady())
        {
            instructionText.text = "Calibration Completed!\nPress [Close] to proceed.";
            calibrationCompleted = true;

            computeButton.interactable = false;
            closeButton.gameObject.SetActive(true);
        }
        else
        {
            instructionText.text = "Calibration Failed.\nPlease click 4 corners correctly.";
        }
    }

    /// <summary>
    /// [Close/OK] ボタン押下
    /// </summary>
    public void OnCloseButtonClick()
    {
        if (calibrationCompleted)
        {
            // パネルを閉じる
            instructionPanel.SetActive(false);

            // placementManagerのnullチェックを追加
            if (placementManager != null)
            {
                // (1) 一度だけ手玉を同期
                placementManager.OneTimeSyncWhiteBall();

                // (2) 物理シミュレーションを有効化
                placementManager.OnConfirmPlacement();

                // ここで好きに "continuousTracking = false/true" を制御してもよ
                // e.g. placementManager.continuousTracking = false;
            }
            else
            {
                Debug.LogError("RealWhiteBallPlacementManager reference is not set in HomographyUIFlowController!");
            }

            // 以降、ユーザーはキューで白球を撞ける
        }
        else
        {
            instructionText.text = "You must calibrate first!";
        }
    }
}
