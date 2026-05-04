using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Screen 채널 나레이터.
/// 타이핑 없이 텍스트 전체를 FadeIn → 유지 → FadeOut으로 표시.
/// 천체 대사처럼 묵직하게 등장했다 사라지는 연출.
///
/// BaseNarrator의 TypeText를 완전히 우회하기 위해
/// ShowText / ShowBlocks 를 직접 override한다.
///
/// 기존 스탯 호버(ShowStatDescription / RestoreText) 기능 유지.
/// StatAllocatorUI는 이 컴포넌트를 직접 참조.
///
/// [Inspector 연결]
///   narratorTMP    : TextMeshProUGUI (화면 중앙)
///   canvasGroup    : CanvasGroup (Fade 제어용)
///   fadeInDuration : FadeIn 소요 시간 (초) 기본 0.6
///   holdDuration   : 완전 불투명 유지 시간 (초) 기본 1.5
///   fadeOutDuration: FadeOut 소요 시간 (초) 기본 0.6
/// </summary>
public class ScreenNarrator : BaseNarrator
{
    [Header("Screen 채널 TMP")]
    [SerializeField] private TextMeshProUGUI narratorTMP;

    [Header("Fade 연출")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInDuration = 0.6f;
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private float fadeOutDuration = 0.6f;

    // 호버 복원용
    private string currentText = "";
    private Coroutine restoreCoroutine;
    private Coroutine fadeCoroutine;

    // ── BaseNarrator 구현 (TMP 공급만) ───────────────────

    protected override TextMeshProUGUI GetTMP() => narratorTMP;
    protected override AudioCue TypingCue => AudioCue.ScreenNarratorSfx;

    // ── ShowText / ShowBlocks override — 타이핑 완전 우회 ─

    public override IEnumerator ShowText(NarrationBlock block)
    {
        if (block == null || string.IsNullOrEmpty(block.text)) yield break;

        // 이전 Fade 중단
        if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); fadeCoroutine = null; }

        if (narratorTMP) narratorTMP.text = block.text;
        currentText = block.text;

        StartTypingCueLoop();
        yield return fadeCoroutine = StartCoroutine(FadeSequence());
        fadeCoroutine = null;
        StopTypingCueLoop();
    }

    public override IEnumerator ShowBlocks(NarrationBlock[] blocks)
    {
        if (blocks == null) yield break;
        foreach (var block in blocks)
        {
            if (block == null || string.IsNullOrEmpty(block.text)) continue;
            yield return ShowText(block);
        }
    }

    // ── Fade 시퀀스 ───────────────────────────────────────

    private IEnumerator FadeSequence()
    {
        if (canvasGroup == null) yield break;

        // FadeIn
        yield return Fade(0f, 1f, fadeInDuration);

        // Hold
        yield return new WaitForSeconds(holdDuration);

        // FadeOut
        yield return Fade(1f, 0f, fadeOutDuration);

        if (narratorTMP) narratorTMP.text = "";
        currentText = "";
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    // ── Clear override ────────────────────────────────────

    public override void Clear()
    {
        if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); fadeCoroutine = null; }
        StopTypingCueLoop();
        if (canvasGroup) canvasGroup.alpha = 0f;
        if (narratorTMP) narratorTMP.text = "";
        currentText = "";
    }

    // ── 스탯 호버 (StatAllocatorUI 전용) ─────────────────

    /// <summary>스탯 호버 시 설명 출력. 복원 대기 중이면 취소.</summary>
    public void ShowStatDescription(string desc)
    {
        if (restoreCoroutine != null) StopCoroutine(restoreCoroutine);
        if (narratorTMP) narratorTMP.text = desc;
    }

    /// <summary>호버 해제 시 호출 — 딜레이 후 currentText 복원.</summary>
    public void RestoreText()
    {
        if (restoreCoroutine != null) StopCoroutine(restoreCoroutine);
        restoreCoroutine = StartCoroutine(RestoreDelay());
    }

    private IEnumerator RestoreDelay()
    {
        yield return new WaitForSeconds(0.2f);
        if (narratorTMP) narratorTMP.text = currentText;
        restoreCoroutine = null;
    }

    // ── 레거시 호환 (TitleSceneController 등에서 직접 호출하던 메서드) ──

    /// <summary>
    /// 타이핑 없이 텍스트 즉시 교체.
    /// TitleSceneController 내부 정리 후 제거 가능.
    /// </summary>
    public void SetTextImmediate(string text)
    {
        Clear();
        if (narratorTMP) narratorTMP.text = text;
        currentText = text;
    }
}
