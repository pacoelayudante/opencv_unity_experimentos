using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class FindContoursCableCanal : CableCanalBehaviour
{
    public override Mat MatOut() => dibujo;

    public RetrievalModes retrievalMode = RetrievalModes.External;
    public ContourApproximationModes approxMode = ContourApproximationModes.ApproxNone;
    public Gradient colContornos = new Gradient();
    public float loopColorsEvery = 100;
    public LineTypes tipoDibujo = LineTypes.AntiAlias;
    public float umbralArea = 100;
    public bool ordenarPorArea = true;
    Mat mat = null;
    Mat dibujo = null;
    Point[][] puntos;

    public override void Actualizar(IEsCable nodo)
    {

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
        puntos = Cv2.FindContoursAsArray(mat, retrievalMode, approxMode);
        if(ordenarPorArea) puntos = puntos.OrderByDescending(e=>Cv2.ContourArea(e)).ToArray();
        dibujo = new Mat(matEntrante.Rows, matEntrante.Cols, MatType.CV_8UC3);
        var loopColorsEvery = this.loopColorsEvery;
        if (loopColorsEvery < 1) loopColorsEvery = 1;
        for (int i = 0; i < puntos.Length; i++)
        {
            var area = Cv2.ContourArea(puntos[i]);
            if (area < umbralArea) continue;
            var colContornos = this.colContornos.Evaluate((i % loopColorsEvery) / (float)loopColorsEvery);
            var colScalar = new Scalar(colContornos.b * 255, colContornos.g * 255, colContornos.r * 255);
            Cv2.DrawContours(dibujo, puntos, i, colScalar, 1, tipoDibujo);
        }
        PropagarActualizacion();
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(FindContoursCableCanal))]
    public class CountoursEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Actualizar"))
            {
                foreach (FindContoursCableCanal coso in targets)
                {
                    coso.Actualizar();
                }
            }
            var dispMat = target as FindContoursCableCanal;

            var mat = dispMat.dibujo;
            if (mat == null) GUILayout.Label("Mat es NULL");
            else
            {
                GUILayout.Label(string.Format("Mat es {0}", mat.CvPtr));
                GUILayout.Label(string.Format("tam : ({0},{1})", mat.Width, mat.Height));
            }
            if (dispMat.puntos != null)
            {
                GUILayout.Label(string.Format("contornos : {0}", dispMat.puntos.Length));
                for (int i = 0; i < dispMat.puntos.Length && i < 5; i++)
                {
                    GUILayout.Label(string.Format("area de {0}: {1}", i, Cv2.ContourArea(dispMat.puntos[i])));
                }
            }

        }
    }
#endif
}
