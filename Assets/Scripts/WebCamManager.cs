using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// カメラ映像の取得と表示を管理するクラス
/// </summary>
public class WebCamManager : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private string _deviceName = "";
    [SerializeField] private int _requestedWidth = 1920;
    [SerializeField] private int _requestedHeight = 1080;
    [SerializeField] private int _requestedFPS = 30;
    [SerializeField] private bool _autoSelectFirstCamera = true;

    [Header("Display Settings")]
    [SerializeField] private RawImage _previewImage;
    [SerializeField] private bool _showPreview = true;
    [SerializeField] private RenderTexture _cameraRenderTexture;

    [Header("Debug")]
    [SerializeField] private Text _debugText;
    [SerializeField] private bool _showDebugInfo = true;

    private WebCamTexture _webCamTexture;
    private Color32[] _pixelBuffer;
    private bool _isInitialized = false;

    private void Start()
    {
        InitializeWebCam();
    }

    private void Update()
    {
        if (_isInitialized && _webCamTexture.isPlaying)
        {
            UpdateCameraTexture();

            if (_showDebugInfo && _debugText != null)
            {
                UpdateDebugInfo();
            }
        }
    }

    private void UpdateDebugInfo()
    {
        _debugText.text = $"Camera: {_webCamTexture.deviceName}\n" +
                          $"Resolution: {_webCamTexture.width}x{_webCamTexture.height}\n" +
                          $"FPS: {_webCamTexture.requestedFPS} (actual: ~{1.0f / Time.deltaTime:F1})\n" +
                          $"Rotation: {_webCamTexture.videoRotationAngle}°";
    }

    private void InitializeWebCam()
    {
        // 利用可能なカメラデバイスをログ出力
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("カメラデバイスが見つかりません。USB カメラが接続されているか確認してください。");
            return;
        }

        // 利用可能なデバイス一覧をログに出力
        Debug.Log($"利用可能なカメラデバイス: {devices.Length}台");
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"[{i}] {devices[i].name} ({(devices[i].isFrontFacing ? "前面" : "背面")})");
        }

        // デバイス名の選択（指定がなければ先頭を使用）
        string selectedDeviceName = _deviceName;
        if (string.IsNullOrEmpty(selectedDeviceName) && _autoSelectFirstCamera)
        {
            selectedDeviceName = devices[0].name;
            Debug.Log($"カメラデバイスを自動選択: {selectedDeviceName}");
        }

        // WebCamTexture インスタンス作成
        _webCamTexture = new WebCamTexture(
            selectedDeviceName,
            _requestedWidth,
            _requestedHeight,
            _requestedFPS
        );

        // テクスチャの設定
        if (_previewImage != null)
        {
            _previewImage.texture = _webCamTexture;
            _previewImage.gameObject.SetActive(_showPreview);
        }

        // カメラ起動
        _webCamTexture.Play();

        // 初期化完了フラグ
        _isInitialized = true;
        Debug.Log($"カメラ初期化完了: {_webCamTexture.width}x{_webCamTexture.height} @{_webCamTexture.requestedFPS}fps");

        // ピクセルバッファの初期化
        _pixelBuffer = new Color32[_webCamTexture.width * _webCamTexture.height];
    }

    private void UpdateCameraTexture()
    {
        // WebCamTexture の映像を RenderTexture にコピー（処理用）
        if (_cameraRenderTexture != null)
        {
            Graphics.Blit(_webCamTexture, _cameraRenderTexture);
        }

        // テクスチャのピクセルデータをバッファにコピー（画像処理用）
        if (_pixelBuffer != null && _pixelBuffer.Length == _webCamTexture.width * _webCamTexture.height)
        {
            _webCamTexture.GetPixels32(_pixelBuffer);
        }
    }

    /// <summary>
    /// 現在のカメラ映像のピクセルデータを取得します
    /// </summary>
    public Color32[] GetPixelData()
    {
        return _pixelBuffer;
    }

    /// <summary>
    /// 現在のカメラテクスチャを取得します
    /// </summary>
    public Texture GetCameraTexture()
    {
        return _webCamTexture;
    }

    /// <summary>
    /// カメラのレンダーテクスチャを取得します（画像処理用）
    /// </summary>
    public RenderTexture GetCameraRenderTexture()
    {
        return _cameraRenderTexture;
    }

    /// <summary>
    /// カメラの解像度を取得します
    /// </summary>
    public Vector2Int GetResolution()
    {
        if (_webCamTexture != null)
        {
            return new Vector2Int(_webCamTexture.width, _webCamTexture.height);
        }
        return Vector2Int.zero;
    }

    /// <summary>
    /// カメラの映像を回転する必要があるかどうかを取得します
    /// </summary>
    public bool NeedsRotation()
    {
        return _webCamTexture != null && _webCamTexture.videoRotationAngle != 0;
    }

    /// <summary>
    /// カメラ映像の回転角度を取得します
    /// </summary>
    public int GetRotationAngle()
    {
        return _webCamTexture != null ? _webCamTexture.videoRotationAngle : 0;
    }

    private void OnDestroy()
    {
        if (_webCamTexture != null)
        {
            _webCamTexture.Stop();
        }
    }
}