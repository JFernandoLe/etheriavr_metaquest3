using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Corrige la iluminación del público cuando hay Light Probes habilitados pero no existen
/// sondas en la escena (causa común de personajes casi negros en teatros con lightmaps).
/// </summary>
[DefaultExecutionOrder(-50)]
public class AudienceLightingSetup : MonoBehaviour
{
    [SerializeField] private bool generateLightProbes = true;
    [SerializeField] private float probeHeightOffset = 1.4f;
    [SerializeField] private int gridDivisions = 4;
    [SerializeField] private float gridPadding = 1.5f;

    void Start()
    {
        GameObject[] audience = GameObject.FindGameObjectsWithTag("Publico");
        if (audience.Length == 0)
        {
            Debug.LogWarning("[AudienceLightingSetup] No se encontraron objetos con tag Publico.");
            return;
        }

        if (generateLightProbes)
            CreateLightProbeGroup(audience);

        ConfigureAudienceRenderers(audience);
        Debug.Log($"[AudienceLightingSetup] Configurados {audience.Length} personajes del público.");
    }

    private void CreateLightProbeGroup(GameObject[] audience)
    {
        if (FindObjectOfType<LightProbeGroup>() != null)
            return;

        Bounds bounds = CalculateBounds(audience);
        var positions = new List<Vector3>();

        float stepX = bounds.size.x / Mathf.Max(1, gridDivisions);
        float stepZ = bounds.size.z / Mathf.Max(1, gridDivisions);

        for (int x = 0; x <= gridDivisions; x++)
        {
            for (int z = 0; z <= gridDivisions; z++)
            {
                float px = bounds.min.x - gridPadding + stepX * x;
                float pz = bounds.min.z - gridPadding + stepZ * z;
                float py = bounds.center.y + probeHeightOffset;
                positions.Add(new Vector3(px, py, pz));
            }
        }

        foreach (GameObject person in audience)
            positions.Add(person.transform.position + Vector3.up * probeHeightOffset);

        var probeObject = new GameObject("AudienceLightProbes");
        var group = probeObject.AddComponent<LightProbeGroup>();
        group.probePositions = positions.ToArray();

        LightProbes.Tetrahedralize();
    }

    private static void ConfigureAudienceRenderers(GameObject[] audience)
    {
        foreach (GameObject person in audience)
        {
            foreach (SkinnedMeshRenderer smr in person.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.receiveShadows = true;
                smr.shadowCastingMode = ShadowCastingMode.On;
                smr.lightProbeUsage = LightProbeUsage.BlendProbes;
                smr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
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

    private static Bounds CalculateBounds(GameObject[] objects)
    {
        Bounds bounds = new Bounds(objects[0].transform.position, Vector3.zero);
        foreach (GameObject obj in objects)
            bounds.Encapsulate(obj.transform.position);
        return bounds;
    }
}
