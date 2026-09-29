using System;
using UnityEngine;

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
/// 게임이 끝나면 플레이어 입력을 막고 결과 문구를 표시한다.
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] UIManager ui;
    [SerializeField] float failHeight = -5f;

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
    }
}
