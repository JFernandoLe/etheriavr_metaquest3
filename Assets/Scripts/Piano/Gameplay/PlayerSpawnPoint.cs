using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Posiciona el jugador (XR Origin) en el punto de spawn correcto al iniciar la escena
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("Posición de Spawn")]
    [SerializeField] private bool repositionPlayerOnStart = false;
    [SerializeField] private bool useTransformAsSpawnPoint = true;
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
        if (!repositionPlayerOnStart)
        {
            Debug.Log("[PlayerSpawn] Reubicación automática desactivada. Se conserva la posición actual del jugador.");
            return;
        }

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
            Vector3 targetPosition = useTransformAsSpawnPoint ? transform.position : spawnPosition;
            float targetRotationY = useTransformAsSpawnPoint ? transform.eulerAngles.y : spawnRotationY;

            // Aplicar posición
            xrOrigin.transform.position = targetPosition;
            
            // Aplicar rotación (solo en eje Y)
            xrOrigin.transform.rotation = Quaternion.Euler(0, targetRotationY, 0);
            
            Debug.Log($"[PlayerSpawn] Jugador posicionado en {targetPosition} con rotación Y={targetRotationY}°");
        }
        else
        {
            Debug.LogWarning("[PlayerSpawn] No se encontró XR Origin en la escena. El jugador podría aparecer en la posición por defecto.");
        }
    }
}
