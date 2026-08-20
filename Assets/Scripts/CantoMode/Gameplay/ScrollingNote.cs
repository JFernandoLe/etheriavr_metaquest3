using UnityEngine;

public class ScrollingNote : MonoBehaviour
{
    public int midi;
    public float totalTime = 0f;
    public float startTime;
    public float duration;
    public bool evaluated = false;
    public bool alreadyScored = false;
    public float correctTime = 0f;
}