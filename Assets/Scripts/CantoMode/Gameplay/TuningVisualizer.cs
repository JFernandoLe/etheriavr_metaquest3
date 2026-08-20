using UnityEngine;

public class TuningVisualizer : MonoBehaviour
{
    public SUDPReceiver receiver;
    public Transform indicator;

    public float maxCentsRange = 50f;
    public float movementRange = 1f;

    void Update()
    {
        if (receiver == null || indicator == null)
            return;

        float cents = receiver.GetCurrentCents();
        string state = receiver.GetCurrentTuningState();

        // Clamp para evitar que se salga
        float clamped = Mathf.Clamp(cents, -maxCentsRange, maxCentsRange);

        float normalized = clamped / maxCentsRange;
        float xPosition = normalized * movementRange;

        indicator.localPosition = new Vector3(xPosition, 0, 0);

        Renderer rend = indicator.GetComponent<Renderer>();

        if (state == "PERFECTO")
            rend.material.color = Color.green;
        else if (state == "CASI")
            rend.material.color = Color.yellow;
        else
            rend.material.color = Color.red;
    }
}