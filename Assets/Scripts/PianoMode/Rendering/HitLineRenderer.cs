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
    [SerializeField] private bool enablePulse = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;

    private GameObject lineObject;
    private Renderer lineRenderer;
    private Color originalColor;
    private float pulseTimer = 0f;

    void Start() => CreateHitLine();

    private void CreateHitLine()
    {
        lineObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lineObject.name = "HitLine";
        lineObject.transform.parent = transform;
        lineObject.transform.localPosition = Vector3.zero;
        // Línea vertical: delgada, alta y plana.
        lineObject.transform.localScale = new Vector3(lineThickness, lineHeight, 0.001f);

        lineRenderer = lineObject.GetComponent<Renderer>();
        lineRenderer.material = new Material(Shader.Find("Standard")) { color = lineColor };
        lineRenderer.material.EnableKeyword("_EMISSION");
        lineRenderer.material.SetColor(EmissionColor, lineColor * 0.5f);

        originalColor = lineColor;

        Destroy(lineObject.GetComponent<Collider>());
    }

    void Update()
    {
        if (!enablePulse || lineRenderer == null) return;

        pulseTimer += Time.deltaTime * pulseSpeed;
        Color currentColor = originalColor * (1f + Mathf.Sin(pulseTimer) * pulseIntensity);

        lineRenderer.material.color = currentColor;
        lineRenderer.material.SetColor(EmissionColor, currentColor * 0.8f);
    }

    /// <summary>Destello verde al acertar una nota.</summary>
    public void TriggerHitEffect()
    {
        if (lineRenderer != null) StartCoroutine(HitFlash());
    }

    private IEnumerator HitFlash()
    {
        lineRenderer.material.color = Color.green;
        lineRenderer.material.SetColor(EmissionColor, Color.green);

        yield return new WaitForSeconds(0.15f);

        lineRenderer.material.color = originalColor;
        lineRenderer.material.SetColor(EmissionColor, originalColor * 0.5f);
    }
}
