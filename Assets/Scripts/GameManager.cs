using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Playing,
    GameOver,
}

/// <summary>
/// 게임 상태, 점수, 최고 점수 관리.
/// - 7단계: CLEAR 없음. 떨어질 때까지 계속하며 코인으로 점수를 모은다.
/// - 실패: 플레이어가 failHeight 아래로 떨어지면 GameOver → 최고 점수 저장 → 3 → 2 → 1 후 재시작.
/// - 최고 점수는 PlayerPrefs에 저장해 다음 실행 때도 보여 준다.
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] UIManager ui;
    [SerializeField] float failHeight = -5f;
    [Tooltip("게임이 끝나고 재시작까지 기다리는 초 (카운트다운 숫자 개수)")]
    [SerializeField] int restartSeconds = 3;
    [Tooltip("최고 점수를 저장하는 PlayerPrefs 키")]
    [SerializeField] string bestScoreKey = "BestScore";

    public GameState State { get; private set; } = GameState.Playing;
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public string BestScoreKey => bestScoreKey;

    /// <summary>게임이 끝났을 때 (HoleManager, CoinSpawner가 멈춤)</summary>
    public event Action<GameState> GameEnded;

    void OnEnable() => Item.Collected += OnItemCollected;
    void OnDisable() => Item.Collected -= OnItemCollected;

    void Start()
    {
        State = GameState.Playing;
        Score = 0;
        BestScore = PlayerPrefs.GetInt(bestScoreKey, 0);
        if (player != null) player.InputEnabled = true;
        if (ui != null)
        {
            ui.SetScore(Score);
            ui.SetBest(BestScore);
            ui.HideResult();
        }
    }

    void Update()
    {
        if (State == GameState.Playing && player != null && player.transform.position.y < failHeight)
            EndGame();
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

        if (Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(bestScoreKey, BestScore);
            PlayerPrefs.Save();
            if (ui != null) ui.SetBest(BestScore);
        }

        if (ui != null) ui.ShowResult(State);
        GameEnded?.Invoke(State);
        StartCoroutine(RestartCountdown());
    }

    IEnumerator RestartCountdown()
    {
        // 게임이 끝나자마자 시작. 시간 배율과 상관없이 실제 시간으로 센다
        for (var n = restartSeconds; n >= 1; n--)
        {
            if (ui != null) ui.ShowCountdown(n);
            yield return new WaitForSecondsRealtime(1f);
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
