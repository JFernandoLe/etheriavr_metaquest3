using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Guías de entrada en vivo: esferas sobre la línea de hit que muestran
/// qué teclas está pulsando el jugador y a qué altura del pentagrama caen.
/// </summary>
public partial class StaffRenderer
{
    private static readonly Color LiveIndicatorBrown = new Color(0.45f, 0.26f, 0.12f, 1f);

    private readonly Dictionary<int, GameObject> liveInputIndicators = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, Renderer> liveInputRenderers = new Dictionary<int, Renderer>();
    private Material liveInputIndicatorMaterial;
    private MaterialPropertyBlock liveInputPropertyBlock;

    public void ShowLiveInputIndicator(int midiNote, Color color)
    {
        if (!liveInputIndicators.TryGetValue(midiNote, out GameObject indicator) || indicator == null)
        {
            indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = $"LiveInput_{midiNote}";
            indicator.transform.SetParent(transform, false);

            Collider indicatorCollider = indicator.GetComponent<Collider>();
            if (indicatorCollider != null) Destroy(indicatorCollider);

            Renderer newRenderer = indicator.GetComponent<Renderer>();
            newRenderer.sharedMaterial = GetLiveInputIndicatorMaterial();
            newRenderer.shadowCastingMode = ShadowCastingMode.Off;
            newRenderer.receiveShadows = false;

            liveInputIndicators[midiNote] = indicator;
            liveInputRenderers[midiNote] = newRenderer;
        }

        float noteY = GetNoteYPosition(midiNote);
        if (noteY < 0f || noteY > PentagramHeight)
        {
            CreateLedgerLinesForNote(noteY);
            UpdateHitLineHeight();
        }

        float hitLineX = transform.InverseTransformPoint(GetHitPoint()).x;
        indicator.transform.localPosition = new Vector3(hitLineX, noteY, -0.03f);
        indicator.transform.localScale = Vector3.one * Mathf.Max(lineSpacing * 0.72f, 0.09f);
        indicator.SetActive(true);

        if (liveInputRenderers.TryGetValue(midiNote, out Renderer activeRenderer) && activeRenderer != null)
        {
            liveInputPropertyBlock ??= new MaterialPropertyBlock();
            activeRenderer.GetPropertyBlock(liveInputPropertyBlock);
            liveInputPropertyBlock.SetColor(ColorId, color);
            liveInputPropertyBlock.SetColor(BaseColorId, color);
            activeRenderer.SetPropertyBlock(liveInputPropertyBlock);
        }
    }

    public void HideLiveInputIndicator(int midiNote)
    {
        if (liveInputIndicators.TryGetValue(midiNote, out GameObject indicator) && indicator != null)
            indicator.SetActive(false);
    }

    public void ClearLiveInputIndicators()
    {
        foreach (KeyValuePair<int, GameObject> pair in liveInputIndicators)
        {
            if (pair.Value != null) Destroy(pair.Value);
        }

        liveInputIndicators.Clear();
        liveInputRenderers.Clear();
    }

    private Material GetLiveInputIndicatorMaterial()
    {
        if (liveInputIndicatorMaterial != null) return liveInputIndicatorMaterial;

        liveInputIndicatorMaterial = new Material(ResolveLineShader()) { color = LiveIndicatorBrown };
        return liveInputIndicatorMaterial;
    }
}
