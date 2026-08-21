using UnityEngine;

public class TuningVisualizer : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public VocalPitchAnalyzer receiver;
    public Transform indicator;

    public float maxCentsRange = 50f;
    public float movementRange = 1f;

    private Renderer indicatorRenderer;
    private MaterialPropertyBlock propertyBlock;
    private string lastState;
    private Color lastColor;

    void Awake()
    {
        if (receiver == null)
            receiver = FindObjectOfType<VocalPitchAnalyzer>();
        if (indicator != null) indicatorRenderer = indicator.GetComponent<Renderer>();
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (receiver == null || indicator == null) return;

        float cents = receiver.GetCurrentCents();
        string state = receiver.GetCurrentTuningState();

        float clamped = Mathf.Clamp(cents, -maxCentsRange, maxCentsRange);
        float normalized = clamped / maxCentsRange;
        float xPosition = normalized * movementRange;
        indicator.localPosition = new Vector3(xPosition, 0, 0);

        if (indicatorRenderer == null || state == lastState) return;

        Color color = state == "PERFECTO" ? Color.green : state == "CASI" ? Color.yellow : Color.red;
        if (color == lastColor && state == lastState) return;

        lastState = state;
        lastColor = color;
        propertyBlock ??= new MaterialPropertyBlock();
        indicatorRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(ColorId, color);
        propertyBlock.SetColor(BaseColorId, color);
        indicatorRenderer.SetPropertyBlock(propertyBlock);
    }
}
