using UnityEngine;
using System.Collections;

/// <summary>
/// Feedback visual de la línea de hit: destellos de acierto/error y pulsos de color.
/// </summary>
public partial class StaffRenderer
{
    private static readonly Color PerfectGreen = new Color(0, 1, 0, 1);
    private static readonly Color GoodGreen = new Color(0.5f, 1, 0.5f, 1);
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer cachedHitLineRenderer;
    private MaterialPropertyBlock hitLinePropertyBlock;
    private static readonly WaitForSeconds Wait02 = new WaitForSeconds(0.2f);
    private static readonly WaitForSeconds Wait025 = new WaitForSeconds(0.25f);
    private static readonly WaitForSeconds Wait03 = new WaitForSeconds(0.3f);

    private Renderer HitLineRenderer
    {
        get
        {
            if (cachedHitLineRenderer == null && hitLine != null)
                cachedHitLineRenderer = hitLine.GetComponent<Renderer>();
            return cachedHitLineRenderer;
        }
    }

    public void SetHitLinePerfect() => FlashHitLine(PerfectGreen, 0.3f);

    public void SetHitLineGood() => FlashHitLine(GoodGreen, 0.25f);

    public void SetHitLineError() => FlashHitLine(Color.red, 0.2f);

    private void FlashHitLine(Color color, float resetDelay)
    {
        Renderer hitLineRenderer = HitLineRenderer;
        if (hitLineRenderer == null) return;

        SetHitLineColorInternal(hitLineRenderer, color);
        StartCoroutine(ResetHitLineColor(resetDelay));
    }

    private IEnumerator ResetHitLineColor(float delay)
    {
        yield return GetWait(delay);

        Renderer hitLineRenderer = HitLineRenderer;
        if (hitLineRenderer != null) SetHitLineColorInternal(hitLineRenderer, Color.yellow);
    }

    public void SetHitLineColor(Color color)
    {
        Renderer hitLineRenderer = HitLineRenderer;
        if (hitLineRenderer != null) SetHitLineColorInternal(hitLineRenderer, color);
    }

    public void PulseHitLine(Color pulseColor, float duration = 0.2f)
    {
        if (HitLineRenderer != null) StartCoroutine(PulseHitLineCoroutine(pulseColor, duration));
    }

    private IEnumerator PulseHitLineCoroutine(Color pulseColor, float duration)
    {
        Renderer hitLineRenderer = HitLineRenderer;
        if (hitLineRenderer == null) yield break;

        SetHitLineColorInternal(hitLineRenderer, pulseColor);
        yield return GetWait(duration);
        SetHitLineColorInternal(hitLineRenderer, Color.yellow);
    }

    private void SetHitLineColorInternal(Renderer rend, Color color)
    {
        hitLinePropertyBlock ??= new MaterialPropertyBlock();
        rend.GetPropertyBlock(hitLinePropertyBlock);
        hitLinePropertyBlock.SetColor(ColorId, color);
        hitLinePropertyBlock.SetColor(BaseColorId, color);
        rend.SetPropertyBlock(hitLinePropertyBlock);
    }

    private static WaitForSeconds GetWait(float delay)
    {
        if (Mathf.Approximately(delay, 0.2f)) return Wait02;
        if (Mathf.Approximately(delay, 0.25f)) return Wait025;
        if (Mathf.Approximately(delay, 0.3f)) return Wait03;
        return new WaitForSeconds(delay);
    }
}
