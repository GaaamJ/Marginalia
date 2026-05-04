using System.Collections;
using UnityEngine;

/// <summary>
/// CheckMarkGraphic을 구동해 판정 연출(O|X 드로잉 → 점 떨림 → 선 이동)을 수행한다.
/// RoomSceneController에서 BaseRoomRunner.CheckAnimator에 주입한다.
/// </summary>
public class CheckPhaseAnimator : MonoBehaviour
{
    [Header("References")]
    public CheckMarkGraphic checkMarkGraphic;

    [Header("Drawing Speed")]
    public float oDrawDuration   = 0.55f;
    public float divDrawDuration = 0.18f;
    public float xDrawDuration   = 0.45f;
    public float symbolGap       = 0.08f;

    [Header("Wait Shake")]
    public float waitShakeAmount   = 2.5f;
    public float waitShakeInterval = 0.07f;
    public float waitDuration      = 1.2f;

    [Header("Resistance")]
    public float resistDistance = 12f;
    public float resistDuration = 0.13f;

    [Header("Step Movement")]
    public float stepSize              = 9f;
    public float stepInterval          = 0.065f;
    public float stepIntervalVariance  = 0.025f;

    [Header("Wobble")]
    public float noiseAmount    = 5f;
    public float noiseFrequency = 2.8f;

    [Header("Overshoot")]
    public float overshootAmount   = 14f;
    public float overshootDuration = 0.1f;
    public float arrivalPause      = 0.35f;

    [Header("Symbol Shake")]
    public float symbolShakeAmount      = 2f;
    public float symbolShakeMinInterval = 0.04f;
    public float symbolShakeMaxInterval = 0.10f;

    [Header("Result Color")]
    public float resultColorDuration = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioCue drawCue = AudioCue.PaperCheckSfx;
    [SerializeField] private float drawCueInterval = 0.08f;

    private Coroutine _symbolShakeCoroutine;
    private Coroutine _drawCueCoroutine;

    // ── Public Methods ─────────────────────────────────────

    /// <summary>O|X 드로잉 후 점 떨림 대기. CheckSystem.Roll 직전에 호출.</summary>
    public IEnumerator OnBeforeCheck()
    {
        if (checkMarkGraphic == null) yield break;
        checkMarkGraphic.ResetAll();

        StartSymbolShake();

        StartDrawCueLoop();
        yield return Animate(t => checkMarkGraphic.SetOProgress(t),        oDrawDuration);
        yield return new WaitForSeconds(symbolGap);
        yield return Animate(t => checkMarkGraphic.SetDividerProgress(t),  divDrawDuration);
        yield return new WaitForSeconds(symbolGap);
        yield return Animate(t => checkMarkGraphic.SetXProgress(t),        xDrawDuration);
        StopDrawCueLoop();
        yield return new WaitForSeconds(symbolGap);
        yield return WaitShake();
    }

    /// <summary>판정 결과에 따라 점을 O 또는 X 중심으로 끌어당기는 연출. Roll 직후에 호출.</summary>
    public IEnumerator OnAfterCheck(bool success)
    {
        if (checkMarkGraphic == null) yield break;
        Vector2 start  = checkMarkGraphic.DividerCenter;
        Vector2 target = success ? checkMarkGraphic.OCenter : checkMarkGraphic.XCenter;
        yield return MoveJudgeLine(start, target);
        yield return LerpResultColor(success);
    }

    private IEnumerator LerpResultColor(bool success)
    {
        Color from    = checkMarkGraphic.InkColor;
        Color to      = success ? checkMarkGraphic.OSuccessColor : checkMarkGraphic.XFailureColor;
        float elapsed = 0f;
        while (elapsed < resultColorDuration)
        {
            elapsed += Time.deltaTime;
            Color c = Color.Lerp(from, to, Mathf.Clamp01(elapsed / resultColorDuration));
            if (success) checkMarkGraphic.SetOColor(c);
            else         checkMarkGraphic.SetXColor(c);
            checkMarkGraphic.SetLineColor(c);
            yield return null;
        }
        if (success) checkMarkGraphic.SetOColor(to);
        else         checkMarkGraphic.SetXColor(to);
        checkMarkGraphic.SetLineColor(to);
    }

    /// <summary>다음 나레이션 직전에 호출해 그래픽을 초기화.</summary>
    public void ResetGraphic()
    {
        StopSymbolShake();
        if (checkMarkGraphic != null)
            checkMarkGraphic.ResetAll();
    }

    // ── Draw Animation ─────────────────────────────────────

