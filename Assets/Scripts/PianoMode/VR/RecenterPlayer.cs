using UnityEngine;
using System.Collections;
using Unity.XR.CoreUtils;

/// <summary>
/// Recoloca el XR Origin sobre el punto de spawn del piano.
/// </summary>
public class RecenterPlayer : MonoBehaviour
{
    public XROrigin xrOrigin;
    public Transform pianoSpawnPoint;

    [SerializeField] private bool autoRecenterOnStart = false;
    [SerializeField] private bool autoRecenterOnApplicationFocus = false;

    private bool CanRecenter => xrOrigin != null && pianoSpawnPoint != null;

    void Start()
    {
        if (autoRecenterOnStart && CanRecenter) StartCoroutine(WaitAndRecenter());
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (autoRecenterOnApplicationFocus && hasFocus && CanRecenter) DoRecenter();
    }

    // Se espera un instante a que el rig XR termine de inicializarse.
    private IEnumerator WaitAndRecenter()
    {
        yield return new WaitForSeconds(0.2f);
        DoRecenter();
    }

    public void DoRecenter()
    {
        xrOrigin.MoveCameraToWorldLocation(pianoSpawnPoint.position);
        xrOrigin.MatchOriginUpCameraForward(pianoSpawnPoint.up, pianoSpawnPoint.forward);

        Debug.Log("<color=green>[XR]</color> Recentrado automático aplicado.");
    }
}
