using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int score = 0;

    private float accuracySum = 0f;
    private int totalEvaluations = 0;

    public float accuracyPercent = 0f;

    public TextMeshPro scoreText;

    private float rhythmSum = 0f;
    private int rhythmCount = 0;

    public float rhythmPercent = 0f;

    void Awake()
    {
        Debug.Log("ScoreManager activo: " + gameObject.name);
        Instance = this;
    }

    public void AddScore(int amount)
    {
        score += amount;

        if (scoreText != null)
            scoreText.text = score.ToString();
    }

    public void RegisterHit(float value)
    {
        accuracySum += value;
        totalEvaluations++;

        accuracyPercent = (accuracySum / totalEvaluations) * 100f;

        Debug.Log(" RegisterHit llamado: " + value +
                  " | Total: " + totalEvaluations +
                  " | Accuracy: " + accuracyPercent);
    }

    public void RegisterRhythm(float value)
    {
        rhythmSum += value;
        rhythmCount++;

        rhythmPercent = (rhythmSum / rhythmCount) * 100f;
    }
}