using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 2단계 확인: 이동 / 점프 / 낙하 / 카메라.
/// 가상 키보드로 입력을 넣고 Main 씬의 결과를 확인한다.
/// </summary>
public class PlayerMovementTests
{
    static readonly Vector3 StartPosition = new Vector3(-1.5f, 0f, -1.5f);
    static readonly Vector3 CameraOffset = new Vector3(0f, 5f, -4.5f);

    Keyboard keyboard;
    GameObject player;
    Rigidbody rb;
    InputSettings.BackgroundBehavior oldBackground;
    InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // batchmode에서는 창 포커스가 없으므로 포커스와 무관하게 입력을 받게 한다
        oldBackground = InputSystem.settings.backgroundBehavior;
        oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

        keyboard = InputSystem.AddDevice<Keyboard>();
        SceneManager.LoadScene("Main");
        yield return null;
        yield return null;

        player = GameObject.Find("VoxelCharacter");
        Assert.IsNotNull(player, "VoxelCharacter가 씬에 없습니다.");
        rb = player.GetComponent<Rigidbody>();
        yield return new WaitForSeconds(0.3f); // 착지 대기
    }

    [TearDown]
    public void TearDown()
    {
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = oldBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
    }

    void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    void ReleaseAll() => InputSystem.QueueStateEvent(keyboard, new KeyboardState());

    IEnumerator Hold(float seconds, params Key[] keys)
    {
        Press(keys);
        yield return new WaitForSeconds(seconds);
        ReleaseAll();
        yield return new WaitForSeconds(0.2f);
    }

    void Teleport(Vector3 position)
    {
        rb.linearVelocity = Vector3.zero;
        rb.position = position;
        player.transform.position = position;
    }

    [UnityTest]
    public IEnumerator StartsOnStartTile_FacingCamera()
    {
        var p = player.transform.position;
        Assert.AreEqual(StartPosition.x, p.x, 0.05f);
        Assert.AreEqual(StartPosition.z, p.z, 0.05f);
        Assert.AreEqual(0f, p.y, 0.05f, "시작 타일 위에 서 있어야 합니다.");
        Assert.Less(player.transform.forward.z, -0.9f, "카메라 쪽(-Z)을 바라봐야 합니다.");
        yield break;
    }

    [UnityTest]
    public IEnumerator D_MovesRight_AndFacesRight()
    {
        var before = player.transform.position;
        yield return Hold(0.5f, Key.D);
        var after = player.transform.position;

        Assert.AreEqual(1.5f, after.x - before.x, 0.4f, "초당 3칸 속도로 0.5초 = 약 1.5칸");
        Assert.AreEqual(before.z, after.z, 0.1f);
        Assert.AreEqual(0f, after.y, 0.05f, "타일 위에 있어야 합니다.");
        Assert.Greater(player.transform.forward.x, 0.9f, "이동 방향(+X)을 바라봐야 합니다.");
    }

    [UnityTest]
    public IEnumerator W_And_UpArrow_MoveForward()
    {
        var before = player.transform.position;
        yield return Hold(0.3f, Key.W);
        var mid = player.transform.position;
        Assert.Greater(mid.z - before.z, 0.6f, "W = 화면 위쪽(+Z)");

        yield return Hold(0.3f, Key.UpArrow);
        var after = player.transform.position;
        Assert.Greater(after.z - mid.z, 0.6f, "↑ = 화면 위쪽(+Z)");
    }

    [UnityTest]
    public IEnumerator Space_Jumps_AboutOnePointTwoTiles()
    {
        var maxY = 0f;
        Press(Key.Space);
        for (var t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            if (t > 0.1f) ReleaseAll();
            maxY = Mathf.Max(maxY, player.transform.position.y);
            yield return null;
        }

        Assert.AreEqual(1.2f, maxY, 0.2f, "점프 높이 약 1.2칸");
        Assert.AreEqual(0f, player.transform.position.y, 0.05f, "다시 착지해야 합니다.");
    }

    [UnityTest]
    public IEnumerator WalkingOffEdge_Falls()
    {
        yield return Hold(1.0f, Key.A); // 시작 위치는 왼쪽 끝 → 맵 밖
        yield return new WaitForSeconds(0.5f);
        Assert.Less(player.transform.position.y, -1f, "맵 밖으로 나가면 떨어져야 합니다.");
    }

    [UnityTest]
    public IEnumerator WalkingIntoHole_Falls()
    {
        Teleport(new Vector3(-0.5f, 0f, -0.5f)); // 타일 (1,1), 바로 위(+Z)가 구멍 (1,2)
        yield return new WaitForSeconds(0.3f);
        Assert.AreEqual(0f, player.transform.position.y, 0.05f);

        yield return Hold(0.35f, Key.W); // 약 1칸 이동 → 구멍 한가운데서 멈춤
        yield return new WaitForSeconds(0.5f);
        Assert.Less(player.transform.position.y, -1f, "구멍에 들어가면 떨어져야 합니다.");
    }

    [UnityTest]
    public IEnumerator JumpingOverHole_Lands()
    {
        Teleport(new Vector3(-0.5f, 0f, -0.5f)); // 타일 (1,1) → 구멍 (1,2) 건너 타일 (1,3)
        yield return new WaitForSeconds(0.3f);

        Press(Key.W, Key.Space);
        yield return new WaitForSeconds(0.1f);
        Press(Key.W);
        yield return new WaitForSeconds(0.55f);
        ReleaseAll();
        yield return new WaitForSeconds(0.8f);

        Assert.AreEqual(0f, player.transform.position.y, 0.05f, "구멍을 점프로 넘어 착지해야 합니다.");
        Assert.Greater(player.transform.position.z, 0.9f, "구멍 건너편 타일에 있어야 합니다.");
    }

    [UnityTest]
    public IEnumerator Camera_FollowsWithFixedAngle()
    {
        var cam = Camera.main.transform;
        var rotationBefore = cam.rotation;

        yield return Hold(0.5f, Key.D);
        yield return new WaitForSeconds(0.8f);

        var expected = player.transform.position + CameraOffset;
        Assert.Less(Vector3.Distance(expected, cam.position), 0.1f, "카메라가 플레이어를 따라가야 합니다.");
        Assert.Less(Quaternion.Angle(rotationBefore, cam.rotation), 0.1f, "카메라 각도는 고정이어야 합니다.");
    }
}
