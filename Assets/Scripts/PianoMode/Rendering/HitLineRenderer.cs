using UnityEngine;
using System.Collections;

/// <summary>
/// Dibuja la línea vertical de "hit" donde las notas deben ser tocadas,
/// al estilo de la línea amarilla de Guitar Hero.
/// </summary>
public class HitLineRenderer : MonoBehaviour
{
    private const string EmissionColor = "_EmissionColor";

    [Header("Configuración")]
    [SerializeField] private float lineHeight = 0.8f;
    [SerializeField] private float lineThickness = 0.02f;
    [SerializeField] private Color lineColor = Color.yellow;

    [Header("Efecto de Pulsación")]
    [SerializeField] private bool enablePulse = false;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;

    private GameObject lineObject;
    private Renderer lineRenderer;
    private Material lineMaterial;
    private Color originalColor;
    private float pulseTimer = 0f;

    void Start() => CreateHitLine();

    private void CreateHitLine()
    {
        lineObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lineObject.name = "HitLine";
        lineObject.transform.parent = transform;
        lineObject.transform.localPosition = Vector3.zero;
        lineObject.transform.localScale = new Vector3(lineThickness, lineHeight, 0.001f);

        lineRenderer = lineObject.GetComponent<Renderer>();
        lineMaterial = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard")) { color = lineColor };
        if (lineMaterial.HasProperty(EmissionColor))
        {
            lineMaterial.EnableKeyword("_EMISSION");
            lineMaterial.SetColor(EmissionColor, lineColor * 0.5f);
        }

        lineRenderer.sharedMaterial = lineMaterial;
        originalColor = lineColor;

        Destroy(lineObject.GetComponent<Collider>());
    }

    void Update()
    {
        if (!enablePulse || lineMaterial == null) return;

        pulseTimer += Time.deltaTime * pulseSpeed;
        Color currentColor = originalColor * (1f + Mathf.Sin(pulseTimer) * pulseIntensity);
        lineMaterial.color = currentColor;
        if (lineMaterial.HasProperty(EmissionColor))
            lineMaterial.SetColor(EmissionColor, currentColor * 0.8f);
    }

    public void TriggerHitEffect()
    {
        if (lineMaterial != null) StartCoroutine(HitFlash());
    }

    private IEnumerator HitFlash()
    {
        lineMaterial.color = Color.green;
        if (lineMaterial.HasProperty(EmissionColor))
            lineMaterial.SetColor(EmissionColor, Color.green);

        yield return new WaitForSeconds(0.15f);

        lineMaterial.color = originalColor;
        if (lineMaterial.HasProperty(EmissionColor))
            lineMaterial.SetColor(EmissionColor, originalColor * 0.5f);
    }

    void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
