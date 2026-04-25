using UnityEngine;

public class ReticleToLineEnd : MonoBehaviour
{
    public LineRenderer line;
    public Transform lookAtCamera;

    public float forwardOffset = 0.0005f;
    public float minScale = 0.008f;
    public float maxScale = 0.015f;
    public float minVisibleDistance = 0.05f;

    public Color reticleColor = Color.white;

    private Renderer rend;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = reticleColor;
        }
    }

    void LateUpdate()
    {
        if (line == null || line.positionCount < 2)
        {
            SetVisibility(false);
            return;
        }

        int last = line.positionCount - 1;

        Vector3 start = line.GetPosition(0);
        Vector3 end = line.GetPosition(last);

        Vector3 dir = (end - start).normalized;
        transform.position = end + dir * forwardOffset;

        if (lookAtCamera != null)
        {
            transform.LookAt(lookAtCamera);
        }

        float dist = Vector3.Distance(start, end);
        bool visible = dist >= minVisibleDistance;
        SetVisibility(visible);

        if (!visible)
            return;

        float s = Mathf.Lerp(minScale, maxScale, Mathf.InverseLerp(0.2f, 5f, dist));
        transform.localScale = Vector3.one * s;
    }

    void SetVisibility(bool visible)
    {
        if (line != null)
        {
            line.enabled = visible;
        }

        if (rend != null)
        {
            rend.enabled = visible;
        }
    }
}
