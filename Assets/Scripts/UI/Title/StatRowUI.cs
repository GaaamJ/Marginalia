using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// 스탯 1행 프리팹에 붙이는 컴포넌트.
///
/// 세그먼트 클릭 방식:
///   - 세그먼트를 클릭하면 "그 칸 번호"가 새 값이 됨.
///   - 이미 켜진 칸을 클릭하면 그 칸으로 줄어듦 (토글 다운).
///   - 켜진 칸들은 펜 획 스프라이트(ScratchSprite)로 표시.
///
/// 프리팹 계층 예시:
///   StatRow  (StatRowUI, PointerEnter 감지)
///     ├─ LabelTMP           (TextMeshPro)
///     └─ SegmentContainer   (HorizontalLayoutGroup)
///        ├─ Segment_1       (Button + Image)
///        ├─ Segment_2
///        ├─ Segment_3
///        └─ Segment_4
///
/// [Inspector 연결]
///   labelTMP        : 스탯 이름 TMP
///   segmentButtons  : 세그먼트 버튼 배열
///   scratchObjects  : 펜 획 스프라이트 배열
///
/// [Typing Reveal]
///   charInterval    : 글자 하나 나오는 간격 (기본 0.05s)
///   segmentRevealDelay : 라벨 타이핑 끝난 후 세그먼트 등장까지 딜레이
/// </summary>
public class StatRowUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI labelTMP;

    [Header("Segments")]
    [SerializeField] private Button[] segmentButtons;
    [SerializeField] private Image[] segmentBGs;     // 각 Segment의 BG Image
    [SerializeField] private GameObject[] scratchObjects;

    [Header("Colors")]
    [SerializeField] private Color colorOn = Color.white;
    [SerializeField] private Color colorOff = new(0.25f, 0.25f, 0.25f);

    [Header("Typing Reveal")]
    [SerializeField] private float charInterval = 0.08f;
    [SerializeField] private float segmentRevealDelay = 0.15f;
    [SerializeField] private float segmentInterval = 0.08f;

    [Header("Audio")]
    [SerializeField] private AudioCue revealCue = AudioCue.PaperNarratorSfx;

    private int currentValue;
    private Action<int> onValueChanged;
    private Action onHover;
    private Action onExit;

    // ── 초기화 ───────────────────────────────────────────
    private void Awake()
    {
        if (labelTMP) labelTMP.maxVisibleCharacters = 0;
        SetSegmentsInteractable(false);
        SetSegmentsVisible(false);
    }
    public void Init(
        StatData.StatEntry entry,
        Action<int> onValueChanged,
        Action onHover,
        Action onExit)
    {
        this.onValueChanged = onValueChanged;
        this.onHover = onHover;
        this.onExit = onExit;

        if (labelTMP)
        {
            labelTMP.text = entry.displayName;
            labelTMP.maxVisibleCharacters = 0;
        }

        // 세그먼트 초기 비활성화 (reveal 전까지 클릭 불가)
        SetSegmentsInteractable(false);
        SetSegmentsVisible(false);
        SetValue(0);

        for (int i = 0; i < segmentButtons.Length; i++)
        {
            int segIndex = i + 1;
            segmentButtons[i].onClick.AddListener(() => OnSegmentClicked(segIndex));
        }
    }

    // ── Reveal ───────────────────────────────────────────

    /// <summary>
    /// 라벨 타이핑 → 세그먼트 등장 순서로 reveal.
    /// StatAllocatorUI에서 yield return StartCoroutine(row.Reveal()) 으로 호출.
    /// </summary>
    public IEnumerator Reveal()
    {
        AudioManager.PlayCue(revealCue);

        // 1. 라벨 타이핑
        if (labelTMP != null)
        {
            int total = labelTMP.text.Length;
            for (int i = 0; i <= total; i++)
            {
                labelTMP.maxVisibleCharacters = i;
                yield return new WaitForSeconds(charInterval);
            }
        }

        yield return new WaitForSeconds(segmentRevealDelay);

        // 2. 세그먼트 BG 하나씩 등장
        for (int i = 0; i < segmentBGs.Length; i++)
        {
            if (segmentBGs[i] != null)
                segmentBGs[i].gameObject.SetActive(true);
            yield return new WaitForSeconds(segmentInterval);
        }

        SetSegmentsInteractable(true);
    }

    // ── 세그먼트 클릭 ────────────────────────────────────

    private void OnSegmentClicked(int clickedIndex)
    {
        int newVal = (clickedIndex == currentValue) ? 0 : clickedIndex;
        onValueChanged?.Invoke(newVal);
    }

    // ── 값 갱신 ──────────────────────────────────────────

    public void ShowImmediate(int val)
    {
        if (labelTMP) labelTMP.maxVisibleCharacters = int.MaxValue;
        foreach (var bg in segmentBGs)
            if (bg) bg.gameObject.SetActive(true);
        SetSegmentsInteractable(false);
        SetValue(val);
    }

    public void SetValue(int val)
    {
        currentValue = Mathf.Clamp(val, 0, PlayerStats.MAX_STAT);

        for (int i = 0; i < segmentButtons.Length; i++)
        {
            bool on = (i + 1) <= currentValue;

            var img = segmentButtons[i].GetComponent<Image>();
            if (img) img.color = on ? colorOn : colorOff;

            if (scratchObjects != null && i < scratchObjects.Length && scratchObjects[i])
                scratchObjects[i].SetActive(on);
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────

    private void SetSegmentsInteractable(bool interactable)
    {
        foreach (var btn in segmentButtons)
            if (btn) btn.interactable = interactable;
    }

    private void SetSegmentsVisible(bool visible)
    {
        foreach (var bg in segmentBGs)
            if (bg) bg.gameObject.SetActive(visible);
    }

    // ── 호버 ─────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _) => onHover?.Invoke();
    public void OnPointerExit(PointerEventData _) => onExit?.Invoke();
}
