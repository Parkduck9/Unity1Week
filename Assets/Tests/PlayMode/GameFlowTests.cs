using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// 5단계 확인: GAME OVER / CLEAR 판정, 결과 문구, 입력 막기, 떨어질 때 카메라.
/// 테스트 어셈블리는 게임 스크립트를 직접 참조할 수 없어서 UI 오브젝트로 결과를 확인한다.
/// </summary>
public class GameFlowTests : PlayModeTestBase
{
    static readonly Vector3[] ItemPositions =
    {
        new Vector3(-0.5f, 0f, -0.5f),
        new Vector3(0.5f, 0f, 0.5f),
        new Vector3(1.5f, 0f, 1.5f),
    };

    GameObject resultPanel;
    TMP_Text resultText;

    [UnitySetUp]
    public IEnumerator FindResultUI()
    {
        // 처음에는 꺼져 있으므로 GameObject.Find 대신 부모에서 찾는다
        var ui = GameObject.Find("UI");
        Assert.IsNotNull(ui, "UI가 있어야 합니다.");
        resultPanel = ui.transform.Find("ResultPanel").gameObject;
        resultText = resultPanel.transform.Find("ResultText").GetComponent<TMP_Text>();
        yield break;
    }

    IEnumerator WaitUntilResult(float timeout)
    {
        for (var t = 0f; t < timeout && !resultPanel.activeSelf; t += Time.deltaTime) yield return null;
    }

    IEnumerator AssertInputBlocked()
    {
        var before = player.transform.position;
        yield return Hold(0.5f, Key.D, Key.W);
        var after = player.transform.position;
        Assert.Less(new Vector2(after.x - before.x, after.z - before.z).magnitude, 0.05f, "게임이 끝나면 입력이 막혀야 합니다.");
    }

    [UnityTest]
    public IEnumerator Start_NoResult()
    {
        Assert.IsFalse(resultPanel.activeSelf, "시작할 때 결과 화면은 숨겨져 있어야 합니다.");
        yield return Hold(0.3f, Key.Space); // 점프해도 게임 오버가 되지 않아야 함
        yield return new WaitForSeconds(1f);
        Assert.IsFalse(resultPanel.activeSelf);
    }

    [UnityTest]
    public IEnumerator FallingOffEdge_GameOver_CameraStaysAtMapHeight()
    {
        Press(Key.A); // 시작 위치는 왼쪽 끝 → 맵 밖
        var cameraMinY = float.MaxValue;
        for (var t = 0f; t < 2.5f && !resultPanel.activeSelf; t += Time.deltaTime)
        {
            if (player.transform.position.y < -1f) cameraMinY = Mathf.Min(cameraMinY, Camera.main.transform.position.y);
            yield return null;
        }
        ReleaseAll();

        Assert.IsTrue(resultPanel.activeSelf, "y < -5로 떨어지면 결과 화면이 나와야 합니다.");
        Assert.AreEqual("GAME OVER", resultText.text);
        Assert.Less(player.transform.position.y, -4.9f, "GAME OVER는 y < -5에서 판정해야 합니다.");
        Assert.Greater(cameraMinY, 4.8f, "떨어질 때 카메라는 맵 높이(y 0 + 5)에서 멈춰야 합니다.");
    }

    [UnityTest]
    public IEnumerator FallingIntoHole_GameOver_AndInputBlocked()
    {
        Teleport(new Vector3(1.5f, 0f, -0.5f)); // 타일 (3,1), 왼쪽(-X)이 구멍 (2,1)
        yield return new WaitForSeconds(0.2f);
        yield return Hold(0.35f, Key.A);
        yield return WaitUntilResult(2f);

        Assert.IsTrue(resultPanel.activeSelf);
        Assert.AreEqual("GAME OVER", resultText.text);
        yield return AssertInputBlocked();
    }

    [UnityTest]
    public IEnumerator CollectingTwo_NotClearYet()
    {
        Teleport(ItemPositions[0]);
        yield return new WaitForSeconds(0.2f);
        Teleport(ItemPositions[1]);
        yield return new WaitForSeconds(0.5f);
        Assert.IsFalse(resultPanel.activeSelf, "아이템을 다 모으기 전에는 CLEAR가 아니어야 합니다.");
    }

    [UnityTest]
    public IEnumerator CollectingAll_Clear_AndInputBlocked_Idle()
    {
        foreach (var p in ItemPositions)
        {
            Teleport(p);
            yield return new WaitForSeconds(0.2f);
        }
        Assert.IsTrue(resultPanel.activeSelf, "아이템을 모두 모으면 결과 화면이 나와야 합니다.");
        Assert.AreEqual("CLEAR", resultText.text);

        yield return AssertInputBlocked();
        var animator = player.GetComponentInChildren<Animator>();
        Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "클리어 후 캐릭터는 그 자리에 멈춰 Idle이어야 합니다.");
        Assert.AreEqual("CLEAR", resultText.text, "CLEAR 뒤에 다른 결과로 바뀌면 안 됩니다.");
    }
}
