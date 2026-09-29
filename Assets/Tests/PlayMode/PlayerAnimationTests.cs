using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// 3단계 확인: Idle / Walk / Jump 애니메이션과 전환.
/// </summary>
public class PlayerAnimationTests : PlayModeTestBase
{
    Animator animator;
    Transform model, armL, legL, legR;

    [UnitySetUp]
    public IEnumerator FindParts()
    {
        animator = player.GetComponentInChildren<Animator>();
        Assert.IsNotNull(animator, "Model에 Animator가 있어야 합니다.");
        model = animator.transform;
        armL = model.Find("Arm_L");
        legL = model.Find("Leg_L");
        legR = model.Find("Leg_R");
        yield break;
    }

    bool InState(string name) => animator.GetCurrentAnimatorStateInfo(0).IsName(name);
    static float Angle(float euler) => Mathf.Abs(Mathf.DeltaAngle(0f, euler));

    [UnityTest]
    public IEnumerator StartsInIdle_AndBreathes()
    {
        Assert.IsTrue(InState("Idle"), "시작 상태는 Idle이어야 합니다.");
        float minY = 10f, maxY = 0f;
        for (var t = 0f; t < 3f; t += Time.deltaTime)
        {
            minY = Mathf.Min(minY, model.localScale.y);
            maxY = Mathf.Max(maxY, model.localScale.y);
            yield return null;
        }
        Assert.Greater(maxY - minY, 0.01f, "숨쉬기로 몸 세로 크기가 변해야 합니다.");
        Assert.Less(maxY, 1.05f, "숨쉬기는 작게 움직여야 합니다.");
    }

    [UnityTest]
    public IEnumerator Moving_PlaysWalk_WithSmallLegSwing()
    {
        Press(Key.D);
        float maxArm = 0f, maxLeg = 0f, maxBob = 0f;
        for (var t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            maxArm = Mathf.Max(maxArm, Angle(armL.localEulerAngles.x));
            maxLeg = Mathf.Max(maxLeg, Angle(legL.localEulerAngles.x), Angle(legR.localEulerAngles.x));
            maxBob = Mathf.Max(maxBob, model.localPosition.y);
            yield return null;
        }
        Assert.IsTrue(InState("Walk"), "이동 중에는 Walk여야 합니다.");
        ReleaseAll();

        Assert.Greater(maxArm, 25f, "팔을 크게 흔들어야 합니다 (약 35°).");
        Assert.LessOrEqual(maxLeg, 16f, "다리는 치마를 뚫지 않도록 약 15° 이하로 흔들어야 합니다.");
        Assert.Greater(maxBob, 0.015f, "걸을 때 몸이 위로 튀어야 합니다.");

        yield return new WaitForSeconds(0.5f);
        Assert.IsTrue(InState("Idle"), "멈추면 Idle로 돌아와야 합니다.");
    }

    [UnityTest]
    public IEnumerator Jump_RaisesArms_Stretches_ThenLands()
    {
        Press(Key.Space);
        float maxStretch = 0f, maxArmRaise = 0f;
        var sawJump = false;
        for (var t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            if (t > 0.1f) ReleaseAll();
            sawJump |= InState("Jump");
            maxStretch = Mathf.Max(maxStretch, model.localScale.y);
            maxArmRaise = Mathf.Max(maxArmRaise, Angle(armL.localEulerAngles.z));
            yield return null;
        }
        Assert.IsTrue(sawJump, "점프하면 Jump 상태가 되어야 합니다.");
        Assert.Greater(maxStretch, 1.05f, "점프할 때 몸이 세로로 늘어나야 합니다.");
        Assert.Greater(maxArmRaise, 120f, "점프할 때 팔을 들어 올려야 합니다 (약 140°).");

        yield return new WaitForSeconds(1.0f);
        Assert.IsTrue(InState("Idle"), "착지하면 Idle로 돌아와야 합니다.");
        Assert.AreEqual(1f, model.localScale.y, 0.03f, "착지 후 몸 크기가 원래대로 돌아와야 합니다.");
    }

    [UnityTest]
    public IEnumerator FallingWithoutJump_PlaysJump()
    {
        Press(Key.A); // 시작 위치는 왼쪽 끝 → 맵 밖으로 떨어짐
        var sawJump = false;
        for (var t = 0f; t < 1.0f; t += Time.deltaTime)
        {
            sawJump |= InState("Jump");
            yield return null;
        }
        ReleaseAll();
        Assert.Less(player.transform.position.y, -0.5f, "맵 밖으로 떨어져야 합니다.");
        Assert.IsTrue(sawJump, "점프하지 않고 떨어져도 공중이면 Jump 동작이어야 합니다.");
    }
}
