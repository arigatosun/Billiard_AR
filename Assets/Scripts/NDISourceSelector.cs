using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Klak.Ndi;

/// <summary>
/// NDIソースを一覧表示して選択できるUIコンポーネント
/// </summary>
public class NDISourceSelector : MonoBehaviour
{
    [Header("UI参照")]
    [Tooltip("NDIソース一覧を表示するドロップダウン")]
    public Dropdown sourceDropdown;

    [Tooltip("ソース更新ボタン")]
    public Button refreshButton;

    [Header("NDI参照")]
    [Tooltip("NDIレシーバーコンポーネント")]
    public NDIReceiver ndiReceiver;

    // 現在のNDIソース名の配列
    private string[] _sourceNames;

    void Start()
    {
        // コンポーネントの確認
        if (sourceDropdown == null)
        {
            Debug.LogError("ソース選択用のDropdownが設定されていません");
            return;
        }

        if (ndiReceiver == null)
        {
            Debug.LogError("NDIReceiverが設定されていません");
            return;
        }

        // ボタンイベントの設定
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RefreshSources);
        }

        // ドロップダウンイベントの設定
        sourceDropdown.onValueChanged.AddListener(OnSourceSelected);

        // 初期ソース一覧の取得
        RefreshSources();
    }

    /// <summary>
    /// NDIソース一覧を更新
    /// </summary>
    public void RefreshSources()
    {
        // ドロップダウンをクリア
        sourceDropdown.ClearOptions();

        // 現在のソース名を取得
        string currentSource = ndiReceiver.GetCurrentSourceName();

        // オプションリストを作成
        List<string> options = new List<string>();
        options.Add("自動選択"); // 最初のオプションは自動選択

        // 選択インデックス
        int selectedIndex = 0;

        // NDIソース一覧を取得（静的メソッドを使用）- IEnumerableをToArray()で配列に変換
        _sourceNames = NdiFinder.sourceNames.ToArray();
        
        if (_sourceNames != null && _sourceNames.Length > 0)
        {
            foreach (var source in _sourceNames)
            {
                options.Add(source);
                
                // 現在選択中のソースと一致するか確認
                if (source == currentSource)
                {
                    selectedIndex = options.Count - 1;
                }
            }
        }

        // ドロップダウンにオプションを設定
        sourceDropdown.AddOptions(options);

        // 現在のソースを選択
        sourceDropdown.value = selectedIndex;
    }

    /// <summary>
    /// ソースが選択された時の処理
    /// </summary>
    private void OnSourceSelected(int index)
    {
        if (index == 0)
        {
            // 自動選択
            ndiReceiver.FindFirstSource();
        }
        else if (index > 0 && _sourceNames != null && index <= _sourceNames.Length)
        {
            // 特定のソースを選択
            string sourceName = _sourceNames[index - 1];
            ndiReceiver.SelectSource(sourceName);
        }
    }
} 