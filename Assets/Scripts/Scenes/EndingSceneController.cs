using UnityEngine;
using UnityEngine.UI;

public class EndingSceneController : MonoBehaviour
{
    [Header("Stat Paper")]
    [SerializeField] private StatPaperUI statPaperUI;

    [Header("History")]
    [SerializeField] private HistoryPaper historyPaper;

    [Header("Paper Visual Branch")]
    [SerializeField] private GameObject clearPaperVisual;
    [SerializeField] private GameObject gameOverPaperVisual;

    [Header("UI")]
    [SerializeField] private Button restartButton;
    [SerializeField] private string titleSceneName = "TitleScene";

#if UNITY_EDITOR
    [Header("Test (Editor Only)")]
    [SerializeField] private bool useTestData = false;
    [SerializeField] private string testEndingId = "TEST_CLEAR";
    [SerializeField] private int testSTR = 2;
    [SerializeField] private int testDEX = 1;
    [SerializeField] private int testPER = 3;
    [SerializeField] private int testINT = 2;
    [SerializeField] private int testLUK = 1;
    [SerializeField] private int testHUM = 3;
#endif

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

#if UNITY_EDITOR
        if (useTestData)
        {
            statPaperUI?.Populate(testEndingId, testSTR, testDEX, testPER, testINT, testLUK, testHUM);
            historyPaper?.Populate(null);
            SetPaperVisual(false);
            SetupRestartButton();
            return;
        }
#endif
        var flow = GameFlowManager.Instance;
        var stats = PlayerStats.Instance;

        statPaperUI?.Populate(
            flow?.LastEndingID ?? "—",
            stats?.STR ?? 0,
            stats?.DEX ?? 0,
            stats?.PER ?? 0,
            stats?.INT ?? 0,
            stats?.LUK ?? 0,
            stats?.HUM ?? 0
        );

        historyPaper?.Populate(flow?.CheckHistory);
        SetPaperVisual(flow?.IsEscaped == true);
        SetupRestartButton();
    }

    private void SetPaperVisual(bool isEscaped)
    {
        if (clearPaperVisual != null)
            clearPaperVisual.SetActive(isEscaped);
        if (gameOverPaperVisual != null)
            gameOverPaperVisual.SetActive(!isEscaped);
    }

    private void SetupRestartButton()
    {
        if (restartButton == null) return;

        restartButton.gameObject.SetActive(true);
        restartButton.onClick.AddListener(OnRestart);
    }

    private void OnRestart()
    {
        GameFlowManager.Instance?.ResetForNewGame();
        UnityEngine.SceneManagement.SceneManager.LoadScene(titleSceneName);
    }
}
