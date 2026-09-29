using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 금화 아이템. 회전하며 위아래로 떠다니고, 플레이어가 닿으면 작아지며 떠올라 사라진다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Item : MonoBehaviour
{
    /// <summary>아이템을 먹었을 때 (UIManager, 이후 GameManager가 받음)</summary>
    public static event Action<Item> Collected;

    [Tooltip("회전·떠다니기·사라지기 연출을 적용할 모델")]
    [SerializeField] Transform visual;

    [Header("떠다니기")]
    [SerializeField] float spinSpeed = 120f;     // 초당 회전 각도
    [SerializeField] float bobHeight = 0.08f;    // 위아래 폭
    [SerializeField] float bobSpeed = 2f;        // 초당 왕복 횟수 × 2π

    [Header("획득 연출")]
    [SerializeField] float collectTime = 0.3f;
    [SerializeField] float collectRise = 0.4f;

    public bool IsCollected { get; private set; }

    Vector3 visualStart;
    float phase;

    void Awake()
    {
        if (visual == null) visual = transform;
        visualStart = visual.localPosition;
        // 아이템마다 떠다니는 박자를 조금씩 다르게
        phase = transform.position.x * 1.3f + transform.position.z * 0.7f;
    }

    void Update()
    {
        if (IsCollected) return;
        visual.localRotation = Quaternion.Euler(0f, Time.time * spinSpeed, 0f);
        visual.localPosition = visualStart + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight);
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsCollected || other.GetComponentInParent<PlayerController>() == null) return;

        IsCollected = true;
        GetComponent<Collider>().enabled = false;
        Collected?.Invoke(this);
        StartCoroutine(CollectEffect());
    }

    IEnumerator CollectEffect()
    {
        var startPos = visual.localPosition;
        var startScale = visual.localScale;
        var startRot = visual.localRotation;
        for (var t = 0f; t < collectTime; t += Time.deltaTime)
        {
            var k = t / collectTime;
            var ease = 1f - (1f - k) * (1f - k);
            visual.localPosition = startPos + Vector3.up * (collectRise * ease);
            visual.localScale = startScale * (1f - ease);
            visual.localRotation = startRot * Quaternion.Euler(0f, 720f * ease, 0f); // 빠르게 돌며 사라짐
            yield return null;
        }
        Destroy(gameObject);
    }
}
