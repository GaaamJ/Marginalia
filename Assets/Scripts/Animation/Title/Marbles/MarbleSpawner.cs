using UnityEngine;
using System;
using System.Collections;
using MoreMountains.Feedbacks;

/// <summary>
/// 씬에 미리 배치된 구슬들을 순서대로 PopIn → 낙하시키는 컴포넌트.
///
/// 동작 흐름:
///   씬 시작 시 — 구슬 전부 isKinematic=true, Scale(0,0,0) 상태로 대기
///   SpawnAll() 호출
///   → 구슬 순서대로 MMF_Player PlayFeedbacks() (PopIn 스케일 연출)
///   → TotalDuration 후 isKinematic=false → 중력 낙하 시작
///   → 착지 후 랜덤 방향 + 랜덤 세기로 굴러감
///   → 전부 안착 → OnAllSettled 이벤트 + 콜백
///
/// [Inspector 연결]
///   marbles       : 씬에 배치된 구슬 GameObject 배열
///                   각 구슬에 Rigidbody + SphereCollider + PhysicsMaterial + MMF_Player 필요
///
/// [Timing]
///   spawnInterval         : 구슬 간 기본 딜레이 (기본 0.15s)
///   spawnIntervalVariance : 딜레이 랜덤 편차 ±값 (기본 0.05s)
///
/// [Roll]
///   rollSpeedMax  : 착지 후 수평 속도 상한선 (0 ~ 이 값 사이 랜덤)
///
/// [Settle Detection]
///   settleSpeed   : 이 속도 이하 → 안착 판정 (기본 0.05)
///   settleDelay   : 안착 판정 유지 시간 (기본 0.3s)
///
/// [Prefab MMF_Player 권장 세팅 (PopIn)]
///   MMF_Scale : startScale(0,0,0) → targetScale(1,1,1), duration 0.15s, Ease Out Back
///
/// [PhysicsMaterial 권장값]
///   Bounciness : 0.55 / Bounce Combine : Maximum
///   Dynamic Friction : 0.4 ~ 0.8
/// </summary>
public class MarbleSpawner : MonoBehaviour
{
    [Header("Marbles (씬에 배치)")]
    [SerializeField] private GameObject[] marbles;

    [Header("Timing")]
    [SerializeField] private float spawnInterval = 0.15f;
    [SerializeField] private float spawnIntervalVariance = 0.05f;

    [Header("Roll")]
    [Tooltip("착지 후 수평 속도 상한선. 0 ~ 이 값 사이에서 랜덤")]
    [SerializeField] private float rollSpeedMax = 1.5f;

    [Header("Settle Detection")]
    [SerializeField] private float settleSpeed = 0.05f;
    [SerializeField] private float settleDelay = 0.3f;

    [Header("Impact Detection")]
    [SerializeField] private float impactMinFallSpeed = 0.25f;
    [SerializeField] private float impactCooldown = 0.08f;

    public event Action OnAllSettled;

    // ─────────────────────────────────────────
    //  Unity
    // ─────────────────────────────────────────

    private void Awake()
    {
        // 시작 시 전부 잠금 (씬 배치 상태 보정)
        foreach (var marble in marbles)
        {
            if (marble == null) continue;
            var rb = marble.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            marble.transform.localScale = Vector3.zero;
        }
    }

    // ─────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────

    /// <summary>
    /// 구슬 전체를 순서대로 PopIn → 낙하시킨다. 전부 안착 후 콜백 실행.
    /// yield return StartCoroutine(marbleSpawner.SpawnAll(...))
    /// </summary>
    public IEnumerator SpawnAll(Action onAllSettled = null)
    {
        if (marbles == null || marbles.Length == 0)
        {
            Debug.LogError("[MarbleSpawner] marbles 배열 비어있음");
            onAllSettled?.Invoke();
            yield break;
        }

        int total = marbles.Length;
        int settledCount = 0;

        for (int i = 0; i < total; i++)
        {
            if (marbles[i] == null)
            {
                settledCount++;
                continue;
            }

            float angle = UnityEngine.Random.Range(0f, 360f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float speed = UnityEngine.Random.Range(0f, rollSpeedMax);

            StartCoroutine(ActivateOne(marbles[i], dir, speed, () => settledCount++));

            if (i < total - 1)
            {
                float delay = spawnInterval + UnityEngine.Random.Range(-spawnIntervalVariance, spawnIntervalVariance);
                yield return new WaitForSeconds(Mathf.Max(0f, delay));
            }
        }

        yield return new WaitUntil(() => settledCount >= total);

        OnAllSettled?.Invoke();
        onAllSettled?.Invoke();
    }

    // ─────────────────────────────────────────
    //  Internal
    // ─────────────────────────────────────────

    private IEnumerator ActivateOne(GameObject marble, Vector3 rollDir, float speed, Action onSettled)
    {
        var rb = marble.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"[MarbleSpawner] {marble.name} 에 Rigidbody 없음 — 스킵");
            onSettled?.Invoke();
            yield break;
        }

        // PopIn 연출
        var feedback = marble.GetComponent<MMF_Player>();
        if (feedback != null)
        {
            feedback.PlayFeedbacks();
            yield return new WaitForSeconds(feedback.TotalDuration);
        }
        else
        {
            // MMF 없으면 즉시 스케일 복원
            marble.transform.localScale = Vector3.one;
        }

        // 중력 해제 → 낙하 시작
        rb.isKinematic = false;

        // 낙하 중 체크 방지
        yield return new WaitForSeconds(0.3f);

        // 착지/바운스 감지 → 첫 착지에는 수평 속도 주입, 이후 바운스도 SFX 재생
        bool touchedDown = false;
        float previousYVelocity = rb.linearVelocity.y;
        float impactTimer = impactCooldown;
        float stillTime = 0f;
        while (stillTime < settleDelay)
        {
            if (rb == null) break;

            impactTimer += Time.deltaTime;
            float currentYVelocity = rb.linearVelocity.y;
            bool impactDetected =
                previousYVelocity < -impactMinFallSpeed &&
                currentYVelocity > -0.1f &&
                impactTimer >= impactCooldown;

            if (impactDetected)
            {
                impactTimer = 0f;

                if (!touchedDown)
                {
                    touchedDown = true;
                    if (speed > 0f)
                        rb.linearVelocity = new Vector3(rollDir.x * speed, rb.linearVelocity.y, rollDir.z * speed);
                }
            }

            if (rb.linearVelocity.magnitude < settleSpeed)
                stillTime += Time.deltaTime;
            else
                stillTime = 0f;

            previousYVelocity = rb.linearVelocity.y;
            yield return null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        onSettled?.Invoke();
    }
}
