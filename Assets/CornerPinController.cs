using UnityEngine;

/// <summary>
/// コーナーピン(4隅の台形補正)を行うサンプルスクリプト。
/// 手順：
/// 1. GameObjectに MeshFilter + MeshRenderer を付ける
/// 2. 当スクリプトをアタッチし、meshFilterをインスペクタで設定
/// 3. マテリアルには、ビリヤード台カメラの RenderTexture を設定
/// 4. 実行後、マウスで四隅をドラッグするとQuadが歪み、投影映像が台形補正される
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CornerPinController : MonoBehaviour
{
    // Unityエディタ上で MeshFilter をドラッグ＆ドロップしてセット
    [SerializeField] private MeshFilter meshFilter;

    // 4隅のスクリーン座標(ピクセル単位)で保持
    // UnityのScreen座標系は、左下(0,0) → 右上(Screen.width, Screen.height)
    // プロジェクター解像度に合わせる場合、Screen.width/Screen.heightがプロジェクター出力に対応していると考えてください
    public Vector2 topLeft = new Vector2(0, 1080);
    public Vector2 topRight = new Vector2(1920, 1080);
    public Vector2 bottomLeft = new Vector2(0, 0);
    public Vector2 bottomRight = new Vector2(1920, 0);

    // 内部用
    private Mesh _mesh;

    // ドラッグ判定用
    private const float HANDLE_RADIUS = 20f; // ハンドル範囲
    private int _draggingCornerIndex = -1;   // -1は未選択

    void Awake()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();

        // メッシュを新規生成し、Quadを構築
        _mesh = new Mesh();
        meshFilter.mesh = _mesh;

        CreateInitialQuad();
    }

    /// <summary>
    /// 毎フレーム、メッシュの頂点を更新してQuad形状を反映
    /// </summary>
    void Update()
    {
        UpdateMeshVertices();
    }

    /// <summary>
    /// OnGUIでマウスドラッグ処理 + ハンドル描画
    /// </summary>
    void OnGUI()
    {
        Event e = Event.current;

        // === ハンドルの描画 & ドラッグ検出 ===
        DrawAndHandleCorner(0, ref bottomLeft);
        DrawAndHandleCorner(1, ref bottomRight);
        DrawAndHandleCorner(2, ref topLeft);
        DrawAndHandleCorner(3, ref topRight);

        // === マウスドラッグ中の処理 ===
        if (e.type == EventType.MouseDrag && _draggingCornerIndex != -1)
        {
            // マウス位置でコーナー頂点を更新
            Vector2 mousePos = e.mousePosition;
            // IMGUI座標は上から下にyが増えるので反転補正
            // ただし、ScreenSpaceが左下(0,0)の場合は invert する
            mousePos.y = Screen.height - mousePos.y;

            switch (_draggingCornerIndex)
            {
                case 0: bottomLeft = mousePos; break;
                case 1: bottomRight = mousePos; break;
                case 2: topLeft = mousePos; break;
                case 3: topRight = mousePos; break;
            }
        }

        // === マウスボタンアップでドラッグ解除 ===
        if (e.type == EventType.MouseUp)
        {
            _draggingCornerIndex = -1;
        }
    }

    /// <summary>
    /// Quadの初期頂点を設定
    /// (初回のみ呼び出し)
    /// </summary>
    private void CreateInitialQuad()
    {
        // 頂点4つ
        Vector3[] vertices = new Vector3[4];
        Vector2[] uv = new Vector2[4];
        int[] triangles = new int[6] { 0, 1, 2, 2, 1, 3 };

        // とりあえず初期値として変数の値をそのまま適用
        vertices[0] = new Vector3(bottomLeft.x, bottomLeft.y, 0);
        vertices[1] = new Vector3(bottomRight.x, bottomRight.y, 0);
        vertices[2] = new Vector3(topLeft.x, topLeft.y, 0);
        vertices[3] = new Vector3(topRight.x, topRight.y, 0);

        // UVは [0,0],[1,0],[0,1],[1,1] の標準的な割り当て
        uv[0] = new Vector2(1, 0);
        uv[1] = new Vector2(0, 0);
        uv[2] = new Vector2(1, 1);
        uv[3] = new Vector2(0, 1);

        _mesh.vertices = vertices;
        _mesh.uv = uv;
        _mesh.triangles = triangles;

        _mesh.RecalculateBounds();
    }

    /// <summary>
    /// 実行中に4隅の座標が変わった場合、メッシュ頂点を更新
    /// </summary>
    private void UpdateMeshVertices()
    {
        Vector3[] vertices = _mesh.vertices;

        vertices[0] = new Vector3(bottomLeft.x, bottomLeft.y, 0);
        vertices[1] = new Vector3(bottomRight.x, bottomRight.y, 0);
        vertices[2] = new Vector3(topLeft.x, topLeft.y, 0);
        vertices[3] = new Vector3(topRight.x, topRight.y, 0);

        _mesh.vertices = vertices;
        _mesh.RecalculateBounds();
    }

    /// <summary>
    /// コーナー位置に小さなGUIハンドルを描画し、マウスクリックされたらドラッグ開始
    /// </summary>
    /// <param name="cornerIndex">0~3</param>
    /// <param name="cornerPos">参照渡しするベクトル</param>
    private void DrawAndHandleCorner(int cornerIndex, ref Vector2 cornerPos)
    {
        // IMGUI座標系に合わせる: 
        //   UnityのEvent.current.mousePosition.yは「上から下に+」になる。
        //   一方、Screen座標(0,0)は左下。差分調整が必要。
        float invY = Screen.height - cornerPos.y;

        // ハンドル用のRect
        Rect handleRect = new Rect(cornerPos.x - HANDLE_RADIUS * 0.5f,
                                   invY - HANDLE_RADIUS * 0.5f,
                                   HANDLE_RADIUS, HANDLE_RADIUS);

        // ピンク色のBoxで可視化 (デザインはお好みで)
        GUI.color = Color.magenta;
        GUI.Box(handleRect, cornerIndex.ToString());

        // マウスダウン判定
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            // マウスがこのハンドルの範囲に入ったらドラッグ対象にする
            if (handleRect.Contains(e.mousePosition))
            {
                _draggingCornerIndex = cornerIndex;
            }
        }
    }
}
