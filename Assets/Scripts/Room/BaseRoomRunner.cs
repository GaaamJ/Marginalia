using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// NormalRoomRunner / EncoreRoomRunner의 공통 Phase 실행 로직.
/// Template Method 패턴.
///
/// 서브클래스 구현 필수:
///   GetRoomPhases()   — 이번 방의 PhaseData 배열 반환
///   GetRoomLabel()    — 로그용 방 식별자
///   OnRoomComplete()  — 방 완료 시 호출 (NextRoom or Encore)
///
/// Phase 흐름:
///   OnPhaseEnter 연출
///   → onEnter 나레이션
///   → (Check) onBeforeCheck → 판정 연출
///   → OnPhaseExit 연출
///   → OutcomeData.narration
///   → 결과 분기
/// </summary>
public abstract class BaseRoomRunner : IRoomRunner
{
    protected readonly HashSet<string> completedPhases = new();
    protected readonly HashSet<string> succeededPhases = new();
    protected RoomRunContext ctx;
    private bool roomIsLeaving;

    /// <summary>
    /// 판정 연출 담당. RoomSceneController에서 주입.
    /// null이면 phase.animator 폴백 또는 연출 스킵.
    /// </summary>
    public CheckPhaseAnimator CheckAnimator { get; set; }

    // ── IRoomRunner ───────────────────────────────────────

    public IEnumerator Run(RoomRunContext context)
    {
        ctx = context;

        var phases = GetRoomPhases();
        if (phases == null)
        {
            Debug.LogError($"[{GetRoomLabel()}] PhaseData가 null.");
            yield break;
        }

        // RoomStart Phase 순서대로 실행
        foreach (var phase in phases)
            if (phase.triggerCondition == RoomData.TriggerCondition.RoomStart)
                yield return RunPhase(phase);

        // 대기 루프
        yield return WaitLoop(phases);
    }

    // ── 서브클래스 구현 필수 ──────────────────────────────

    protected abstract RoomData.PhaseData[] GetRoomPhases();
    protected abstract string GetRoomLabel();
    protected abstract void OnRoomComplete(NarrationBlock[] transitionNarration = null);

    // ── 대기 루프 ─────────────────────────────────────────

    private IEnumerator WaitLoop(RoomData.PhaseData[] phases)
    {
        string triggeredObjectID = null;
        void OnObjectInteracted(string objectID) => triggeredObjectID = objectID;
        RoomEventBus.OnObjectInteracted += OnObjectInteracted;

        try
        {
            while (true)
            {
                if (triggeredObjectID != null)
                {
                    string objectID = triggeredObjectID;
                    triggeredObjectID = null;

                    // objectID → Interact Phase 매칭
                    // triggerObjectID가 비어 있으면 모든 오브젝트에 반응
                    var phase = FindInteractPhase(phases, objectID);
                    if (phase != null)
                        yield return RunInteractPhase(phase);
                }
                yield return null;
            }
        }
        finally
        {
            RoomEventBus.OnObjectInteracted -= OnObjectInteracted;
        }
    }

    // ── Interact Phase 진입 ───────────────────────────────

    private IEnumerator RunInteractPhase(RoomData.PhaseData phase)
    {
        if (!phase.isRepeatable && completedPhases.Contains(phase.phaseID))
            yield break;

        ctx.PlayerController?.DisableMovement();

        if (!MeetsRequirements(phase))
        {
            if (phase.requirementFailNarration?.Length > 0)
                yield return ctx.Narrator.ShowBlocks(phase.requirementFailNarration);
            if (!roomIsLeaving)
                ctx.PlayerController?.EnableMovement();
            yield break;
        }

        yield return RunPhase(phase);

        if (!roomIsLeaving)
            ctx.PlayerController?.EnableMovement();
    }

    // ── Phase 핵심 흐름 ───────────────────────────────────

    protected IEnumerator RunPhase(RoomData.PhaseData phase)
    {
        if (phase.animator != null)
            yield return phase.animator.OnPhaseEnter();

        if (phase.onEnter?.Length > 0)
            yield return ctx.Narrator.ShowBlocks(phase.onEnter);

        RoomData.OutcomeData outcome = null;
        if (phase.exitCondition == RoomData.ExitCondition.Check)
            yield return RunCheck(phase, result => outcome = result);
        else
            outcome = phase.outcome;

        if (phase.animator != null)
            yield return phase.animator.OnPhaseExit();

        if (!string.IsNullOrEmpty(phase.phaseID))
            completedPhases.Add(phase.phaseID);

        if (outcome?.narration?.Length > 0)
            yield return ctx.Narrator.ShowBlocks(outcome.narration);

        ctx.Narrator.ClearAll();

        if (outcome != null)
            yield return HandleOutcome(outcome);
    }

    // ── 판정 ─────────────────────────────────────────────

