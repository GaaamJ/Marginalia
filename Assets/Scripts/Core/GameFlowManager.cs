using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 씬 전환 후에도 유지되는 게임 흐름 관리자.
/// 방을 roomSequence 순서대로 진행. 복도 씬 없음.
///
/// [Inspector 연결 목록]
///   - roomSequence       : 방 진행 순서 (RoomData 배열)
///   - layoutDataSequence : 방별 오브젝트 배치 데이터 (roomSequence와 동일 순서)
///   - encoreRoomData     : 영원 방 앙코르 루프용 SO
///   - roomSceneName      : "RoomScene"
///   - endingSceneName    : "EndingScene"
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("방 순서 (인덱스 순서대로 진행)")]
    [SerializeField] private RoomData[] roomSequence;

    [Header("방별 Layout 데이터 — roomSequence와 동일 순서로 등록")]
    // null 허용 — 해당 방에 RoomLayoutData 없으면 오브젝트 생성 스킵
    [SerializeField] private RoomLayoutData[] layoutDataSequence;

    [Header("영원 방 앙코르 루프")]
    [SerializeField] private EncoreRoomData encoreRoomData;

    // ── 공개 상태 ─────────────────────────────────────────
    public int CurrentRoomIndex { get; private set; } = 0;
    public bool IsGameOver { get; private set; } = false;
    public bool IsEscaped { get; private set; } = false;
    public string LastEndingID { get; private set; } = "";

    // ── 앙코르 상태 ───────────────────────────────────────
    public bool IsEncoreLoop { get; private set; } = false;
    public int EncoreCounter { get; private set; } = 0;

    /// <summary>현재 방 RoomData. 앙코르 루프 중이면 null.</summary>
    public RoomData CurrentRoomData =>
        (!IsEncoreLoop && CurrentRoomIndex >= 0 && CurrentRoomIndex < roomSequence.Length)
        ? roomSequence[CurrentRoomIndex]
        : null;

    /// <summary>
    /// 현재 방 RoomLayoutData.
    /// null이면 RoomSceneController에서 오브젝트 생성 스킵.
    /// </summary>
    public RoomLayoutData CurrentLayoutData =>
        (!IsEncoreLoop && layoutDataSequence != null &&
         CurrentRoomIndex >= 0 && CurrentRoomIndex < layoutDataSequence.Length)
        ? layoutDataSequence[CurrentRoomIndex]
        : null;

    /// <summary>앙코르 루프 데이터.</summary>
    public EncoreRoomData EncoreRoomData => encoreRoomData;

    // ── 판정 기록 ─────────────────────────────────────────
    public readonly List<CheckRecord> CheckHistory = new();

    // ── 씬 이름 ───────────────────────────────────────────
    [Header("Scene Names")]
    [SerializeField] private string roomSceneName = "RoomScene";
    [SerializeField] private string endingSceneName = "EndingScene";

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 흐름 제어 ─────────────────────────────────────────

    /// <summary>TitleScene 완료 후 첫 번째 방으로.</summary>
    public void StartGame()
    {
        CurrentRoomIndex = 0;
        LoadRoomScene();
    }

    /// <summary>방 클리어 → 다음 방으로. 마지막 방이면 앙코르 루프 진입.</summary>
    public void OnRoomClear_NextRoom(NarrationBlock[] transitionNarration = null)
    {
        CurrentRoomIndex++;

        if (CurrentRoomIndex >= roomSequence.Length)
            EnterEncoreLoop(transitionNarration);
        else
            LoadRoomScene(transitionNarration);
    }

    /// <summary>앙코르 루프 진입.</summary>
    public void EnterEncoreLoop(NarrationBlock[] transitionNarration = null)
    {
        IsEncoreLoop = true;
        EncoreCounter = 0;
        LoadRoomScene(transitionNarration);
    }

    /// <summary>앙코르 방 하나 클리어 → 카운터 올리고 다시 로드.</summary>
    public void OnEncoreClear(NarrationBlock[] transitionNarration = null)
    {
        EncoreCounter++;
        Debug.Log($"[GameFlow] OnEncoreClear → EncoreCounter: {EncoreCounter}");
        LoadRoomScene(transitionNarration);
    }

    /// <summary>게임 오버. endingID → EndingData 매칭.</summary>
    public void OnDeath(string endingID)
    {
        IsGameOver = true;
        IsEscaped = false;
        LastEndingID = endingID;

        SnapshotFinalStats();
        if (SceneTransitioner.Instance != null)
            SceneTransitioner.Instance.TransitionTo(endingSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(endingSceneName);
    }

    /// <summary>탈출 (클리어). endingID → EndingData 매칭.</summary>
    public void OnEscape(string endingID)
    {
        IsEscaped = true;
        IsGameOver = false;
        LastEndingID = endingID;

        SnapshotFinalStats();
        if (SceneTransitioner.Instance != null)
            SceneTransitioner.Instance.TransitionTo(endingSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(endingSceneName);
    }

    /// <summary>
    /// 방 전환 트랜지션.
    /// transitionNarration이 있으면 암전 중 Screen 채널로 출력.
    /// </summary>
    private void LoadRoomScene(NarrationBlock[] transitionNarration = null)
    {
        AudioManager.PlayCue(AudioCue.DoorOpenSfx);

        if (SceneTransitioner.Instance != null)
            SceneTransitioner.Instance.TransitionTo(roomSceneName, transitionNarration);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(roomSceneName);
    }

    // ── 판정 기록 ─────────────────────────────────────────

    public void RecordCheck(StatType stat, bool success, string context, string summaryText = "")
    {
        CheckHistory.Add(new CheckRecord
        {
            stat = stat,
            success = success,
            context = context,
            statValue = PlayerStats.Instance != null ? PlayerStats.Instance.Get(stat) : 0,
            summaryText = summaryText,
        });
    }

    public void ResetForNewGame()
    {
        CurrentRoomIndex = 0;
        IsGameOver = false;
        IsEscaped = false;
        IsEncoreLoop = false;
        EncoreCounter = 0;
        LastEndingID = "";
        FinalStats = null;
        CheckHistory.Clear();
    }

    // ── 스탯 스냅샷 ───────────────────────────────────────

    public FinalStatsSnapshot FinalStats { get; private set; }

    private void SnapshotFinalStats()
    {
        if (PlayerStats.Instance == null) return;
        var s = PlayerStats.Instance;
        FinalStats = new FinalStatsSnapshot
        {
            STR = s.STR,
            DEX = s.DEX,
            PER = s.PER,
            INT = s.INT,
            LUK = s.LUK,
            HUM = s.HUM,
        };
    }

    // ── 내부 데이터 구조 ──────────────────────────────────

    [System.Serializable]
    public class CheckRecord
    {
        public StatType stat;
        public int statValue;
        public bool success;
        public string context;
        public string summaryText;
    }

    [System.Serializable]
    public class FinalStatsSnapshot
    {
        public int STR, DEX, PER, INT, LUK, HUM;

        public override string ToString() =>
            $"STR  {STR}\nDEX  {DEX}\nPER  {PER}\nINT  {INT}\nLUK  {LUK}\nHUM  {HUM}";
    }

    // ── 테스트용 데이터 주입 API ──────────────────────────
#if UNITY_EDITOR
    public void InjectTestDeath(string endingID)
    {
        IsGameOver = true;
        IsEscaped = false;
        LastEndingID = endingID;
        SnapshotFinalStats();
    }

    public void InjectTestEscape(string endingID)
    {
        IsGameOver = false;
        IsEscaped = true;
        LastEndingID = endingID;
        SnapshotFinalStats();
    }

    public void JumpToRoom(int index)
    {
        IsEncoreLoop = false;
        CurrentRoomIndex = Mathf.Clamp(index, 0, roomSequence.Length - 1);
        LoadRoomScene();
    }

    public void JumpToEncore(int counter)
    {
        IsEncoreLoop = true;
        EncoreCounter = counter;
        LoadRoomScene();
    }
#endif
}
