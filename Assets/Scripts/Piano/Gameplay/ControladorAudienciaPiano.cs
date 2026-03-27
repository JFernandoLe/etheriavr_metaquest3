using System.Collections.Generic;
using UnityEngine;

public class ControladorAudienciaPiano : MonoBehaviour
{
    [Header("Configuración de Desempeño")]
    [Range(0f, 100f)]
    public float puntajePiano = 0f;
    public PianoPublicSystem sistemaPublico;

    [Header("Rotación")]
    public Transform jugador;
    public float velocidadRotacion = 2f;

    private readonly List<Animator> listaAnimadores = new List<Animator>();

    void Start()
    {
        if (sistemaPublico == null)
        {
            sistemaPublico = FindObjectOfType<PianoPublicSystem>();
        }

        if (jugador == null && Camera.main != null)
        {
            jugador = Camera.main.transform;
        }

        GameObject[] personajes = GameObject.FindGameObjectsWithTag("Publico");
        foreach (GameObject personaje in personajes)
        {
            Animator anim = personaje.GetComponent<Animator>();
            if (anim != null)
            {
                listaAnimadores.Add(anim);
            }
        }
    }

    void Update()
    {
        if (sistemaPublico != null)
        {
            puntajePiano = Mathf.Lerp(puntajePiano, sistemaPublico.GetCurrentPublicScore(), Time.deltaTime * 1.4f);
        }

        if (jugador == null)
        {
            return;
        }

        foreach (Animator anim in listaAnimadores)
        {
            if (anim == null) continue;

            anim.SetFloat("Calidad", puntajePiano);

            Vector3 direccion = jugador.position - anim.transform.position;
            direccion.y = 0f;

            if (direccion.sqrMagnitude > 0.0001f)
            {
                Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion.normalized);
                anim.transform.rotation = Quaternion.Slerp(
                    anim.transform.rotation,
                    rotacionObjetivo,
                    Time.deltaTime * velocidadRotacion);
            }
        }
    }
}