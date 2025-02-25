using UnityEngine;
using UnityEditor;
using System.Linq;
using Klak.Ndi;

/// <summary>
/// NDIReceiverコンポーネントのカスタムエディタ
/// </summary>
[CustomEditor(typeof(NDIReceiver))]
public class NDIReceiverEditor : Editor
{
    // 現在のNDIソース名の配列
    private string[] _sourceNames;

    private void OnEnable()
    {
        // NDIソース一覧を取得
        UpdateSourceList();
    }

    public override void OnInspectorGUI()
    {
        // ターゲットを取得
        NDIReceiver receiver = (NDIReceiver)target;

        // デフォルトのインスペクタを描画
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("NDIソース選択", EditorStyles.boldLabel);

        // 利用可能なNDIソースを表示
        if (_sourceNames != null && _sourceNames.Length > 0)
        {
            EditorGUILayout.LabelField("利用可能なNDIソース:");
            
            // 各ソースをボタンとして表示
            foreach (var sourceName in _sourceNames)
            {
                if (GUILayout.Button(sourceName))
                {
                    // ボタンが押されたらそのソースを選択
                    receiver.sourceName = sourceName;
                    EditorUtility.SetDirty(receiver);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("利用可能なNDIソースが見つかりません。", MessageType.Info);
        }

        EditorGUILayout.Space();

        // 更新ボタン
        if (GUILayout.Button("NDIソース一覧を更新"))
        {
            // ソース一覧を更新
            UpdateSourceList();
        }

        // 自動選択ボタン
        if (GUILayout.Button("最初のソースを自動選択"))
        {
            receiver.sourceName = "";
            EditorUtility.SetDirty(receiver);
        }
    }

    // NDIソース一覧を更新
    private void UpdateSourceList()
    {
        // 静的メソッドを使用してNDIソース一覧を取得 - IEnumerableをToArray()で配列に変換
        _sourceNames = NdiFinder.sourceNames.ToArray();
        
        // エディタの再描画を要求
        Repaint();
    }
} 