using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴 확인: START를 누르기 전에는 게임이 멈춰 있고, START로 시작, QUIT로 종료.
/// </summary>
public class MainMenuTests : PlayModeTestBase
{
    protected override bool AutoStart => false; // 메인 메뉴 상태 그대로 시작
    protected override bool AutoSystems => true; // 구멍·코인이 메뉴에서 멈춰 있는지 보기 위해 켜 둠

    static Transform MainMenu => GameObject.Find("UI").transform.Find("MainMenuPanel");
    static Button MenuButton(string name) => MainMenu.Find(name).GetComponent<Button>();

    [UnityTest]
    public IEnumerator ShowsMenu_WithTitle_AndStartSelected()
    {
        Assert.AreEqual(GameState.Menu, gameManager.State, "처음에는 메인 메뉴여야 합니다.");
        Assert.IsTrue(MainMenu.gameObject.activeSelf);
        Assert.AreEqual("JumpGirl", MainMenu.Find("Title").GetComponent<TMP_Text>().text);
        Assert.IsNotNull(MainMenu.Find("StartButton"));
        Assert.IsNotNull(MainMenu.Find("QuitButton"));
        Assert.IsNull(GameObject.Find("ScoreText"), "메뉴에서는 점수(SCORE)를 숨겨야 합니다.");
        Assert.IsNotNull(GameObject.Find("BestText"), "메뉴에서도 최고 점수(BEST)는 보여야 합니다.");
        Assert.AreEqual(MenuButton("StartButton").gameObject, EventSystem.current.currentSelectedGameObject, "키보드로 바로 누를 수 있게 START가 선택되어 있어야 합니다.");
        yield break;
    }

    [UnityTest]
    public IEnumerator BeforeStart_NothingMoves()
    {
        var before = player.transform.position;
        yield return Hold(0.5f, Key.D);
        Assert.Less(Vector3.Distance(before, player.transform.position), 0.05f, "START 전에는 캐릭터가 움직이면 안 됩니다.");

        yield return new WaitForSeconds(3.3f);
        foreach (var tile in holeManager.Tiles) Assert.AreEqual(TileState.Normal, tile.State, "START 전에는 땅이 떨어지면 안 됩니다.");
        Assert.AreEqual(0, GameObject.Find("Level/Coins").transform.childCount, "START 전에는 코인이 생기면 안 됩니다.");
    }

    [UnityTest]
    public IEnumerator Start_BeginsGame()
    {
        MenuButton("StartButton").onClick.Invoke();
        yield return null;
        Assert.AreEqual(GameState.Playing, gameManager.State);
        Assert.IsFalse(MainMenu.gameObject.activeSelf, "START를 누르면 메뉴가 사라져야 합니다.");
        Assert.IsNotNull(GameObject.Find("ScoreText"), "플레이 중에는 점수가 보여야 합니다.");

        var before = player.transform.position;
        yield return Hold(0.3f, Key.D);
        Assert.Greater(player.transform.position.x - before.x, 0.5f, "START 뒤에는 움직일 수 있어야 합니다.");

        yield return new WaitForSeconds(3f);
        var changed = 0;
        foreach (var tile in holeManager.Tiles) if (tile.State != TileState.Normal) changed++;
        Assert.GreaterOrEqual(changed, 1, "START 뒤에는 땅이 흔들리기 시작해야 합니다.");
    }

    [UnityTest]
    public IEnumerator Quit_QuitsGame()
    {
        var quit = false;
        GameManager.QuitOverride = () => quit = true;
        MenuButton("QuitButton").onClick.Invoke();
        Assert.IsTrue(quit, "메뉴의 QUIT는 게임을 종료해야 합니다.");
        yield break;
    }
}
