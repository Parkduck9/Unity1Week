using TMPro;
using UnityEngine;

/// <summary>
/// 화면 UI 표시 담당. 값은 GameManager가 알려준다.
/// - 7단계: 왼쪽 위 SCORE, 오른쪽 위 BEST
/// - 결과 문구 (GAME OVER) + 화면 어둡게
/// - 재시작 카운트다운 (결과 문구 아래 숫자, 바뀔 때마다 커졌다 작아짐)
/// </summary>
public class UIManager : MonoBehaviour
{
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text bestText;

    [Header("결과")]
    [SerializeField] GameObject resultPanel;
    [SerializeField] TMP_Text resultText;
    [SerializeField] Color gameOverColor = new Color(1f, 0.32f, 0.32f);

    [Header("카운트다운")]
    [SerializeField] TMP_Text countdownText;
    [SerializeField] float popScale = 1.5f;
    [SerializeField] float popTime = 0.3f;

    float popTimer = -1f;

    public void SetScore(int score)
    {
        if (scoreText != null) scoreText.text = $"SCORE {score}";
    }

    public void SetBest(int best)
    {
        if (bestText != null) bestText.text = $"BEST {best}";
    }

    public void ShowResult(GameState result)
    {
        if (resultPanel == null || resultText == null) return;
        resultText.text = "GAME OVER";
        resultText.color = gameOverColor;
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        resultPanel.SetActive(true);
    }

    public void HideResult()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    public void ShowCountdown(int seconds)
    {
        if (countdownText == null) return;
        countdownText.gameObject.SetActive(true);
        countdownText.text = seconds.ToString();
        popTimer = 0f;
        countdownText.transform.localScale = Vector3.one * (Application.isPlaying ? popScale : 1f);
    }

    void Update()
    {
        if (popTimer < 0f || countdownText == null) return;
        popTimer += Time.unscaledDeltaTime;
        var k = Mathf.Clamp01(popTimer / popTime);
        var ease = 1f - (1f - k) * (1f - k);
        countdownText.transform.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, ease);
        if (k >= 1f) popTimer = -1f;
    }
}
