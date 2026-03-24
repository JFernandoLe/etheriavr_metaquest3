using UnityEngine;
using System.Collections;

/// <summary>
/// Extensión parcial de StaffRenderer para manejar feedback visual de la línea de hit
/// </summary>
public partial class StaffRenderer : MonoBehaviour
{
    /// <summary>
    /// Cambia el color de la línea de hit a verde (acierto perfecto)
    /// </summary>
    public void SetHitLinePerfect()
    {
        if (hitLine == null) return;
        
        Renderer renderer = hitLine.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0, 1, 0, 1); // Verde brillante
            StartCoroutine(ResetHitLineColor(0.3f));
        }
    }
    
    /// <summary>
    /// Cambia el color de la línea de hit a verde claro (acierto bueno)
    /// </summary>
    public void SetHitLineGood()
    {
        if (hitLine == null) return;
        
        Renderer renderer = hitLine.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.5f, 1, 0.5f, 1); // Verde claro
            StartCoroutine(ResetHitLineColor(0.25f));
        }
    }
    
    /// <summary>
    /// Cambia el color de la línea de hit a rojo (error)
    /// </summary>
    public void SetHitLineError()
    {
        if (hitLine == null) return;
        
        Renderer renderer = hitLine.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.red;
            StartCoroutine(ResetHitLineColor(0.2f));
        }
    }
    
    /// <summary>
    /// Resetea la línea de hit a su color original (amarillo)
    /// </summary>
    private IEnumerator ResetHitLineColor(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (hitLine == null) yield break;
        
        Renderer renderer = hitLine.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.yellow;
        }
    }
    
    /// <summary>
    /// Acceso público para cambiar el color genéricamente
    /// </summary>
    public void SetHitLineColor(Color color)
    {
        if (hitLine == null) return;
        
        Renderer renderer = hitLine.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }
    
    /// <summary>
    /// Pulso de feedback: cambio rápido de color con reset
    /// </summary>
    public void PulseHitLine(Color pulseColor, float duration = 0.2f)
    {
        if (hitLine == null) return;
        
        Renderer renderer = hitLine.GetComponent<Renderer>();
        if (renderer != null)
        {
            StartCoroutine(PulseHitLineCoroutine(pulseColor, duration));
        }
    }
    
    private IEnumerator PulseHitLineCoroutine(Color pulseColor, float duration)
    {
        if (hitLine == null) yield break;
        
        Renderer renderer = hitLine.GetComponent<Renderer>();
        renderer.material.color = pulseColor;
        yield return new WaitForSeconds(duration);
        renderer.material.color = Color.yellow;
    }
}
