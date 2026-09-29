using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 코인(금화). 회전하며 위아래로 떠다닌다.
/// 7단계: 수명 3초 — 0~2초는 금색이고 먹으면 100점, 2~3초는 회색으로 깜빡이며 0점, 3초에 사라진다.
/// 칸이 떨어지면 함께 떨어져 사라진다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Item : MonoBehaviour
{
    /// <summary>코인을 먹었을 때 (GameManager가 받아 점수를 더함)</summary>
    public static event Action<Item> Collected;

    [Tooltip("회전·떠다니기·사라지기 연출을 적용할 모델")]
    [SerializeField] Transform visual;

    [Header("떠다니기")]
    [SerializeField] float spinSpeed = 120f;
    [SerializeField] float bobHeight = 0.08f;
    [SerializeField] float bobSpeed = 2f;

    [Header("점수와 수명")]
    [SerializeField] int points = 100;
    [SerializeField] float scoringTime = 2f;   // 이 시간 안에 먹어야 점수
    [SerializeField] float lifetime = 3f;      // 이 시간이 지나면 사라짐
    [SerializeField] Material expiredMaterial; // 점수 시간이 지난 뒤 모습 (회색)
    [SerializeField] float blinkInterval = 0.1f;

    [Header("획득 연출")]
    [SerializeField] float collectTime = 0.3f;
    [SerializeField] float collectRise = 0.4f;

    [Header("떨어짐")]
    [SerializeField] float dropGravity = 20f;
    [SerializeField] float dropTime = 0.8f;

    public bool IsCollected { get; private set; }
    public bool IsExpired { get; private set; }
    public bool IsDropped { get; private set; }
    public float Age { get; private set; }
    /// <summary>먹었을 때 얻은 점수 (늦게 먹으면 0)</summary>
    public int CollectedPoints { get; private set; }
    /// <summary>지금 먹으면 얻는 점수</summary>
    public int CurrentPoints => Age < scoringTime ? points : 0;

    Renderer[] renderers;
    Vector3 visualStart;
    float phase;

    void Awake()
    {
        if (visual == null) visual = transform;
        renderers = visual.GetComponentsInChildren<Renderer>();
        visualStart = visual.localPosition;
        phase = transform.position.x * 1.3f + transform.position.z * 0.7f;
    }

    void Update()
    {
        if (IsCollected || IsDropped) return;

        Age += Time.deltaTime;
        visual.localRotation = Quaternion.Euler(0f, Time.time * spinSpeed, 0f);
        visual.localPosition = visualStart + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight);

        if (Age >= scoringTime)
        {
            if (!IsExpired)
            {
                IsExpired = true;
                if (expiredMaterial != null)
                    foreach (var r in renderers) r.sharedMaterial = expiredMaterial;
            }
            // 깜빡임
            var on = Mathf.FloorToInt((Age - scoringTime) / blinkInterval) % 2 == 0;
            foreach (var r in renderers) r.enabled = on;
        }

        if (Age >= lifetime) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsCollected || IsDropped || other.GetComponentInParent<PlayerController>() == null) return;

        IsCollected = true;
        CollectedPoints = CurrentPoints;
        GetComponent<Collider>().enabled = false;
        foreach (var r in renderers) r.enabled = true;
        Collected?.Invoke(this);
        StartCoroutine(CollectEffect());
    }

    /// <summary>칸이 떨어질 때 함께 떨어진다.</summary>
    public void Drop()
    {
        if (IsCollected || IsDropped) return;
        IsDropped = true;
        GetComponent<Collider>().enabled = false;
        StartCoroutine(DropEffect());
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
            visual.localRotation = startRot * Quaternion.Euler(0f, 720f * ease, 0f);
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator DropEffect()
    {
        var speed = 0f;
        for (var t = 0f; t < dropTime; t += Time.deltaTime)
        {
            speed += dropGravity * Time.deltaTime;
            transform.position += Vector3.down * (speed * Time.deltaTime);
            yield return null;
        }
        Destroy(gameObject);
    }
}
