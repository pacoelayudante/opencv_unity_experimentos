using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class ClonarMatCableCanal : CableCanalBehaviour
{
    public override Mat MatOut()=>mat;
    Mat mat = null;
    [Range(0.00001f,1f)]
    public double escala = 0.5f;
    InterpolationFlags interpolation = InterpolationFlags.Linear;
    
    public override void Actualizar(IEsCable nodo){
        
        if (nodo == null) return;

        var matEntrante = nodo.MatOut();
        if (matEntrante == null) return;
        
        // if (generarNuevoMat)
        // {
        //     if (mat == null) mat = new Mat();
        // }
        // else
        // {
        //     if (mat != matEntrante) mat = matEntrante;
        // }
        mat = matEntrante.Resize(new Size(0,0),escala,escala,interpolation);
        PropagarActualizacion();
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(ClonarMatCableCanal))]
    public class ClonarEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Actualizar"))
            {
                foreach (ClonarMatCableCanal coso in targets)
                {
                    coso.Actualizar();
                }
                EditorWindow view = EditorWindow.GetWindow<SceneView>();
                view.Repaint();
            }
            var dispMat = target as ClonarMatCableCanal;

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