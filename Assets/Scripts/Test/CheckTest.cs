using UnityEngine;
using System.Collections;

/// <summary>
/// CheckSystem 테스트용.
/// 확인 후 삭제하세요.
/// </summary>
public class CheckTest : MonoBehaviour
{
    [Header("판정 설정 (단일 Roll)")]
    [SerializeField] private StatType stat = StatType.STR;
    [SerializeField] private CheckSystem.CheckType checkType = CheckSystem.CheckType.Threshold;
    [SerializeField] private int threshold = 2;

    [Header("복합 판정 (CheckType = Compound 시 사용)")]
    [SerializeField] private StatType stat2 = StatType.DEX;
    [SerializeField] private int threshold2 = 2;

    [Header("스탯 강제 세팅 (Set And Roll)")]
    [SerializeField] private StatType overrideStat = StatType.STR;
    [SerializeField] private int overrideValue = 3;

    [Header("시퀀스 테스트용 레퍼런스")]
    [SerializeField] private CheckPhaseAnimator checkPhaseAnimator;
    [SerializeField] private NarratorRouter narratorRouter;

    // ── 단일 Roll ─────────────────────────────────────────

    [ContextMenu("Roll Check")]
    public void Roll() => StartCoroutine(RollRoutine());

    [ContextMenu("Set Stat and Roll")]
    public void SetAndRoll()
    {
        var stats = PlayerStats.Instance;
        if (stats == null) { Debug.LogWarning("[CheckTest] PlayerStats.Instance가 null."); return; }
        stats.SetStat(overrideStat, overrideValue);
        Debug.Log($"[CheckTest] {overrideStat} = {overrideValue} 로 세팅");
        Roll();
    }

    private IEnumerator RollRoutine()
    {
        if (checkPhaseAnimator != null)
            yield return checkPhaseAnimator.OnBeforeCheck();

        bool success;
        if (checkType == CheckSystem.CheckType.Compound)
            success = CheckSystem.RollCompoundDebug(stat, threshold, stat2, threshold2, out _);
        else
            success = CheckSystem.RollDebug(stat, checkType, threshold, out _);

        AudioManager.PlayCue(AudioCue.PaperCheckSfx);

        if (checkPhaseAnimator != null)
            yield return checkPhaseAnimator.OnAfterCheck(success);
    }

    // ── 시퀀스 테스트 ─────────────────────────────────────
    // Paper → Check → Paper → Check → Paper 교대 더미 데이터

    private static readonly (NarrationBlock[] paper, RoomData.CheckData check)[] TestSequence =
    {
        (
            paper: new[]
            {
                new NarrationBlock("당신 앞에 두 개의 문이 있습니다.\n통과하려면 근력이 필요합니다.", NarratorChannel.Paper) { appendMode = false },
            },
            check: new RoomData.CheckData
            {
                stat      = StatType.STR,
                checkType = CheckSystem.CheckType.Threshold,
                threshold = 2,
                onBeforeCheck = new[] { new NarrationBlock("근력 판정을 시작합니다.", NarratorChannel.World) },
                onSuccess = new RoomData.OutcomeData { narration = new[] { new NarrationBlock("문이 열렸습니다.", NarratorChannel.World) } },
                onFailure = new RoomData.OutcomeData { narration = new[] { new NarrationBlock("문은 꿈쩍도 하지 않았습니다.", NarratorChannel.World) } },
            }
        ),
        (
            paper: new[]
            {
                new NarrationBlock("어둠 속에서 무언가가 다가옵니다.\n눈치가 필요합니다.", NarratorChannel.Paper) { appendMode = false },
            },
            check: new RoomData.CheckData
            {
                stat      = StatType.PER,
                checkType = CheckSystem.CheckType.Probability,
                threshold = 0,
                onBeforeCheck = new[] { new NarrationBlock("지각 판정을 시작합니다.", NarratorChannel.World) },
                onSuccess = new RoomData.OutcomeData { narration = new[] { new NarrationBlock("낌새를 알아챘습니다.", NarratorChannel.World) } },
                onFailure = new RoomData.OutcomeData { narration = new[] { new NarrationBlock("아무것도 느끼지 못했습니다.", NarratorChannel.World) } },
            }
        ),
        (
            paper: new[]
            {
                new NarrationBlock("마지막 관문입니다.\n운이 따라야 합니다.", NarratorChannel.Paper) { appendMode = false },
            },
            check: new RoomData.CheckData
            {
                stat      = StatType.LUK,
                checkType = CheckSystem.CheckType.LuckFixed,
                threshold = 2,
                onBeforeCheck = new[] { new NarrationBlock("운 판정을 시작합니다.", NarratorChannel.World) },
                onSuccess = new RoomData.OutcomeData { narration = new[] { new NarrationBlock("행운이 함께했습니다.", NarratorChannel.World) } },
                onFailure = new RoomData.OutcomeData { narration = new[] { new NarrationBlock("운이 따르지 않았습니다.", NarratorChannel.World) } },
            }
        ),
    };

    [ContextMenu("Run Test Sequence")]
    public void RunTestSequence()
    {
        if (narratorRouter == null) { Debug.LogWarning("[CheckTest] NarratorRouter가 연결되지 않았습니다."); return; }
        if (checkPhaseAnimator == null) { Debug.LogWarning("[CheckTest] CheckPhaseAnimator가 연결되지 않았습니다."); return; }
        StartCoroutine(TestSequenceRoutine());
    }

    private IEnumerator TestSequenceRoutine()
    {
        foreach (var (paper, check) in TestSequence)
        {
            // 1. Check 그래픽 flush 후 Paper 나레이션
            checkPhaseAnimator.ResetGraphic();
            yield return narratorRouter.ShowBlocks(paper);

            // 2. Check 시작 전 Paper flush
            narratorRouter.Clear(NarratorChannel.Paper);

            // 3. onBeforeCheck 나레이션
            if (check.onBeforeCheck?.Length > 0)
                yield return narratorRouter.ShowBlocks(check.onBeforeCheck);

            // 4. 판정 연출
            yield return checkPhaseAnimator.OnBeforeCheck();

            // 5. Roll
            bool success = CheckSystem.RollDebug(check.stat, check.checkType, check.threshold, out _);

            AudioManager.PlayCue(AudioCue.PaperCheckSfx);

            // 6. 결과 연출
            yield return checkPhaseAnimator.OnAfterCheck(success);

            // 7. 결과 나레이션
            var outcome = success ? check.onSuccess : check.onFailure;
            if (outcome?.narration?.Length > 0)
                yield return narratorRouter.ShowBlocks(outcome.narration);

            narratorRouter.ClearAll();
        }

        // 종료 Paper
        yield return narratorRouter.ShowBlocks(new[]
        {
            new NarrationBlock("모든 판정이 끝났습니다.", NarratorChannel.Paper) { appendMode = false },
        });
    }
}
