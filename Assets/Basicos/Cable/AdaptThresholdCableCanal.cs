using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class AdaptThresholdCableCanal : CableCanalBehaviour
{
    public override Mat MatOut()=>mat;

    public AdaptiveThresholdTypes tipoDeAdaptativo = AdaptiveThresholdTypes.MeanC;
    public ThresholdTypes tipoDeUmbral = ThresholdTypes.Binary;
    [Range(-255,255)]
    public double umbral = 0d;
    [Range(0,255)]
    public double valorMaximo = 255d;
    [Range(3,11)]
    public int blockSize = 3;
    Mat mat = null;

    int BlockSize {
        get{
            if (blockSize<3) return 3;
            if (blockSize%2==0) return blockSize+1;
            else return blockSize;
        }
    }
    
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
        Cv2.AdaptiveThreshold(mat,mat,valorMaximo,tipoDeAdaptativo,tipoDeUmbral,BlockSize,umbral);
        PropagarActualizacion();
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(AdaptThresholdCableCanal))]
    public class AdaptThreshEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();            
            if (GUILayout.Button("Actualizar"))
            {
                foreach (AdaptThresholdCableCanal coso in targets)
                {
                    coso.Actualizar();
                }
            }
            var dispMat = target as AdaptThresholdCableCanal;

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