using UnityEngine;
using System.Linq;
using Klak.Ndi;

/// <summary>
/// NDIソースからの映像を受信してQuadに表示するコンポーネント
/// </summary>
public class NDIReceiver : MonoBehaviour
{
    [Header("NDI設定")]
    [Tooltip("NDIソース名（空の場合は最初に見つかったソースを使用）")]
    public string sourceName = "";

    [Header("表示設定")]
    [Tooltip("映像を表示するQuad")]
    public GameObject targetQuad;
    
    [Tooltip("RenderTextureの解像度")]
    public Vector2Int resolution = new Vector2Int(1920, 1080);
    
    [Header("デバッグ")]
    [Tooltip("デバッグログを表示")]
    public bool showDebugLogs = true;

    // NDIコンポーネント
    private NdiReceiver _receiver;
    private Material _material;
    private Renderer _renderer;
    private RenderTexture _renderTexture;
    private bool _initialized = false;
    private bool _connectionLogged = false;

    void Awake()
    {
        DebugLog("Awake: NDIReceiver初期化開始");
        
        try
        {
            // RenderTextureを事前に作成
            _renderTexture = new RenderTexture(resolution.x, resolution.y, 0);
            _renderTexture.Create();
            DebugLog($"RenderTexture作成: {resolution.x}x{resolution.y}");
            
            // NDIレシーバーを作成
            _receiver = gameObject.AddComponent<NdiReceiver>();
            DebugLog("NDIレシーバーコンポーネント追加");
            
            // 初期設定
            _receiver.targetTexture = _renderTexture;
            DebugLog("NDIレシーバーにRenderTextureを設定");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"NDIReceiver初期化エラー: {e.Message}\n{e.StackTrace}");
        }
    }

    void Start()
    {
        DebugLog("Start: NDIReceiver設定開始");
        
        try
        {
            // ターゲットQuadが指定されていない場合は、このGameObjectを使用
            if (targetQuad == null)
            {
                targetQuad = this.gameObject;
                DebugLog("ターゲットQuadが未指定のため、このGameObjectを使用");
            }

            // レンダラーを取得
            _renderer = targetQuad.GetComponent<Renderer>();
            if (_renderer == null)
            {
                Debug.LogError("ターゲットQuadにRendererコンポーネントがありません");
                return;
            }
            DebugLog("Rendererコンポーネント取得成功");

            // マテリアルを取得
            _material = _renderer.material;
            if (_material == null)
            {
                Debug.LogError("ターゲットQuadにマテリアルがありません");
                return;
            }
            DebugLog("マテリアル取得成功");

            // NDIソース名が指定されている場合は設定
            if (!string.IsNullOrEmpty(sourceName))
            {
                _receiver.ndiName = sourceName;
                DebugLog($"NDIソース名を設定: {sourceName}");
            }
            else
            {
                DebugLog("NDIソース名が未指定のため、自動検出を使用");
            }

            // テクスチャをマテリアルに適用
            _material.mainTexture = _renderTexture;
            DebugLog("マテリアルにRenderTextureを設定");
            
            // 利用可能なNDIソースを表示
            var sources = NdiFinder.sourceNames.ToArray();
            if (sources != null && sources.Length > 0)
            {
                DebugLog($"利用可能なNDIソース ({sources.Length}個):");
                foreach (var source in sources)
                {
                    DebugLog($" - {source}");
                }
            }
            else
            {
                DebugLog("利用可能なNDIソースが見つかりません");
            }
            
            _initialized = true;
            DebugLog("NDIReceiver初期化完了");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"NDIReceiver設定エラー: {e.Message}\n{e.StackTrace}");
        }
    }

    void Update()
    {
        // 初期化後、接続状態を1回だけチェック
        if (_initialized && _receiver != null && showDebugLogs && !_connectionLogged)
        {
            // 接続が確立されたら1回だけログを出力
            if (!string.IsNullOrEmpty(_receiver.ndiName))
            {
                DebugLog($"NDI接続状態: ソース名={_receiver.ndiName}, 接続中=True");
                _connectionLogged = true;
            }
            // 接続が確立されていない場合は、3秒後に再度チェック
            else if (Time.frameCount % 180 == 0)
            {
                DebugLog("NDIソースへの接続を待機中...");
            }
        }
    }

    void OnDestroy()
    {
        DebugLog("OnDestroy: NDIReceiverクリーンアップ");
        
        // RenderTextureのクリーンアップ
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
            DebugLog("RenderTextureを解放");
        }
    }

    /// <summary>
    /// 利用可能なNDIソースを検索して最初のものを選択
    /// </summary>
    public void FindFirstSource()
    {
        if (_receiver != null)
        {
            _receiver.ndiName = "";  // 空にすると最初のソースを自動選択
            DebugLog("最初のNDIソースを自動選択");
            _connectionLogged = false; // 接続状態のログをリセット
        }
    }

    /// <summary>
    /// 特定のNDIソースを名前で選択
    /// </summary>
    public void SelectSource(string name)
    {
        if (_receiver != null)
        {
            _receiver.ndiName = name;
            DebugLog($"NDIソースを選択: {name}");
            _connectionLogged = false; // 接続状態のログをリセット
        }
    }

    /// <summary>
    /// 現在接続されているNDIソース名を取得
    /// </summary>
    public string GetCurrentSourceName()
    {
        return _receiver != null ? _receiver.ndiName : "";
    }
    
    /// <summary>
    /// デバッグログを出力
    /// </summary>
    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[NDIReceiver] {message}");
        }
    }

    /// <summary>
    /// NDIレシーバーのRenderTextureを取得
    /// </summary>
    public RenderTexture GetTargetTexture()
    {
        return _receiver != null ? _receiver.targetTexture : null;
    }
} 