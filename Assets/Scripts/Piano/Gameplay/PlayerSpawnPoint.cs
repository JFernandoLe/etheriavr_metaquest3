using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Posiciona el jugador (XR Origin) en el punto de spawn correcto al iniciar la escena
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("Posición de Spawn")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(0.46f, -1.5f, -14f);
    [SerializeField] private float spawnRotationY = 180f; // Rotación en eje Y
    
    void Start()
    {
        PositionPlayer();
    }
    
    /// <summary>
    /// Posiciona el XR Origin en la posición de spawn
    /// </summary>
    private void PositionPlayer()
    {
        // Buscar el XR Origin en la escena
        GameObject xrOrigin = GameObject.Find("XR Origin") ?? 
                              GameObject.Find("XR Rig") ?? 
                              GameObject.Find("OVRCameraRig");
        
        if (xrOrigin == null)
        {
            // Buscar por nombre alternativo
            xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        }
        
        if (xrOrigin != null)
        {
            // Aplicar posición
            xrOrigin.transform.position = spawnPosition;
            
            // Aplicar rotación (solo en eje Y)
            xrOrigin.transform.rotation = Quaternion.Euler(0, spawnRotationY, 0);
            
            Debug.Log($"[PlayerSpawn] Jugador posicionado en {spawnPosition} con rotación Y={spawnRotationY}°");
        }
        else
        {
            Debug.LogWarning("[PlayerSpawn] No se encontró XR Origin en la escena. El jugador podría aparecer en la posición por defecto.");
        }
    }
}
