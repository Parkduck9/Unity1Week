using TMPro;
using UnityEngine;

/// <summary>
/// 화면 UI. 4단계: 아이템 개수 표시 (ITEM 0 / 3).
/// 5단계에서 결과 문구, 6단계에서 카운트다운이 추가될 예정.
/// </summary>
public class UIManager : MonoBehaviour
{
    [SerializeField] TMP_Text itemText;

    int total;
    int collected;

    public int Collected => collected;
    public int Total => total;

    void OnEnable() => Item.Collected += OnItemCollected;
    void OnDisable() => Item.Collected -= OnItemCollected;

    void Start()
    {
        total = FindObjectsByType<Item>(FindObjectsSortMode.None).Length;
        collected = 0;
        Refresh();
    }

    void OnItemCollected(Item item)
    {
        collected = Mathf.Min(collected + 1, total);
        Refresh();
    }

    void Refresh()
    {
        if (itemText != null) itemText.text = $"ITEM {collected} / {total}";
    }
}
