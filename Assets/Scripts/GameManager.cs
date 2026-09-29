using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Menu,     // 메인 메뉴 (START를 누르기 전)
    Playing,
    GameOver,
}

/// <summary>
/// 게임 흐름, 점수, 최고 점수 관리.
/// - 메인 메뉴 → START → 플레이 → 떨어지면 GAME OVER → RETRY / MAIN MENU / QUIT (자동 재시작 없음)
/// - 실패: 플레이어가 failHeight 아래로 떨어지면 GameOver, 최고 점수를 넘으면 PlayerPrefs에 저장
/// - RETRY는 메인 메뉴를 거치지 않고 바로 다시 시작, MAIN MENU는 메인 메뉴부터 다시 시작
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] UIManager ui;
    [SerializeField] float failHeight = -5f;
    [Tooltip("최고 점수를 저장하는 PlayerPrefs 키")]
    [SerializeField] string bestScoreKey = "BestScore";

    /// <summary>true면 다음에 씬을 불러올 때 메인 메뉴를 건너뛰고 바로 시작 (RETRY)</summary>
    public static bool SkipMenuOnce;

    /// <summary>테스트에서 게임 종료 대신 부를 동작 (null이면 실제로 종료)</summary>
    public static Action QuitOverride;

    public GameState State { get; private set; } = GameState.Menu;
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public string BestScoreKey => bestScoreKey;

    public event Action GameStarted;
    public event Action<GameState> GameEnded;

    void OnEnable() => Item.Collected += OnItemCollected;
    void OnDisable() => Item.Collected -= OnItemCollected;

    void Start()
    {
        Score = 0;
        BestScore = PlayerPrefs.GetInt(bestScoreKey, 0);
        if (player != null) player.InputEnabled = false;
        if (ui != null)
        {
            ui.SetScore(Score);
            ui.SetBest(BestScore);
            ui.HideResult();
        }

        if (SkipMenuOnce)
        {
            SkipMenuOnce = false;
            StartGame();
        }
        else
        {
            State = GameState.Menu;
            if (ui != null) ui.ShowMainMenu();
        }
    }

    void Update()
    {
        if (State == GameState.Playing && player != null && player.transform.position.y < failHeight)
            EndGame();
    }

    /// <summary>메인 메뉴의 START</summary>
    public void StartGame()
    {
        if (State == GameState.Playing) return;
        State = GameState.Playing;
        if (player != null) player.InputEnabled = true;
        if (ui != null) ui.HideMainMenu();
        GameStarted?.Invoke();
    }

    /// <summary>게임 오버의 RETRY: 메인 메뉴를 거치지 않고 바로 다시 시작</summary>
    public void Retry()
    {
        SkipMenuOnce = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>게임 오버의 MAIN MENU: 메인 메뉴부터 다시</summary>
    public void GoToMainMenu()
    {
        SkipMenuOnce = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>QUIT: 게임 종료 (에디터에서는 플레이 모드를 멈춤)</summary>
    public void Quit()
    {
        if (QuitOverride != null)
        {
            QuitOverride();
            return;
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnItemCollected(Item item)
    {
        if (State != GameState.Playing) return;
        Score += item.CollectedPoints;
        if (ui != null) ui.SetScore(Score);
    }

    void EndGame()
    {
        State = GameState.GameOver;
        if (player != null) player.InputEnabled = false;

        var newBest = Score > BestScore;
        if (newBest)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(bestScoreKey, BestScore);
            PlayerPrefs.Save();
            if (ui != null) ui.SetBest(BestScore);
        }

        if (ui != null) ui.ShowResult(Score, BestScore, newBest);
        GameEnded?.Invoke(State);
    }
}
