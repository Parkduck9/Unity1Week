using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면 UI 표시 담당. 값은 GameManager가 알려준다. (UI 글자는 기본 폰트에 한글이 없어 영어)
/// - 플레이 중: 왼쪽 위 SCORE, 오른쪽 위 BEST
/// - 메인 메뉴: 제목 JumpGirl, START, QUIT
/// - 게임 오버: GAME OVER, 이번 점수·최고 점수, NEW BEST!, RETRY / MAIN MENU / QUIT (자동 재시작 없음)
/// 버튼은 마우스로 누르거나, 방향키·WASD로 고르고 Enter로 누를 수 있다.
/// </summary>
public class UIManager : MonoBehaviour
{
    [SerializeField] GameManager gameManager;

    [Header("플레이 중")]
    [SerializeField] GameObject scorePanel;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] TMP_Text bestText;

    [Header("메인 메뉴")]
    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] Button startButton;
    [SerializeField] Button menuQuitButton;

    [Header("게임 오버")]
    [SerializeField] GameObject resultPanel;
    [SerializeField] TMP_Text resultText;
    [SerializeField] TMP_Text resultScoreText;
    [SerializeField] TMP_Text resultBestText;
    [SerializeField] GameObject newBestBadge;
    [SerializeField] Button retryButton;
    [SerializeField] Button mainMenuButton;
    [SerializeField] Button quitButton;

    void Awake()
    {
        if (gameManager == null) return;
        if (startButton != null) startButton.onClick.AddListener(gameManager.StartGame);
        if (menuQuitButton != null) menuQuitButton.onClick.AddListener(gameManager.Quit);
        if (retryButton != null) retryButton.onClick.AddListener(gameManager.Retry);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(gameManager.GoToMainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(gameManager.Quit);
    }

    public void SetScore(int score)
    {
        if (scoreText != null) scoreText.text = $"SCORE {score}";
    }

    public void SetBest(int best)
    {
        if (bestText != null) bestText.text = $"BEST {best}";
    }

    public void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (scorePanel != null) scorePanel.SetActive(false); // 메뉴에서는 BEST만 보임
        Select(startButton);
    }

    public void HideMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (scorePanel != null) scorePanel.SetActive(true);
        Select(null);
    }

    public void ShowResult(int score, int best, bool newBest)
    {
        if (resultPanel == null) return;
        if (resultText != null) resultText.text = "GAME OVER";
        if (resultScoreText != null) resultScoreText.text = score.ToString();
        if (resultBestText != null) resultBestText.text = best.ToString();
        if (newBestBadge != null) newBestBadge.SetActive(newBest);
        resultPanel.SetActive(true);
        Select(retryButton);
    }

    public void HideResult()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    // 키보드로 바로 고를 수 있도록 처음 선택할 버튼을 정함
    static void Select(Button button)
    {
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null);
    }
}
