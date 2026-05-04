using UnityEngine;
using System.Collections;
using MoreMountains.Feedbacks;

/// <summary>
/// P03 게임 시작 페이즈 — 흐름 제어만 담당.
///
/// 흐름:
///   1. 카메라 역방향 연출 (TODO — MMF)
///   2. 나레이션 (confirmBlocks)
///   3. 종이 플레이어에게 주어지는 연출 (TODO — MMF)
///   4. 암전 (TODO — MMF)
///   5. 씬 전환 (SceneTransitioner)
///
/// [Inspector 연결]
///   narrator    : NarratorRouter
///   titleData   : TitleData
///   nextScene   : 전환할 씬 이름
/// </summary>
public class TitleP03Controller : MonoBehaviour
{
    [Header("Narration")]
    [SerializeField] private NarratorRouter narrator;
    [SerializeField] private TitleData titleData;

    [Header("FEEL")]
    [SerializeField] private MMF_Player cameraReverse;
    [SerializeField] private MMF_Player paperOutFeel;
    [SerializeField] private MMF_Player fadeOutFeel;

    [Header("Scene Transition")]
    [SerializeField] private string nextScene;


    public IEnumerator Run()
    {
        // 1. 카메라 역방향 연출 (TODO)
        if (cameraReverse != null)
        {
            cameraReverse.PlayFeedbacks();
            yield return new WaitForSeconds(cameraReverse.TotalDuration);
        }

        // 2. 나레이션
        if (narrator != null && titleData?.p03_01Blocks?.Length > 0)
            yield return narrator.ShowBlocks(titleData.p03_01Blocks);

        narrator.ClearAll();

        // 3. 종이 연출
        if (paperOutFeel != null)
        {
            paperOutFeel.PlayFeedbacks();
            yield return new WaitForSeconds(paperOutFeel.TotalDuration);
        }

        // 3-1. 나레이션 (paper ui)
        if (narrator != null && titleData?.p03_02Blocks?.Length > 0)
            yield return narrator.ShowBlocks(titleData.p03_02Blocks);
        narrator.Clear(NarratorChannel.World);

        //4. 암전 (TODO)
        if (fadeOutFeel != null)
        {
            fadeOutFeel.PlayFeedbacks();
            yield return new WaitForSeconds(fadeOutFeel.TotalDuration + 1f);
        }

        // 5. 씬 전환
        if (!string.IsNullOrEmpty(nextScene))
        {
            SceneTransitioner.Instance.TransitionTo(nextScene, null, AudioCue.DoorOpenSfx);
        }
    }
}
