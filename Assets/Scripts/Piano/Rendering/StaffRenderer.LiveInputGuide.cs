using System.Collections.Generic;
using UnityEngine;

public partial class StaffRenderer : MonoBehaviour
{
    private static readonly Color LiveIndicatorBrown = new Color(0.45f, 0.26f, 0.12f, 1f);
    private readonly Dictionary<int, GameObject> liveInputIndicators = new Dictionary<int, GameObject>();
    private Material liveInputIndicatorMaterial;

    public void ShowLiveInputIndicator(int midiNote, Color color)
    {
        if (!liveInputIndicators.TryGetValue(midiNote, out GameObject indicator) || indicator == null)
        {
            indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = $"LiveInput_{midiNote}";
            indicator.transform.SetParent(transform, false);

            Collider collider = indicator.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = indicator.GetComponent<Renderer>();
            renderer.material = GetLiveInputIndicatorMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            liveInputIndicators[midiNote] = indicator;
        }

        float noteY = GetNoteYPosition(midiNote);
        CreateLedgerLinesForNote(noteY);
        UpdateHitLineHeight();

        float hitLineX = transform.InverseTransformPoint(GetHitPoint()).x;
        indicator.transform.localPosition = new Vector3(hitLineX, noteY, -0.03f);
        indicator.transform.localScale = Vector3.one * Mathf.Max(lineSpacing * 0.72f, 0.09f);
        indicator.SetActive(true);

        Renderer activeRenderer = indicator.GetComponent<Renderer>();
        if (activeRenderer != null)
        {
            activeRenderer.material.color = color;
        }
    }

    public void HideLiveInputIndicator(int midiNote)
    {
        if (liveInputIndicators.TryGetValue(midiNote, out GameObject indicator) && indicator != null)
        {
            indicator.SetActive(false);
        }
    }

    public void ClearLiveInputIndicators()
    {
        foreach (KeyValuePair<int, GameObject> pair in liveInputIndicators)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }
        }

        liveInputIndicators.Clear();
    }

    private Material GetLiveInputIndicatorMaterial()
    {
        if (liveInputIndicatorMaterial != null)
        {
            return liveInputIndicatorMaterial;
        }

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        liveInputIndicatorMaterial = new Material(shader);
        liveInputIndicatorMaterial.color = LiveIndicatorBrown;
        return liveInputIndicatorMaterial;
    }
}