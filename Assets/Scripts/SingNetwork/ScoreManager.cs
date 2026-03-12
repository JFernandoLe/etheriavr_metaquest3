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

    void Awake()
    {
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

        Debug.Log("Accuracy: " + accuracyPercent);
    }
}