using UnityEngine;
using TMPro;

public class DetallesSesionUI : MonoBehaviour
{
    public TextMeshProUGUI tituloTxt;
    public TextMeshProUGUI afinacionTxt;
    public TextMeshProUGUI ritmoTxt;
    public TextMeshProUGUI globalTxt;

    public void MostrarDatos(PracticeSessionResponse datos)
    {
        tituloTxt.text = datos.song_title;

        afinacionTxt.text = datos.tuning_score.ToString("F0") + "%";
        ritmoTxt.text = datos.rhythm_score.ToString("F0") + "%";

        float promedio = (datos.tuning_score + datos.rhythm_score) / 2f;
        globalTxt.text = promedio.ToString("F0") + "%";
    }
}