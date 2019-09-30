using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class CannyEdgeCableCanal : CableCanalBehaviour
{
    public bool sobreescribirMat = true;
    public double umbralUno = 50d,umbralDos = 150d;
    [PopupCustom(3,5,7)]
    public int apertureSize = 3;
    public bool L2gradient = false;
    public override Mat MatOut()=>mat;

    Mat mat = null;
    
    public override void Actualizar(IEsCable nodo){
        
        if (nodo == null) return;

        var matEntrante = nodo.MatOut();
        if (matEntrante == null) return;
        if(apertureSize%2==0)apertureSize--;
        if (apertureSize<3)apertureSize=3;
        else if (apertureSize>7)apertureSize=7;
        
        if (sobreescribirMat)
        {
            if (mat != matEntrante) mat = matEntrante;
        }
        else
        {
            if (mat == null) mat = new Mat(matEntrante.Size(),matEntrante.Type());
            else {
                if (mat.Width != matEntrante.Width || mat.Height != matEntrante.Height) {
                    if(mat.Width*mat.Height==matEntrante.Width*matEntrante.Height)mat.Reshape(0,matEntrante.Rows);
                    else{
                        mat = matEntrante.Clone();
                    }
                }
            }
        }
        Cv2.Canny(matEntrante,mat,umbralUno,umbralDos,apertureSize,L2gradient);
        PropagarActualizacion();
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(CannyEdgeCableCanal))]
    public class CannyEdgeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (DrawDefaultInspector()) {
                foreach(CannyEdgeCableCanal coso in targets){
                    coso.Actualizar();
                }
            }
            var dispMat = target as CannyEdgeCableCanal;

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