    private IEnumerator Animate(System.Action<float> setter, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            setter(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        setter(1f);
    }

    // ── Symbol Shake ───────────────────────────────────────

    private void StartSymbolShake()
    {
        if (_symbolShakeCoroutine != null) StopCoroutine(_symbolShakeCoroutine);
        _symbolShakeCoroutine = StartCoroutine(SymbolShakeLoop());
    }

    public void StopSymbolShake()
    {
        if (_symbolShakeCoroutine != null) { StopCoroutine(_symbolShakeCoroutine); _symbolShakeCoroutine = null; }
        StopDrawCueLoop();
        if (checkMarkGraphic != null)
        {
            checkMarkGraphic.SetSymbolShakes(Vector2.zero, Vector2.zero, Vector2.zero);
            checkMarkGraphic.SetJudgeLineShake(Vector2.zero);
        }
    }

    private IEnumerator SymbolShakeLoop()
    {
        float oTimer = 0f, divTimer = 0f, xTimer = 0f, lineTimer = 0f;
        float oNext  = 0f, divNext  = 0f, xNext  = 0f, lineNext  = 0f;
        Vector2 oOff = Vector2.zero, divOff = Vector2.zero, xOff = Vector2.zero, lineOff = Vector2.zero;

        while (true)
        {
            float dt = Time.deltaTime;
            oTimer += dt; divTimer += dt; xTimer += dt; lineTimer += dt;

            bool dirty = false;
            if (oTimer >= oNext)
            {
                oTimer = 0f;
                oNext  = Random.Range(symbolShakeMinInterval, symbolShakeMaxInterval);
                oOff   = (Vector2)Random.insideUnitCircle * symbolShakeAmount;
                dirty  = true;
            }
            if (divTimer >= divNext)
            {
                divTimer = 0f;
                divNext  = Random.Range(symbolShakeMinInterval, symbolShakeMaxInterval);
                divOff   = (Vector2)Random.insideUnitCircle * symbolShakeAmount;
                dirty    = true;
            }
            if (xTimer >= xNext)
            {
                xTimer = 0f;
                xNext  = Random.Range(symbolShakeMinInterval, symbolShakeMaxInterval);
                xOff   = (Vector2)Random.insideUnitCircle * symbolShakeAmount;
                dirty  = true;
            }
            if (lineTimer >= lineNext)
            {
                lineTimer = 0f;
                lineNext  = Random.Range(symbolShakeMinInterval, symbolShakeMaxInterval);
                lineOff   = (Vector2)Random.insideUnitCircle * symbolShakeAmount;
                dirty     = true;
            }
            if (dirty)
            {
                checkMarkGraphic.SetSymbolShakes(oOff, divOff, xOff);
                checkMarkGraphic.SetJudgeLineShake(lineOff);
            }
            yield return null;
        }
    }

    // ── Wait Shake ─────────────────────────────────────────

    private void StartDrawCueLoop()
    {
        StopDrawCueLoop();
        if (drawCue == AudioCue.None) return;
        _drawCueCoroutine = StartCoroutine(DrawCueLoop());
    }

    private void StopDrawCueLoop()
    {
        if (_drawCueCoroutine == null) return;
        StopCoroutine(_drawCueCoroutine);
        _drawCueCoroutine = null;
    }

    private IEnumerator DrawCueLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.01f, drawCueInterval));
        while (true)
        {
            AudioManager.PlayCue(drawCue);
            yield return wait;
        }
    }

    private IEnumerator WaitShake()
    {
        checkMarkGraphic.ResetJudgeOnly();
        Vector2 divCenter  = checkMarkGraphic.DividerCenter;
        checkMarkGraphic.AddJudgePoint(divCenter);

        float elapsed    = 0f;
        float shakeTimer = waitShakeInterval;

        while (elapsed < waitDuration)
        {
            elapsed    += Time.deltaTime;
            shakeTimer += Time.deltaTime;

            if (shakeTimer >= waitShakeInterval)
            {
                shakeTimer = 0f;
                checkMarkGraphic.UpdateLastJudgePoint(divCenter + (Vector2)Random.insideUnitCircle * waitShakeAmount);
            }
            yield return null;
        }
        checkMarkGraphic.UpdateLastJudgePoint(divCenter);
    }

    // ── Judge Line Movement ────────────────────────────────

    private IEnumerator MoveJudgeLine(Vector2 start, Vector2 target)
    {
        checkMarkGraphic.ResetJudgeOnly();
        checkMarkGraphic.AddJudgePoint(start); // 앵커
        checkMarkGraphic.AddJudgePoint(start); // 헤드

        Vector2 dir = (target - start).magnitude > 0.001f
            ? (target - start).normalized
            : Vector2.right;

        // 저항: 출발 직후 반대 방향으로 밀림
        Vector2 resistEnd = start - dir * resistDistance;
        StartDrawCueLoop();
        yield return LerpHead(start, resistEnd, resistDuration);

        Vector2 current = resistEnd;

        // 스텝 이동 — 뚝뚝 끊기며 구불거림
        for (int i = 0; i < 200; i++)
        {
            Vector2 toTarget = target - current;
            float   dist     = toTarget.magnitude;
            if (dist < stepSize * 0.5f) break;

            float   noise = Mathf.PerlinNoise(i * noiseFrequency * 0.1f, 0.5f) * 2f - 1f;
            Vector2 perp  = new Vector2(-toTarget.normalized.y, toTarget.normalized.x);
            Vector2 next  = current
                + toTarget.normalized * Mathf.Min(stepSize, dist)
                + perp * noise * noiseAmount;

            checkMarkGraphic.AddJudgePoint(next); // 앵커 확정 + 새 헤드
            current = next;

            float interval = stepInterval + Random.Range(-stepIntervalVariance, stepIntervalVariance);
            yield return new WaitForSeconds(Mathf.Max(0.016f, interval));
        }

        // 최종 목표 지점으로 부드럽게 수렴
        yield return LerpHead(current, target, resistDuration);

        // 오버슈트 후 정착
        Vector2 overshootPos = target + dir * overshootAmount;
        yield return LerpHead(target,       overshootPos, overshootDuration);
        yield return LerpHead(overshootPos, target,       overshootDuration * 0.65f);

        StopDrawCueLoop();

        yield return new WaitForSeconds(arrivalPause);
    }

    private IEnumerator LerpHead(Vector2 from, Vector2 to, float duration)
    {
        if (duration <= 0f)
        {
            checkMarkGraphic.UpdateLastJudgePoint(to);
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            checkMarkGraphic.UpdateLastJudgePoint(
                Vector2.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        checkMarkGraphic.UpdateLastJudgePoint(to);
    }
}
