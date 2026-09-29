using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Playing,
    GameOver,
    Clear,
}

/// <summary>
/// 게임 상태 관리와 승패 판정.
/// - 실패: 플레이어가 failHeight 아래로 떨어지면 GameOver
/// - 성공: 아이템을 모두 모으면 Clear
/// 게임이 끝나면 플레이어 입력을 막고 결과 문구를 표시한 뒤,
/// 3 → 2 → 1 카운트다운 후 현재 씬을 다시 불러온다 (6단계).
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] UIManager ui;
    [SerializeField] float failHeight = -5f;
    [Tooltip("게임이 끝나고 재시작까지 기다리는 초 (카운트다운 숫자 개수)")]
    [SerializeField] int restartSeconds = 3;

    public GameState State { get; private set; } = GameState.Playing;
    public int CollectedItems { get; private set; }
    public int TotalItems { get; private set; }

    /// <summary>게임이 끝났을 때 (6단계 재시작에서 사용)</summary>
    public event Action<GameState> GameEnded;

    void OnEnable() => Item.Collected += OnItemCollected;
    void OnDisable() => Item.Collected -= OnItemCollected;

    void Start()
    {
        TotalItems = FindObjectsByType<Item>(FindObjectsSortMode.None).Length;
        CollectedItems = 0;
        State = GameState.Playing;
        if (player != null) player.InputEnabled = true;
        if (ui != null)
        {
            ui.SetItemCount(CollectedItems, TotalItems);
            ui.HideResult();
        }
    }

    void Update()
    {
        if (State == GameState.Playing && player != null && player.transform.position.y < failHeight)
            EndGame(GameState.GameOver);
    }

    void OnItemCollected(Item item)
    {
        if (State != GameState.Playing) return;
        CollectedItems = Mathf.Min(CollectedItems + 1, TotalItems);
        if (ui != null) ui.SetItemCount(CollectedItems, TotalItems);
        if (CollectedItems >= TotalItems) EndGame(GameState.Clear);
    }

    void EndGame(GameState result)
    {
        State = result;
        if (player != null) player.InputEnabled = false;
        if (ui != null) ui.ShowResult(result);
        GameEnded?.Invoke(result);
        StartCoroutine(RestartCountdown());
    }

    IEnumerator RestartCountdown()
    {
        // 게임이 끝나자마자 시작 (사용자 결정). 시간 배율이 바뀌어도 실제 시간으로 센다
        for (var n = restartSeconds; n >= 1; n--)
        {
            if (ui != null) ui.ShowCountdown(n);
            yield return new WaitForSecondsRealtime(1f);
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
