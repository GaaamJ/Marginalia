using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 씬 전환 시 블랙아웃 페이드 연출.
/// "눈을 감고 → 암전(나레이션) → 눈을 뜨면 새 방" 컨셉.
///
/// DontDestroyOnLoad로 유지되며 GameFlowManager와 함께 동작.
///
/// [Inspector 연결]
///   fadeImage      : 전체 화면 덮는 검정 Image (Canvas — ScreenSpace Overlay)
///   screenNarrator : ScreenNarrator — 암전 중 나레이션 출력용
///   fadeOutDuration: 검게 꺼지는 시간 (눈 감기)
///   holdDuration   : 나레이션 없을 때 암전 유지 시간
///   fadeInDuration : 밝아지는 시간 (눈 뜨기)
/// </summary>
public class SceneTransitioner : MonoBehaviour
{
    public static SceneTransitioner Instance { get; private set; }

    [Header("페이드 Image (전체화면 검정)")]
    [SerializeField] private Image fadeImage;

    [Header("암전 중 나레이션 (Screen 채널)")]
    [SerializeField] private ScreenNarrator screenNarrator;

    [Header("타이밍")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private float fadeInDuration = 1.0f;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ConfigureFadeImage();
        if (fadeImage) SetAlpha(0f);
    }

    // ── 공개 API ─────────────────────────────────────────

    /// <summary>나레이션 없이 페이드아웃 → 암전 → 씬 로드 → 페이드인.</summary>
    public void TransitionTo(string sceneName)
    {
        StartCoroutine(DoTransition(sceneName, null, AudioCue.None));
    }

    /// <summary>암전 중 나레이션 포함 트랜지션.</summary>
    public void TransitionTo(string sceneName, NarrationBlock[] blocks)
    {
        StartCoroutine(DoTransition(sceneName, blocks, AudioCue.None));
    }

    public void TransitionTo(string sceneName, NarrationBlock[] blocks, AudioCue cueAfterFadeOut)
    {
        StartCoroutine(DoTransition(sceneName, blocks, cueAfterFadeOut));
    }

    /// <summary>씬 로드 없이 페이드인만 (씬 시작 시 호출).</summary>
    public void FadeIn()
    {
        StartCoroutine(DoFade(1f, 0f, fadeInDuration));
    }

    // ── 내부 ─────────────────────────────────────────────

    private IEnumerator DoTransition(string sceneName, NarrationBlock[] blocks, AudioCue cueAfterFadeOut)
    {
        yield return DoFade(0f, 1f, fadeOutDuration);

        AudioManager.PlayCue(cueAfterFadeOut);

        if (blocks != null && blocks.Length > 0 && screenNarrator != null)
            yield return screenNarrator.ShowBlocks(blocks);
        else
            yield return new WaitForSeconds(holdDuration);

        screenNarrator?.Clear();
        SceneManager.LoadScene(sceneName);
        yield return null;

        yield return DoFade(1f, 0f, fadeInDuration);
    }

    private IEnumerator DoFade(float from, float to, float duration)
    {
        SetAlpha(from);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float a)
    {
        if (!fadeImage) return;
        var c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    private void ConfigureFadeImage()
    {
        if (!fadeImage) return;

        fadeImage.raycastTarget = false;

        var color = fadeImage.color;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        fadeImage.color = color;

        var rect = fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.SetAsLastSibling();
    }
}