    private IEnumerator RunCheck(RoomData.PhaseData phase, System.Action<RoomData.OutcomeData> onResult)
    {
        var cd = phase.checkData;

        ctx.Narrator.Clear(NarratorChannel.Paper);

        yield return ctx.Narrator.ShowBlocks(new[]
        {
            new NarrationBlock(BuildCheckAnnouncement(cd), NarratorChannel.World)
        });

        if (CheckAnimator != null)
            yield return CheckAnimator.OnBeforeCheck();
        else if (phase.animator != null)
            yield return phase.animator.OnBeforeCheck();

        bool success = cd.checkType == CheckSystem.CheckType.Compound
            ? CheckSystem.RollCompound(cd.stat, cd.threshold, cd.stat2, cd.threshold2)
            : CheckSystem.Roll(cd.stat, cd.checkType, cd.threshold);

        AudioManager.PlayCue(AudioCue.PaperCheckSfx);

        if (CheckAnimator != null)
            yield return CheckAnimator.OnAfterCheck(success);
        else if (phase.animator != null)
            yield return phase.animator.OnAfterCheck(success);

        ctx.Bridge.RecordCheck(
            cd.stat, success, phase.phaseID,
            success ? cd.summaryText_success : cd.summaryText_failure
        );

        if (success && !string.IsNullOrEmpty(phase.phaseID))
            succeededPhases.Add(phase.phaseID);

        onResult(success ? cd.onSuccess : cd.onFailure);
    }

    private string BuildCheckAnnouncement(RoomData.CheckData checkData)
    {
        if (checkData.checkType == CheckSystem.CheckType.Compound)
            return $"당신의 {GetStatLabel(checkData.stat)}과 {GetStatLabel(checkData.stat2)}가 시험받습니다...";

        return $"당신의 {GetStatLabel(checkData.stat)}이 시험받습니다...";
    }

    private string GetStatLabel(StatType stat) => stat.ToString();

    // ── 결과 분기 ─────────────────────────────────────────

    private IEnumerator HandleOutcome(RoomData.OutcomeData outcome)
    {
        switch (outcome.type)
        {
            case RoomData.OutcomeType.PhaseTo:
                var target = FindPhase(GetRoomPhases(), outcome.targetPhaseID);
                if (target != null)
                    yield return RunPhase(target);
                else
                    Debug.LogWarning($"[{GetRoomLabel()}] PhaseTo 대상 '{outcome.targetPhaseID}' 없음.");
                break;

            case RoomData.OutcomeType.ReturnToWait:
                break;

            case RoomData.OutcomeType.NextRoom:
                roomIsLeaving = true;
                OnRoomComplete(outcome.transitionNarration);
                break;

            case RoomData.OutcomeType.Death:
                roomIsLeaving = true;
                ctx.Bridge.OnDeath(outcome.endingID);
                break;

            case RoomData.OutcomeType.Escape:
                roomIsLeaving = true;
                ctx.Bridge.OnEscape(outcome.endingID);
                break;
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────

    private RoomData.PhaseData FindPhase(RoomData.PhaseData[] phases, string phaseID)
    {
        if (phases == null || string.IsNullOrEmpty(phaseID)) return null;
        foreach (var p in phases)
            if (p.phaseID == phaseID) return p;
        Debug.LogWarning($"[{GetRoomLabel()}] Phase '{phaseID}' 없음.");
        return null;
    }

    /// <summary>
    /// objectID로 실행 가능한 Interact Phase를 찾는다.
    /// 정확히 일치하는 phase를 먼저 찾고, triggerObjectID가 비어 있는 phase는 fallback으로만 사용한다.
    /// </summary>
    private RoomData.PhaseData FindInteractPhase(RoomData.PhaseData[] phases, string objectID)
    {
        if (phases == null) return null;

        RoomData.PhaseData exactRequirementFail = null;
        RoomData.PhaseData wildcardReady = null;
        RoomData.PhaseData wildcardRequirementFail = null;

        foreach (var p in phases)
        {
            if (p.triggerCondition != RoomData.TriggerCondition.Interact) continue;
            if (!p.isRepeatable && completedPhases.Contains(p.phaseID)) continue;

            bool isWildcard = string.IsNullOrEmpty(p.triggerObjectID);
            bool isExactMatch = p.triggerObjectID == objectID;
            if (!isWildcard && !isExactMatch) continue;

            bool meetsRequirements = MeetsRequirements(p);
            if (isExactMatch)
            {
                if (meetsRequirements) return p;
                exactRequirementFail ??= p;
                continue;
            }

            if (meetsRequirements)
                wildcardReady ??= p;
            else
                wildcardRequirementFail ??= p;
        }

        return exactRequirementFail ?? wildcardReady ?? wildcardRequirementFail;
    }

    private bool MeetsRequirements(RoomData.PhaseData phase)
    {
        if (phase.requiredPhaseIDs != null)
            foreach (var id in phase.requiredPhaseIDs)
                if (!completedPhases.Contains(id)) return false;

        if (phase.requiredSuccessPhaseIDs != null)
            foreach (var id in phase.requiredSuccessPhaseIDs)
                if (!succeededPhases.Contains(id)) return false;

        return true;
    }
}
