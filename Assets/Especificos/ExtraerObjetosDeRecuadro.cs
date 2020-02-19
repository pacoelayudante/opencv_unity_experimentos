using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using System.Linq;
using UnityEngine.UI;
using Mathd = System.Math;
using OCVUnity = OpenCvSharp.Unity;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ExtraerObjetosDeRecuadro : MonoBehaviour
{
    readonly static Scalar colRojo = new Scalar(0, 0, 255, 255), colVerde = new Scalar(0, 255, 0, 255);

    public RawImage imgVisualizar;
    public EncontrarRecuadros recuadros;
    public List<Texture2D> texturasGeneradas = new List<Texture2D>();
    [Min(0)]
    public int indiceRecuadroSel = 0;

    public void Extraer()
    {
        if (recuadros == null && (recuadros = FindObjectOfType<EncontrarRecuadros>()) == null) return;
        if (recuadros.recuadros.Count == 0) return;

        ClearTexturas();
        Point[][] contornos = recuadros.contornos;
        HierarchyIndex[] jerarquias = recuadros.jerarquiaContornos;

        Recuadro recuadro = recuadros.recuadros[indiceRecuadroSel % recuadros.recuadros.Count];
        HierarchyIndex jerarquia = jerarquias[recuadro.indiceContorno];

        Mat matUmbral = recuadros.matUmbralEscalado.Clone();
        Cv2.Dilate(matUmbral,matUmbral,new Mat(),null,3);
        Cv2.Erode(matUmbral,matUmbral,new Mat(),null,2);
        Cv2.CvtColor(matUmbral, matUmbral, ColorConversionCodes.GRAY2BGR);
        List<int> hijosDirectos = new List<int>();
        int hijo = jerarquia.Child;
        while (hijo != -1)
        {
            if (contornos[hijo].Length > 1) hijosDirectos.Add(hijo);
            hijo = jerarquias[hijo].Next;
        }
        Debug.Log(hijosDirectos.Aggregate("", (s, e) => $"{e}({contornos[e].Length})-{s}"));
        foreach (var i in hijosDirectos)
        {
            Cv2.DrawContours(matUmbral, contornos, i, colRojo);
            Cv2.Polylines(matUmbral, new Point[][] { Cv2.ConvexHull(contornos[i]) }, true, colVerde);
        }

        var texturaObjetos = OCVUnity.MatToTexture(matUmbral, GenerarTextura(matUmbral.Width, matUmbral.Height));


    }

    void OnDestroy()
    {
        ClearTexturas();
    }

    Texture2D GenerarTextura(int ancho, int alto)
    {
        var textura = new Texture2D(ancho, alto);
        textura.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        texturasGeneradas.Add(textura);
        return textura;
    }
    void ClearTexturas()
    {
#if UNITY_EDITOR
        foreach (var t in texturasGeneradas) DestroyImmediate(t);
#endif
        texturasGeneradas.Clear();
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ExtraerObjetosDeRecuadro))]
    public class ObjetosEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            int maxSel = targets.Select(e=>((ExtraerObjetosDeRecuadro)e).texturasGeneradas.Count).Max();
            EditorGUI.BeginChangeCheck();
            var sel = EditorGUILayout.Popup("Visualizar",-1,Enumerable.Repeat(0,maxSel).Select((e,index)=>index.ToString()).ToArray());
            if (EditorGUI.EndChangeCheck()&&sel != -1) {
                foreach (ExtraerObjetosDeRecuadro coso in targets) {
                    if (coso.imgVisualizar) coso.imgVisualizar.texture = coso.texturasGeneradas[sel%coso.texturasGeneradas.Count];
                }
            }
            if (GUILayout.Button("Extraer"))
            {
                foreach (ExtraerObjetosDeRecuadro coso in targets)
                {
                    coso.Extraer();
                }
                EditorWindow view = EditorWindow.GetWindow<SceneView>();
                view.Repaint();
            }
        }
    }
#endif
}
