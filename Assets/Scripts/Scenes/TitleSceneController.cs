using UnityEngine;
using System.Collections;

/// <summary>
/// TitleScene 흐름 관리자.
///
/// P00 → P01 → P02 → P03
///
/// [Inspector 연결]
///   p00 : TitleP00Controller
///   p01 : TitleP01Controller
///   p02 : TitleP02Controller
///   p03 : TitleP03Controller
/// </summary>
public class TitleSceneController : MonoBehaviour
{
    public enum Phase { P00, P01, P02, P03 }

    [Header("Phase Controllers")]
    [SerializeField] private TitleP00Controller p00;
    [SerializeField] private TitleP01Controller p01;
    [SerializeField] private TitleP02Controller p02;
    [SerializeField] private TitleP03Controller p03;



    public Phase CurrentPhase { get; private set; } = Phase.P00;

    private void Start()
    {
        AudioManager.PlayCue(AudioCue.TitleBgm);
    }

    public void OnP00Complete()
    {
        if (CurrentPhase != Phase.P00) return;
        AudioManager.StopMusicCue();
        AudioManager.PlayCue(AudioCue.AmbientBgm);
        CurrentPhase = Phase.P01;
        StartCoroutine(p01.Run(OnP01Complete));
    }

    public void OnP01Complete()
    {
        if (CurrentPhase != Phase.P01) return;
        CurrentPhase = Phase.P02;
        StartCoroutine(p02.Run(OnP02Complete));
    }

    public void OnP02Complete()
    {
        if (CurrentPhase != Phase.P02) return;
        CurrentPhase = Phase.P03;
        StartCoroutine(p03.Run());
    }
}
