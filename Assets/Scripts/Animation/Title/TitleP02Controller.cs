using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using MoreMountains.Feedbacks;

/// <summary>
/// P02 스탯 분배 페이즈 — 흐름 제어만 담당.
///
/// 흐름:
///   1. 나레이터 (구슬 등장 전)
///   2. 구슬 등장 (MarbleSpawner)
///   3. 나레이터 (공책 클릭 유도)
///   4. 공책 클릭 대기 (notebookButton)
///   5. 카메라 전환 (cameraTransition MMF)
///   6. StatAllocatorUI 활성화 → 타이핑 reveal
///
/// [Inspector 연결]
///   narrator        : NarratorRouter
///   titleData       : TitleData
///   marbleSpawner   : MarbleSpawner
///   notebookButton  : NoteBook 오브젝트 위에 붙은 Button
///   statAllocatorUI : StatAllocatorUI
///   cameraTransition : 공책 클릭 후 카메라 확대 MMF_Player
/// </summary>
public class TitleP02Controller : MonoBehaviour
{
    [Header("Narration")]
    [SerializeField] private NarratorRouter narrator;
    [SerializeField] private TitleData titleData;

    [Header("Interact")]
    [SerializeField] private MarbleSpawner marbleSpawner;
    [SerializeField] private Button notebookButton;

    [Header("UI")]
    [SerializeField] private StatAllocatorUI statAllocatorUI;

    [Header("Feel")]
    [SerializeField] private MMF_Player cameraTransition;

    public IEnumerator Run(Action onComplete)
    {
        // 1. 나레이터 (구슬 등장 전)
        if (narrator != null && titleData?.p02PreBlocks?.Length > 0)
            yield return narrator.ShowBlocks(titleData.p02PreBlocks);
        narrator.Clear(NarratorChannel.World);

        // 2. 구슬 등장
        if (marbleSpawner != null)
            yield return StartCoroutine(marbleSpawner.SpawnAll());

        // 3. 나레이터 (공책 클릭 유도)
        if (narrator != null && titleData?.p02PostBlocks?.Length > 0)
            yield return narrator.ShowBlocks(titleData.p02PostBlocks);
        narrator.Clear(NarratorChannel.World);

        // 4. 공책 클릭 대기
        if (notebookButton != null)
        {
            bool clicked = false;
            notebookButton.onClick.AddListener(() => clicked = true);

            yield return new WaitUntil(() => clicked);

            notebookButton.onClick.RemoveAllListeners();

            // 5. 카메라 전환
            if (cameraTransition != null)
            {
                cameraTransition.PlayFeedbacks();
                yield return new WaitForSeconds(cameraTransition.TotalDuration + 0.1f);
            }
        }

        // 5-1. 나레이터 (공책 클릭 후)
        if (narrator != null && titleData?.p02NotebookBlocks?.Length > 0)
            yield return narrator.ShowBlocks(titleData.p02NotebookBlocks);


        // 6. 스탯 분배 UI
        if (statAllocatorUI != null)
            yield return StartCoroutine(statAllocatorUI.Activate());

        narrator.ClearAllIncludingPaper();
        onComplete?.Invoke();
    }
}
