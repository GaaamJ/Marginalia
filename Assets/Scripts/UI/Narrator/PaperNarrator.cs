using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Paper 채널 나레이터.
/// PaperMesh(3D) 위 Canvas에 지시사항을 타이핑으로 표시.
/// 기본값 appendMode = true (누적 모드). false로 설정 시 블록마다 초기화.
/// 타이핑 완료 후 흔들림 — NarrationBlock.shakeIntensity로 조정.
///
/// [씬 구조]
///   PaperUI (Canvas — Screen Space Camera, PaperCam 연결)
///    └── PaperPanel
///         └── Preview (TMP UI)  ← previewTMP 연결
///
/// [Inspector 연결]
///   previewTMP : 지시사항 TMP
/// </summary>
public class PaperNarrator : BaseNarrator
{
    [Header("TMP")]
    [SerializeField] private TextMeshProUGUI previewTMP;

    protected override TextMeshProUGUI GetTMP() => previewTMP;
    protected override AudioCue TypingCue => AudioCue.PaperNarratorSfx;

    // ── ShowText override ────────────────────────────────
    public override IEnumerator ShowText(NarrationBlock block)
    {
        if (block == null || string.IsNullOrEmpty(block.text)) yield break;

        // appendMode가 false(기본)면 매 블록 초기화
        if (!block.appendMode && previewTMP != null)
            previewTMP.text = "";

        // 타이핑 + 흔들림은 BaseNarrator에 위임
        yield return base.ShowText(block);
    }

    public override IEnumerator ShowBlocks(NarrationBlock[] blocks)
    {
        if (blocks == null) yield break;
        foreach (var block in blocks)
            yield return ShowText(block);
    }

    // ── Clear override ────────────────────────────────────
    public override void Clear()
    {
        if (previewTMP) previewTMP.text = "";
        base.Clear();
    }
}
