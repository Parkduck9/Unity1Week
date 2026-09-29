using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode 테스트 공통 준비: Main 씬을 불러오고 가상 키보드로 입력을 넣는다.
/// 7단계부터 구멍과 코인이 무작위로 생기므로, 기본으로 HoleManager와 CoinSpawner를 멈추고
/// 테스트가 직접 땅·코인을 제어한다. (AutoSystems = true면 멈추지 않음)
/// 최고 점수(PlayerPrefs)는 테스트 전에 백업하고 끝나면 되돌린다.
/// </summary>
public abstract class PlayModeTestBase
{
    protected const string BestScoreKey = "BestScore";

    protected Keyboard keyboard;
    protected GameObject player;
    protected Rigidbody rb;
    protected GameManager gameManager;
    protected HoleManager holeManager;
    protected CoinSpawner coinSpawner;

    /// <summary>true면 구멍·코인이 게임처럼 자동으로 생긴다</summary>
    protected virtual bool AutoSystems => false;

    /// <summary>true면 씬을 불러온 뒤 메인 메뉴의 START를 누른 것처럼 바로 시작한다</summary>
    protected virtual bool AutoStart => true;

    InputSettings.BackgroundBehavior oldBackground;
    InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
    bool hadBest;
    int oldBest;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // batchmode에서는 창 포커스가 없으므로 포커스와 무관하게 입력을 받게 한다
        oldBackground = InputSystem.settings.backgroundBehavior;
        oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

        // 사용자의 실제 최고 점수를 지키기 위해 백업 후 0에서 시작
        hadBest = PlayerPrefs.HasKey(BestScoreKey);
        oldBest = PlayerPrefs.GetInt(BestScoreKey, 0);
        PlayerPrefs.DeleteKey(BestScoreKey);

        keyboard = InputSystem.AddDevice<Keyboard>();
        GameManager.SkipMenuOnce = false;
        SceneManager.LoadScene("Main");
        yield return null;
        FindSceneObjects();
        if (AutoStart) gameManager.StartGame();
        if (!AutoSystems) StopSystems();
        yield return null;
        yield return new WaitForSeconds(0.3f); // 착지 대기
    }

    [TearDown]
    public void TearDown()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = oldBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;

        GameManager.SkipMenuOnce = false;
        GameManager.QuitOverride = null;

        if (hadBest) PlayerPrefs.SetInt(BestScoreKey, oldBest);
        else PlayerPrefs.DeleteKey(BestScoreKey);
        PlayerPrefs.Save();
    }

    /// <summary>씬 오브젝트를 다시 찾는다 (재시작으로 씬이 바뀐 뒤에도 사용)</summary>
    protected void FindSceneObjects()
    {
        player = GameObject.Find("VoxelCharacter");
        Assert.IsNotNull(player, "VoxelCharacter가 씬에 없습니다.");
        rb = player.GetComponent<Rigidbody>();
        gameManager = Object.FindAnyObjectByType<GameManager>();
        holeManager = Object.FindAnyObjectByType<HoleManager>();
        coinSpawner = Object.FindAnyObjectByType<CoinSpawner>();
    }

    protected void StopSystems()
    {
        holeManager.enabled = false;
        coinSpawner.enabled = false;
    }

    protected Tile TileAt(int x, int z) => holeManager.GetTile(new Vector2Int(x, z));

    protected static Vector3 CellPosition(int x, int z) => new Vector3(x - 1.5f, 0f, z - 1.5f);

    protected void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    protected void ReleaseAll() => InputSystem.QueueStateEvent(keyboard, new KeyboardState());

    protected IEnumerator Hold(float seconds, params Key[] keys)
    {
        Press(keys);
        yield return new WaitForSeconds(seconds);
        ReleaseAll();
        yield return new WaitForSeconds(0.2f);
    }

    protected void Teleport(Vector3 position)
    {
        rb.linearVelocity = Vector3.zero;
        rb.position = position;
        player.transform.position = position;
    }

    /// <summary>칸을 바로 떨어뜨리고 구멍이 될 때까지 기다린다</summary>
    protected IEnumerator MakeHole(int x, int z)
    {
        TileAt(x, z).Fall();
        yield return new WaitForSeconds(0.9f);
    }
}
