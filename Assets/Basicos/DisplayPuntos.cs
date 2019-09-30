using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class DisplayPuntos : MonoBehaviour, IEntraCable
{
    public MonoBehaviour entrada;
    public DisplayMat matReferencia;

    /*
    Micro cosas genericas
     */
    // IEntraCable
    public IEsCable Entrada()=> entrada as IEsCable;
    /*
    END microcosas genericas
     */

    void OnEnable()
    {
        this.IniciarConexionesExt();
    }
    void OnDisable()
    {
        this.TerminarConexionesExt();
    }

    void Actualizar()
    {
        Actualizar(Entrada());
    }
    public void Actualizar(IEsCable nodo)
    {
        if (nodo == null) return;
        
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(DisplayPuntos))]
    public class DisplayPuntosEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }
#endif
}
