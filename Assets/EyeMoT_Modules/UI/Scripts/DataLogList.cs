using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DataLogList : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private GameObject dataRowPrefab;
    [SerializeField] private int maxDataCount = 1000;

    private readonly Queue<GameObject> dataRows = new();

    /// <summary>
    /// 新しいデータを一覧へ追加する
    /// </summary>
    public void AddData(string data)
    {
        // 現在、一番下付近を表示しているか
        bool isNearBottom =
            scrollRect.verticalNormalizedPosition <= 0.02f;

        GameObject row = Instantiate(dataRowPrefab, content);

        TMP_Text text = row.GetComponentInChildren<TMP_Text>();

        if (text != null)
        {
            text.text = data;
        }

        dataRows.Enqueue(row);

        // 保存件数を超えた古いデータを削除
        while (dataRows.Count > maxDataCount)
        {
            GameObject oldRow = dataRows.Dequeue();
            Destroy(oldRow);
        }

        // レイアウト計算を即時反映
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // 追加前に一番下を見ていた場合だけ、自動で最新データへ移動
        if (isNearBottom)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    public void ClearContent()
    {
        foreach(var obj in dataRows)
            Destroy(obj);
        dataRows.Clear();
    }
}