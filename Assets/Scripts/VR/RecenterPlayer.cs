using UnityEngine;
using System.Collections;
using Unity.XR.CoreUtils;

public class RecenterPlayer : MonoBehaviour
{
    public XROrigin xrOrigin;
    public Transform pianoSpawnPoint;
    [SerializeField] private bool autoRecenterOnStart = false;
    [SerializeField] private bool autoRecenterOnApplicationFocus = false;

    void Start()
    {
        if (autoRecenterOnStart && xrOrigin != null && pianoSpawnPoint != null)
        {
            StartCoroutine(WaitAndRecenter());
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (autoRecenterOnApplicationFocus && hasFocus && xrOrigin != null && pianoSpawnPoint != null)
        {
            DoRecenter();
        }
    }

    IEnumerator WaitAndRecenter()
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