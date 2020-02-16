using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class ThresholdCableCanal : CableCanalBehaviour
{
    public override Mat MatOut()=>mat;

    public ThresholdTypes tipoDeUmbral = ThresholdTypes.Binary;
    [Range(0,255)]
    public double umbral = 0.5d;
    [Range(0,255)]
    public double valorMaximo = 1d;
    Mat mat = null;
    
    public override void Actualizar(IEsCable nodo){
        
        if (nodo == null) return;

        var matEntrante = nodo.MatOut();
        if (matEntrante == null) return;
        
        // if (generarNuevoMat)
        // {
        //     if (mat == null) mat = new Mat();
        // }
        // else
        {
            if (mat != matEntrante) mat = matEntrante;
        }
        Cv2.Threshold(mat,mat,umbral,valorMaximo,tipoDeUmbral);
        PropagarActualizacion();
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(ThresholdCableCanal))]
    public class ThreshEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();            
            if (GUILayout.Button("Actualizar"))
            {
                foreach (ThresholdCableCanal coso in targets)
                {
                    coso.Actualizar();
                }
            }
            var dispMat = target as ThresholdCableCanal;

            var mat = dispMat.mat;
            if (mat == null) GUILayout.Label("Mat es NULL");
            else
            {
                GUILayout.Label(string.Format("Mat es {0}", mat.CvPtr));
                GUILayout.Label(string.Format("tam : ({0},{1})", mat.Width, mat.Height));
            }

        }
    }
#endif
}