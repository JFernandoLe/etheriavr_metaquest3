using UnityEngine;
using System.Collections;

/// <summary>
/// Feedback visual de la línea de hit: destellos de acierto/error y pulsos de color.
/// </summary>
public partial class StaffRenderer
{
    private static readonly Color PerfectGreen = new Color(0, 1, 0, 1);
    private static readonly Color GoodGreen = new Color(0.5f, 1, 0.5f, 1);

    private Renderer HitLineRenderer => hitLine != null ? hitLine.GetComponent<Renderer>() : null;

    public void SetHitLinePerfect() => FlashHitLine(PerfectGreen, 0.3f);

    public void SetHitLineGood() => FlashHitLine(GoodGreen, 0.25f);

    public void SetHitLineError() => FlashHitLine(Color.red, 0.2f);

    /// <summary>Tiñe la línea de hit y programa su vuelta al amarillo.</summary>
    private void FlashHitLine(Color color, float resetDelay)
    {
        Renderer hitLineRenderer = HitLineRenderer;
        if (hitLineRenderer == null) return;

        hitLineRenderer.material.color = color;
        StartCoroutine(ResetHitLineColor(resetDelay));
    }

    private IEnumerator ResetHitLineColor(float delay)
    {
        yield return new WaitForSeconds(delay);

        Renderer hitLineRenderer = HitLineRenderer;
        if (hitLineRenderer != null) hitLineRenderer.material.color = Color.yellow;
    }

    public void SetHitLineColor(Color color)
    {
        Renderer hitLineRenderer = HitLineRenderer;
        if (hitLineRenderer != null) hitLineRenderer.material.color = color;
    }

    public void PulseHitLine(Color pulseColor, float duration = 0.2f)
    {
        if (HitLineRenderer != null) StartCoroutine(PulseHitLineCoroutine(pulseColor, duration));
    }

    private IEnumerator PulseHitLineCoroutine(Color pulseColor, float duration)
    {
        Renderer hitLineRenderer = HitLineRenderer;
        if (hitLineRenderer == null) yield break;

        hitLineRenderer.material.color = pulseColor;
        yield return new WaitForSeconds(duration);
        hitLineRenderer.material.color = Color.yellow;
    }
}
