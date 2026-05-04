using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// ScreenNarrator / WorldNarrator / PaperNarrator의 공통 베이스.
/// 타이핑 코루틴, 흔들림 루프, Skip 신호를 모두 여기서 처리한다.
///
/// 서브클래스는 TMP 컴포넌트를 GetTMP()로 제공하고,
/// 필요하다면 OnBlockStart / OnBlockEnd를 override해서
/// 채널 고유 연출(화면 확대, WorldSpace 위치 조정 등)을 추가한다.
///
/// [Inspector 연결 — 공통]
///   charInterval        : 글자 타이핑 간격 (초)
///   defaultPauseAfter   : block.pauseAfter == 0 일 때 사용하는 기본 대기 시간
/// </summary>
public abstract class BaseNarrator : MonoBehaviour, INarrator
{
    [Header("타이핑 공통 설정")]
    [SerializeField] protected float charInterval = 0.035f;
    [SerializeField] protected float defaultPauseAfter = 0.8f;

    /// <summary>
    /// 글자당 타이핑 간격의 랜덤 편차 (0이면 균일).
    /// 실제 간격 = charInterval ± charVariance 범위 랜덤.
    /// 뚝뚝 끊기는 느낌은 0.05~0.1 권장.
    /// </summary>
    [SerializeField] protected float charVariance = 0f;

    [Header("Typing SFX")]
    [SerializeField] private float typingCueInterval = 0.06f;

    // ── 내부 상태 ─────────────────────────────────────────

    private Coroutine typingCoroutine;
    private Coroutine shakeCoroutine;
    private Coroutine typingCueCoroutine;

    /// <summary>타이핑 즉시 완성 플래그 (Skip() 호출 시 true).</summary>
    private bool skipTyping = false;

    /// <summary>ShowBlocks에서 현재 블록을 완성 후 다음으로 넘길 플래그.</summary>
    private bool advanceBlock = false;

    /// <summary>ShakeLoop 시작 시점의 TMP anchoredPosition. 복원에 사용.</summary>
    private Vector2 shakeOrigin = Vector2.zero;

    // ── INarrator 구현 ────────────────────────────────────

    public virtual IEnumerator ShowBlocks(NarrationBlock[] blocks)
    {
        if (blocks == null) yield break;

        foreach (var block in blocks)
        {
            if (block == null || string.IsNullOrEmpty(block.text)) continue;

            advanceBlock = false;
            yield return ShowText(block);

            // 타이핑 완성 후 pause 대기.
            // 이 대기 중 Skip() 호출 → advanceBlock = true → 즉시 다음 블록.
            float pause = block.pauseAfter > 0f ? block.pauseAfter : defaultPauseAfter;
            float elapsed = 0f;
            while (elapsed < pause)
            {
                if (advanceBlock) break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    public virtual IEnumerator ShowText(NarrationBlock block)
    {
        if (block == null || string.IsNullOrEmpty(block.text)) yield break;

        StopTyping();
        OnBlockStart(block);

        var tmp = GetTMP();
        if (tmp == null) yield break;

        skipTyping = false;
        // 타이핑 시작 전 origin 저장 — 흔들림 복원 기준점
        var rect = tmp.rectTransform;
        if (rect != null) shakeOrigin = rect.anchoredPosition;
        StartTypingCueLoop();
        typingCoroutine = StartCoroutine(TypeText(block.text, tmp, block.shakeIntensity));
        yield return typingCoroutine;
        typingCoroutine = null;
        StopTypingCueLoop();

        // 타이핑 완료 후 흔들림 시작
        if (block.shakeIntensity > 0f)
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeLoop(tmp, block.shakeIntensity, block.shakeFrequency));
        }

        OnBlockEnd(block);
    }

    public virtual void Clear()
    {
        StopTyping();
        var tmp = GetTMP();
        if (tmp != null) tmp.text = "";
    }

    public void Skip()
    {
        if (typingCoroutine != null)
        {
            // 타이핑 중 → 즉시 완성
            skipTyping = true;
        }
        else
        {
            // 이미 완성 상태 → 다음 블록으로
            advanceBlock = true;
        }
    }

    // ── 서브클래스 훅 ─────────────────────────────────────

    /// <summary>채널 고유 TMP 컴포넌트 반환. 서브클래스에서 구현 필수.</summary>
    protected abstract TextMeshProUGUI GetTMP();

    protected virtual AudioCue TypingCue => AudioCue.None;

    protected void StartTypingCueLoop()
    {
        StopTypingCueLoop();
        if (TypingCue == AudioCue.None) return;
        typingCueCoroutine = StartCoroutine(TypingCueLoop());
    }

    protected void StopTypingCueLoop()
    {
        if (typingCueCoroutine == null) return;
        StopCoroutine(typingCueCoroutine);
        typingCueCoroutine = null;
    }

    /// <summary>블록 출력 시작 직전 호출. 채널 고유 연출 (확대, 위치 등).</summary>
    protected virtual void OnBlockStart(NarrationBlock block) { }

    /// <summary>블록 타이핑 완료 직후 호출.</summary>
    protected virtual void OnBlockEnd(NarrationBlock block) { }

    // ── 타이핑 코루틴 ────────────────────────────────────

    private IEnumerator TypeText(string text, TextMeshProUGUI tmp, float shakeIntensity = 0f)
    {
        tmp.text = "";
        // 타이핑 시작 시 origin 저장 — 흔들림 복원 기준점
        shakeOrigin = tmp.rectTransform.anchoredPosition;

        foreach (char c in text)
        {
            if (skipTyping)
            {
                tmp.text = text;
                skipTyping = false;
                // 흔들림 위치 복원
                if (shakeIntensity > 0f)
                    tmp.rectTransform.anchoredPosition = shakeOrigin;
                yield break;
            }

            tmp.text += c;

            // 타이핑 중 미세 흔들림
            if (shakeIntensity > 0f)
                tmp.rectTransform.anchoredPosition = shakeOrigin + new Vector2(
                    Random.Range(-shakeIntensity, shakeIntensity),
                    Random.Range(-shakeIntensity, shakeIntensity));

            float interval = charVariance > 0f
                ? Mathf.Max(0f, charInterval + Random.Range(-charVariance, charVariance))
                : charInterval;
            yield return new WaitForSeconds(interval);
        }
    }

    // ── 흔들림 루프 ───────────────────────────────────────

    /// <summary>
    /// 타이핑 완료 후 지속되는 흔들림.
    /// Clear() / 다음 블록 시작 시 StopTyping()으로 중단.
    /// </summary>
    private IEnumerator TypingCueLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.01f, typingCueInterval));
        while (true)
        {
            AudioManager.PlayCue(TypingCue);
            yield return wait;
        }
    }

    private IEnumerator ShakeLoop(TextMeshProUGUI tmp, float intensity, float frequency)
    {
        float interval = 1f / Mathf.Max(frequency, 0.1f);
        var rect = tmp.rectTransform;
        shakeOrigin = rect.anchoredPosition;
        var origin = shakeOrigin;

        while (true)
        {
            rect.anchoredPosition = origin + new Vector2(
                Random.Range(-intensity, intensity),
                Random.Range(-intensity, intensity)
            );
            yield return new WaitForSeconds(interval);
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────

    private void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
            // 흔들림으로 이동한 위치 복원 (origin 기준)
            var tmp = GetTMP();
            if (tmp != null) tmp.rectTransform.anchoredPosition = shakeOrigin;
        }
        StopTypingCueLoop();
        skipTyping = false;
    }

    private void OnDestroy()
    {
        StopTyping();
    }
}
