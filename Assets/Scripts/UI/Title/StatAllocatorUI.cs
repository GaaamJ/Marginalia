using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;

/// <summary>
/// P02 스탯 분배 UI. 씬에 미리 배치된 StatRowUI 배열을 순서대로 reveal.
///
/// 동작 흐름:
///   Activate() 호출
///   → 각 StatRowUI 순서대로 Reveal() (타이핑 → 세그먼트 등장)
///   → 세그먼트 클릭 → TrySet() → allocation 갱신 → scratch 표시
///   → 하단 서명 버튼 클릭 → ■ 타이핑 → CommitStats() → onConfirm 콜백
///
/// [Inspector 연결]
///   statData          : StatData SO
///   rows              : 씬에 배치된 StatRowUI 배열 (StatType 순서대로)
///   signatureButton   : 하단 밑줄 이미지 Button
///   signatureTMP      : 밑줄 위 TMP (■ 타이핑 표시)
///
/// [Reveal 설정]
///   rowRevealInterval : Row 간 딜레이. 0이면 순서대로 완료 후 다음 시작.
///   signatureCount    : ■ 개수 (기본 12)
///   signatureInterval : ■ 하나 나오는 간격 (기본 0.08s)
/// </summary>
public class StatAllocatorUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private StatData statData;

    [Header("UI — 씬에 배치된 Row (StatType 순서대로)")]
    [SerializeField] private StatRowUI[] rows;

    [Header("Signature")]
    [SerializeField] private Button signatureButton;
    [SerializeField] private TextMeshProUGUI signatureTMP;
    [SerializeField] private string signatureString = "■■■■■■■■■■■■";
    [SerializeField] private float signatureInterval = 0.08f;
    [Header("Signature")]
    [SerializeField] private MMF_Player signatureAppearFeel;

    [Header("Reveal")]
    [Tooltip("Row 간 reveal 시작 딜레이. 0이면 순서대로 완료 후 다음 시작.")]
    [SerializeField] private float rowRevealInterval = 0.0f;

    private readonly Dictionary<StatType, int> allocation = new();
    private readonly Dictionary<StatType, StatRowUI> rowMap = new();
    private int remainingPoints;

    private void Awake()
    {
        foreach (StatType t in Enum.GetValues(typeof(StatType)))
            allocation[t] = 0;
        remainingPoints = PlayerStats.TOTAL_POINTS;

        if (signatureTMP) signatureTMP.text = "";
        if (signatureButton) signatureButton.interactable = false;
    }

    // ── 공개 API ──────────────────────────────────────────

    /// <summary>
    /// 타이핑 reveal → 서명 대기 → ■ 타이핑 → CommitStats → onConfirm 호출.
    /// yield return StartCoroutine(statAllocatorUI.Activate(...))
    /// </summary>
    public IEnumerator Activate(Action onConfirm = null)
    {
        signatureButton.gameObject.SetActive(false);
        InitRows();
        yield return StartCoroutine(RevealAllRows());

        signatureButton.gameObject.SetActive(true); // 켜도 alpha 0이라 안 보임
        if (signatureAppearFeel != null)
        {
            signatureAppearFeel.PlayFeedbacks();
            yield return new WaitForSeconds(signatureAppearFeel.TotalDuration);
        }

        // 서명 버튼 활성화 + 클릭 대기
        if (signatureButton != null)
        {
            signatureButton.interactable = true;

            bool signed = false;
            signatureButton.onClick.AddListener(() =>
            {
                signed = true;
            });
            yield return new WaitUntil(() => signed);
            signatureButton.onClick.RemoveAllListeners();
            signatureButton.interactable = false;
        }

        // ■ 타이핑
        yield return StartCoroutine(RevealSignature());

        CommitStats();
        onConfirm?.Invoke();
    }

    public void Deactivate() => gameObject.SetActive(false);

    public void CommitStats() => PlayerStats.Instance.Apply(allocation);

    // ── 내부 ──────────────────────────────────────────────

    private void InitRows()
    {
        rowMap.Clear();

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null) continue;
            var entry = i < statData.stats.Length ? statData.stats[i] : null;
            if (entry == null) continue;

            rowMap[entry.type] = rows[i];
            rows[i].Init(
                entry,
                onValueChanged: newVal => TrySet(entry.type, newVal),
                onHover: null,
                onExit: null
            );
        }
    }

    private void TrySet(StatType type, int newVal)
    {
        int prev = allocation[type];
        int delta = newVal - prev;
        int newRemain = remainingPoints - delta;

        if (newRemain < 0) return;
        if (newVal < PlayerStats.MIN_STAT || newVal > PlayerStats.MAX_STAT) return;

        allocation[type] = newVal;
        remainingPoints = newRemain;

        rowMap[type].SetValue(newVal);
    }

    private IEnumerator RevealSignature()
    {
        if (signatureTMP == null) yield break;

        signatureTMP.text = "";
        foreach (char c in signatureString)
        {
            signatureTMP.text += c;
            yield return new WaitForSeconds(signatureInterval);
        }
    }

    private IEnumerator RevealAllRows()
    {
        if (rowRevealInterval <= 0f)
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                yield return StartCoroutine(row.Reveal());
            }
        }
        else
        {
            foreach (var row in rows)
            {
                if (row == null) continue;
                StartCoroutine(row.Reveal());
                yield return new WaitForSeconds(rowRevealInterval);
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
}
