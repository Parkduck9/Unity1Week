using TMPro;
using UnityEngine;

/// <summary>
/// 화면 UI 표시 담당. 값은 GameManager가 알려준다.
/// - 4단계: 아이템 개수 (ITEM 0 / 3)
/// - 5단계: 결과 문구 (GAME OVER / CLEAR) + 화면 어둡게
/// - 6단계: 카운트다운 (추가 예정)
/// </summary>
public class UIManager : MonoBehaviour
{
    [SerializeField] TMP_Text itemText;

    [Header("결과")]
    [SerializeField] GameObject resultPanel;
    [SerializeField] TMP_Text resultText;
    [SerializeField] Color gameOverColor = new Color(1f, 0.32f, 0.32f);
    [SerializeField] Color clearColor = new Color(1f, 0.82f, 0.30f);

    public void SetItemCount(int collected, int total)
    {
        if (itemText != null) itemText.text = $"ITEM {collected} / {total}";
    }

    public void ShowResult(GameState result)
    {
        if (resultPanel == null || resultText == null) return;
        var clear = result == GameState.Clear;
        resultText.text = clear ? "CLEAR" : "GAME OVER";
        resultText.color = clear ? clearColor : gameOverColor;
        resultPanel.SetActive(true);
    }

    public void HideResult()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }
}
