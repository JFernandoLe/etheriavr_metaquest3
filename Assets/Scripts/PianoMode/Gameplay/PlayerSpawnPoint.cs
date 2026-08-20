using UnityEngine;

/// <summary>
/// Posiciona el jugador (XR Origin) en el punto de spawn correcto al iniciar la escena.
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("Posición de Spawn")]
    [SerializeField] private bool repositionPlayerOnStart = false;
    [SerializeField] private bool useTransformAsSpawnPoint = true;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0.46f, -1.5f, -14f);
    [SerializeField] private float spawnRotationY = 180f;

    void Start() => PositionPlayer();

    private void PositionPlayer()
    {
        if (!repositionPlayerOnStart) return;

        GameObject xrOrigin = GameObject.Find("XR Origin")
                              ?? GameObject.Find("XR Rig")
                              ?? GameObject.Find("OVRCameraRig")
                              ?? GameObject.Find("XR Origin (XR Rig)");

        if (xrOrigin == null)
        {
            Debug.LogWarning("[PlayerSpawn] No se encontró XR Origin en la escena. El jugador podría aparecer en la posición por defecto.");
            return;
        }

        xrOrigin.transform.position = useTransformAsSpawnPoint ? transform.position : spawnPosition;
        xrOrigin.transform.rotation = Quaternion.Euler(
            0, useTransformAsSpawnPoint ? transform.eulerAngles.y : spawnRotationY, 0);
    }
}
