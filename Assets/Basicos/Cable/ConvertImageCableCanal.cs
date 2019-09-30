using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class ConvertImageCableCanal : CableCanalBehaviour
{
    public override Mat MatOut()=>mat;

    public ColorConversionCodes tipoDeConversion = ColorConversionCodes.BGR2GRAY;
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
        Cv2.CvtColor(mat,mat,tipoDeConversion);
        PropagarActualizacion();
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(ConvertImageCableCanal))]
    public class ConvertEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var dispMat = target as ConvertImageCableCanal;

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
