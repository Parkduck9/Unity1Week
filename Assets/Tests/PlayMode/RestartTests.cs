using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 게임 오버 뒤 확인 (UI 사용자 결정 A): 자동 재시작 없음, RETRY = 바로 다시 시작, MAIN MENU = 메인 메뉴, QUIT = 게임 종료.
/// 점수·땅·코인은 처음으로 돌아오고 최고 점수는 남는다.
/// </summary>
public class RestartTests : PlayModeTestBase
{
    static readonly Vector3 StartPosition = new Vector3(-1.5f, 0f, -1.5f);

    int loadCount;

    [UnitySetUp]
    public IEnumerator WatchSceneLoads()
    {
        loadCount = 0;
        SceneManager.sceneLoaded += OnSceneLoaded;
        yield break;
    }

    [TearDown]
    public void StopWatching() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => loadCount++;

    static Transform UI => GameObject.Find("UI").transform;
    static GameObject ResultPanel => UI.Find("ResultPanel").gameObject;
    static GameObject MainMenu => UI.Find("MainMenuPanel").gameObject;
    static Button ResultButton(string name) => ResultPanel.transform.Find(name).GetComponent<Button>();
    static TMP_Text BestText => GameObject.Find("BestText").GetComponent<TMP_Text>();

    IEnumerator FallOffEdge()
    {
        Press(Key.A);
        for (var t = 0f; t < 3f && !ResultPanel.activeSelf; t += Time.deltaTime) yield return null;
        ReleaseAll();
        Assert.IsTrue(ResultPanel.activeSelf, "게임 오버 화면이 나와야 합니다.");
    }

    IEnumerator WaitForReload()
    {
        var before = loadCount;
        for (var t = 0f; t < 3f && loadCount == before; t += Time.unscaledDeltaTime) yield return null;
        Assert.Greater(loadCount, before, "씬을 다시 불러와야 합니다.");
        yield return null;
        yield return new WaitForSeconds(0.3f);
        FindSceneObjects();
    }

    [UnityTest]
    public IEnumerator GameOver_DoesNotRestartAutomatically()
    {
        yield return FallOffEdge();
        Assert.AreEqual(ResultButton("RetryButton").gameObject, EventSystem.current.currentSelectedGameObject, "키보드로 바로 고를 수 있게 RETRY가 선택되어 있어야 합니다.");

        var before = loadCount;
        yield return new WaitForSeconds(4f);
        Assert.AreEqual(before, loadCount, "자동으로 다시 시작하면 안 됩니다 (버튼을 누를 때까지 기다림).");
        Assert.IsTrue(ResultPanel.activeSelf);
    }

    [UnityTest]
    public IEnumerator Retry_RestartsImmediately_Fresh()
    {
        TileAt(3, 3).Fall(); // 재시작 뒤 땅이 모두 돌아오는지 보기 위해 구멍을 만들어 둠
        coinSpawner.SpawnAt(TileAt(1, 0));
        yield return Hold(0.4f, Key.D); // 코인 먹기 (100점)
        Teleport(CellPosition(0, 0));
        yield return new WaitForSeconds(0.2f);
        yield return FallOffEdge();

        ResultButton("RetryButton").onClick.Invoke();
        yield return WaitForReload();

        Assert.AreEqual(GameState.Playing, gameManager.State, "RETRY는 메인 메뉴 없이 바로 시작해야 합니다.");
        Assert.IsFalse(MainMenu.activeSelf);
        Assert.IsFalse(ResultPanel.activeSelf, "결과 화면이 사라져야 합니다.");
        var p = player.transform.position;
        Assert.AreEqual(StartPosition.x, p.x, 0.05f, "캐릭터가 시작 위치로 돌아와야 합니다.");
        Assert.AreEqual(StartPosition.z, p.z, 0.05f);
        Assert.AreEqual("SCORE 0", GameObject.Find("ScoreText").GetComponent<TMP_Text>().text, "점수가 0으로 돌아와야 합니다.");
        Assert.AreEqual("BEST 100", BestText.text, "최고 점수는 남아 있어야 합니다.");
        foreach (var tile in holeManager.Tiles) Assert.AreEqual(TileState.Normal, tile.State, "땅이 모두 돌아와야 합니다.");
        Assert.AreEqual(0, GameObject.Find("Level/Coins").transform.childCount, "코인은 다시 처음부터 생겨야 합니다.");

        StopSystems();
        var before = player.transform.position;
        yield return Hold(0.3f, Key.D);
        Assert.Greater(player.transform.position.x - before.x, 0.5f, "재시작 후 바로 움직일 수 있어야 합니다.");
    }

    [UnityTest]
    public IEnumerator MainMenuButton_GoesBackToMenu()
    {
        yield return FallOffEdge();
        ResultButton("MainMenuButton").onClick.Invoke();
        yield return WaitForReload();

        Assert.AreEqual(GameState.Menu, gameManager.State, "MAIN MENU는 메인 메뉴로 돌아가야 합니다.");
        Assert.IsTrue(MainMenu.activeSelf);
        Assert.IsFalse(ResultPanel.activeSelf);
    }

    [UnityTest]
    public IEnumerator QuitButton_QuitsGame()
    {
        var quit = false;
        GameManager.QuitOverride = () => quit = true; // 테스트에서는 실제로 종료하지 않고 호출만 확인
        yield return FallOffEdge();
        ResultButton("QuitButton").onClick.Invoke();
        Assert.IsTrue(quit, "QUIT는 게임을 종료해야 합니다.");
    }
}
