using UnityEngine;

/// <summary>
/// Arranca la configuración de red y ajustes de rendimiento Quest al inicio.
/// </summary>
public class Initializer : MonoBehaviour
{
    private void Awake()
    {
        // Quest 3: 72 Hz nativo; vSync off evita esperar vsync de desktop en builds.
        Application.targetFrameRate = 72;
        QualitySettings.vSyncCount = 0;

        string baseUrl = NetworkConfig.Instance.BaseUrl;
        if (string.IsNullOrEmpty(baseUrl))
            Debug.Log("[App] NetworkConfig sin BaseUrl aún.");
    }
}
