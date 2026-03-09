using UnityEngine;

/// <summary>
/// Dibuja la línea vertical de "hit" donde las notas deben ser tocadas
/// Similar a la línea amarilla de Guitar Hero
/// </summary>
public class HitLineRenderer : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float lineHeight = 0.8f; // Altura de la línea
    [SerializeField] private float lineThickness = 0.02f; // Grosor de la línea
    [SerializeField] private Color lineColor = Color.yellow;
    
    [Header("Efecto de Pulsación")]
    [SerializeField] private bool enablePulse = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;
    
    private GameObject lineObject;
    private Renderer lineRenderer;
    private Color originalColor;
    private float pulseTimer = 0f;

    void Start()
    {
        CreateHitLine();
    }

    /// <summary>
    /// Crea la línea visual de hit
    /// </summary>
    private void CreateHitLine()
    {
        lineObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lineObject.name = "HitLine";
        lineObject.transform.parent = transform;
        lineObject.transform.localPosition = Vector3.zero;
        
        // Forma de línea vertical: delgada, alta, plana
        lineObject.transform.localScale = new Vector3(lineThickness, lineHeight, 0.001f);
        
        // Configurar material
        lineRenderer = lineObject.GetComponent<Renderer>();
        lineRenderer.material = new Material(Shader.Find("Standard"));
        lineRenderer.material.color = lineColor;
        
        // Hacer que brille
        lineRenderer.material.EnableKeyword("_EMISSION");
        lineRenderer.material.SetColor("_EmissionColor", lineColor * 0.5f);
        
        originalColor = lineColor;
        
        // Quitar collider (no lo necesitamos para visual)
        Destroy(lineObject.GetComponent<Collider>());
        
        Debug.Log("[HitLine] Línea de hit creada");
    }

    void Update()
    {
        if (!enablePulse || lineRenderer == null) return;
        
        // Efecto de pulsación
        pulseTimer += Time.deltaTime * pulseSpeed;
        float pulse = Mathf.Sin(pulseTimer) * pulseIntensity;
        
        Color currentColor = originalColor * (1f + pulse);
        lineRenderer.material.color = currentColor;
        lineRenderer.material.SetColor("_EmissionColor", currentColor * 0.8f);
    }

    /// <summary>
    /// Activa un efecto visual cuando se toca una nota correctamente
    /// </summary>
    public void TriggerHitEffect()
    {
        if (lineRenderer != null)
        {
            // Flash verde temporal
            StartCoroutine(HitFlash());
        }
    }

    private System.Collections.IEnumerator HitFlash()
    {
        Color hitColor = Color.green;
        lineRenderer.material.color = hitColor;
        lineRenderer.material.SetColor("_EmissionColor", hitColor);
        
        yield return new WaitForSeconds(0.15f);
        
        lineRenderer.material.color = originalColor;
        lineRenderer.material.SetColor("_EmissionColor", originalColor * 0.5f);
    }
}
