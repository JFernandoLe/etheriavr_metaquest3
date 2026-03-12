using UnityEngine;
using System.Collections.Generic;
using System;

public class ControladorAudiencia : MonoBehaviour
{
    [Header("Configuracion de Desempeño")]
    [Range(0f, 100f)]
    public float puntajeCanto = 100f;
    public AudioSource fuenteAplausos;

    [Header("Rotaci�n")]
    public Transform jugador;
    public float velocidadRotacion = 2f;

    private List<Animator> listaAnimadores = new List<Animator>();
    private bool yaEstaAplaudiendo = false;

    void Start()
    {
        // Buscamps personajes con el Tag "Publico"
        GameObject[] personajes = GameObject.FindGameObjectsWithTag("Publico");
        foreach (GameObject personaje in personajes)
        {
            Animator anim = personaje.GetComponent<Animator>();
            if (anim != null) listaAnimadores.Add(anim);
        }
    }

    void Update()
    {
        if (ScoreManager.Instance != null)
        {
            puntajeCanto = Mathf.Lerp(
                puntajeCanto,
                ScoreManager.Instance.accuracyPercent,
                Time.deltaTime * 2f
            );
            Debug.Log("Puntaje canto: " + puntajeCanto);
        }

        if (jugador == null) return;

        foreach (Animator anim in listaAnimadores)
        {
            if (anim == null) continue;

            anim.SetFloat("Calidad", puntajeCanto);

            Vector3 direccion = jugador.position - anim.transform.position;
            direccion.y = 0;

            if (direccion != Vector3.zero)
            {
                Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion);
                anim.transform.rotation = Quaternion.Slerp(
                    anim.transform.rotation,
                    rotacionObjetivo,
                    Time.deltaTime * velocidadRotacion
                );
            }
        }

        ManejarAudio();
    }

    void ManejarAudio()
    {
        if (fuenteAplausos == null) return;

        if (puntajeCanto > 70 && !yaEstaAplaudiendo)
        {
            fuenteAplausos.Play();
            yaEstaAplaudiendo = true;
        }
        else if (puntajeCanto <= 70 && yaEstaAplaudiendo)
        {
            fuenteAplausos.Stop();
            yaEstaAplaudiendo = false;
        }
    }
}