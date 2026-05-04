using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// World 채널 나레이터.
/// 플레이어 행동 결과를 좌하단에 출력 (당신은 ~했습니다.).
///
/// 타이핑 완료 후 fadeHold 초 유지 → fadeDuration 초에 걸쳐 사라짐.
/// 불규칙 타이핑은 BaseNarrator.charVariance로 조정.
///
/// [Inspector 연결]
///   narratorTMP  : TextMeshProUGUI
///   canvasGroup  : CanvasGroup (FadeOut용 — TMP와 같은 오브젝트 또는 부모)
///   fadeHold     : 타이핑 완료 후 유지 시간 (초)
///   fadeDuration : FadeOut 소요 시간 (초)
/// </summary>
public class WorldNarrator : BaseNarrator
{
    [Header("World 채널 TMP")]
    [SerializeField] private TextMeshProUGUI narratorTMP;

    [Header("FadeOut 설정")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeHold = 1.5f;
    [SerializeField] private float fadeDuration = 0.8f;

    private Coroutine fadeCoroutine;

    protected override TextMeshProUGUI GetTMP() => narratorTMP;
    protected override AudioCue TypingCue => AudioCue.WorldNarratorSfx;

    // ── 훅 ───────────────────────────────────────────────

    protected override void OnBlockStart(NarrationBlock block)
    {
        // 이전 FadeOut 중단 후 즉시 불투명으로 복원
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        if (canvasGroup) canvasGroup.alpha = 1f;
    }

    protected override void OnBlockEnd(NarrationBlock block)
    {
        if (canvasGroup)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOut());
        }
    }

    // ── FadeOut 코루틴 ────────────────────────────────────

    private IEnumerator FadeOut()
    {
        // 유지
        yield return new WaitForSeconds(fadeHold);

        // 페이드
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        fadeCoroutine = null;
    }

    // ── Clear 오버라이드 — FadeOut도 함께 중단 ────────────

    public override void Clear()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        if (canvasGroup) canvasGroup.alpha = 1f;
        base.Clear();
    }

    // ── 위치 지정 ─────────────────────────────────────────

    public void SetTarget(Vector3 worldPosition)
    {
        transform.position = worldPosition;
    }
}
