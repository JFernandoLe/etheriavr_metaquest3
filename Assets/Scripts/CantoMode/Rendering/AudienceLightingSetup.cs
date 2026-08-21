using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Corrige la iluminación del público cuando hay Light Probes habilitados pero no existen
/// sondas válidas en la escena (personajes casi negros en teatros con lightmaps).
/// </summary>
[DefaultExecutionOrder(-50)]
public class AudienceLightingSetup : MonoBehaviour
{
    void Start()
    {
        GameObject[] audience = GameObject.FindGameObjectsWithTag("Publico");
        if (audience.Length == 0)
        {
            Debug.LogWarning("[AudienceLightingSetup] No se encontraron objetos con tag Publico.");
            return;
        }

        ConfigureAudienceRenderers(audience);
        Debug.Log($"[AudienceLightingSetup] Configurados {audience.Length} personajes del público.");
    }

    private static void ConfigureAudienceRenderers(GameObject[] audience)
    {
        foreach (GameObject person in audience)
        {
            foreach (SkinnedMeshRenderer smr in person.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.receiveShadows = true;
                smr.shadowCastingMode = ShadowCastingMode.On;
                // Sin LightProbeGroup en escena, BlendProbes samplea datos vacíos → personajes negros.
                smr.lightProbeUsage = LightProbeUsage.Off;
                smr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                smr.updateWhenOffscreen = false;

                foreach (Material mat in smr.sharedMaterials)
                {
                    if (mat == null)
                        continue;

                    if (mat.HasProperty("_EnvironmentReflections"))
                        mat.SetFloat("_EnvironmentReflections", 1f);
                }
            }
        }
    }
}
