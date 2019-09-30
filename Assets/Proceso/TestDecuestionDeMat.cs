using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TestDecuestionDeMat : MonoBehaviour
{

    public Texture2D convertirTextura;
    Mat elMat;
    Size tamElMat;
    System.IntPtr cosin,otrocosin;
    public Texture2D laCosaQueSalio;
    public UnityEngine.UI.RawImage mostrarAca;

    void ConvertirTextura()
    {
        elMat = OpenCvSharp.Unity.TextureToMat(convertirTextura);
        tamElMat = elMat.Size();
        cosin = elMat.CvPtr;
        otrocosin= elMat.Ptr(0);
    }
    void GenerarLaCosa()
    {
        var sale = new Mat(tamElMat.Width,tamElMat.Height,MatType.CV_8UC4);
        elMat.ConvertTo(sale,MatType.CV_8UC4);
        laCosaQueSalio = Texture2D.CreateExternalTexture(sale.Width, sale.Height, TextureFormat.ARGB32, false, false, sale.CvPtr);
        if (laCosaQueSalio && mostrarAca) mostrarAca.texture = laCosaQueSalio;
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(TestDecuestionDeMat))]
    public class TestEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
                if (GUILayout.Button("Generar Mat")){
            foreach (TestDecuestionDeMat coso in targets)
            {
                coso .ConvertirTextura();
            }
                }
                if (GUILayout.Button("Convertir La Cosa")){
            foreach (TestDecuestionDeMat coso in targets)
            {
                coso .GenerarLaCosa();
            }
                }
            foreach (TestDecuestionDeMat coso in targets){
                GUILayout.Label(coso.elMat==null?"El MAt es NULL":"Hay Ek Mat");
                GUILayout.Label(coso.tamElMat==null?"Sin Size amigo":string.Format("Size:{0}x{1}",coso.tamElMat.Width,coso.tamElMat.Height));
                GUILayout.Label(coso.cosin.ToString());
                GUILayout.Label(coso.otrocosin.ToString());
            }
        }
    }
#endif
}
