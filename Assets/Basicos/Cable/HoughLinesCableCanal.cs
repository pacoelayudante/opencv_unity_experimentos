using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class HoughLinesCableCanal : CableCanalBehaviour
{
    public HoughMethods metodo = HoughMethods.Standard;
    public double rho = 1d;
    public double thetaDiv = 180d;
    public double Theta { get => System.Math.PI / thetaDiv; }// = System.Math.PI / 180d;
    public int umbral = 200;
    [Ocultador("metodo", (int)HoughMethods.MultiScale)]
    [Tooltip("for the multi-scale Hough transform, it is a divisor for the distance resolution rho . The coarse accumulator distance resolution is rho and the accurate accumulator resolution is rho/srn . If both srn=0 and stn=0 , the classical Hough transform is used. Otherwise, both these parameters should be positive.")]
    public double srn = 1d;
    [Ocultador("metodo", (int)HoughMethods.MultiScale)]
    [Tooltip("for the multi-scale Hough transform, it is a divisor for the distance resolution theta.")]
    public double stn = 1d;

    [Ocultador("metodo", (int)HoughMethods.Probabilistic)]
    [Tooltip("minimum line length. Line segments shorter than that are rejected.")]
    public double minLineLength = 0d;
    [Ocultador("metodo", (int)HoughMethods.Probabilistic)]
    [Tooltip("maximum allowed gap between points on the same line to link them.")]
    public double maxLineGap = 0d;

    LineSegmentPolar[] resultadoLineas;
    LineSegmentPoint[] resultadoSegmentos;

    public RectTransform mostrarAca;
    [Ocultador("mostrarAca")]
    public int maxLineas = 30;
    Vector2 tamOrigen;
    [HideInInspector]
    [SerializeField]
    List<Image> poolDeLineas = new List<Image>();
    void UpdateMostrar()
    {
        if (mostrarAca && (resultadoLineas != null || resultadoSegmentos != null))
        {
            var listaLength = resultadoLineas==null?resultadoSegmentos.Length: resultadoLineas.Length;
            poolDeLineas = poolDeLineas.Where(e => e != null).ToList();
            for (int i =listaLength - poolDeLineas.Count; i > 0; i--)
            {
                if (poolDeLineas.Count >= maxLineas) break;
                var nuevoLinea = new GameObject();
                nuevoLinea.transform.SetParent(mostrarAca);
                nuevoLinea.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                var rect = nuevoLinea.AddComponent<RectTransform>();
                var img = nuevoLinea.AddComponent<Image>();
                // rect.sizeDelta = new Vector2(tamOrigen.y, 3);
                rect.sizeDelta = new Vector2(tamOrigen.y, 3);
                poolDeLineas.Add(img);
            }
            if (resultadoLineas != null) MostrarLineas();
            if (resultadoSegmentos != null) MostrarSegmentos();
        }
    }
    void MostrarLineas()
    {
        var escala = mostrarAca.rect.width / tamOrigen.x;
        for (int i = 0; i < poolDeLineas.Count; i++)
        {
            if (poolDeLineas[i].transform.parent != mostrarAca) poolDeLineas[i].transform.SetParent(mostrarAca);
            if (i < resultadoLineas.Length && i < maxLineas)
            {
                poolDeLineas[i].enabled = true;
                var rect = poolDeLineas[i].transform as RectTransform;
                rect.sizeDelta = new Vector2(500, 1);
                rect.pivot = new Vector2(.5f, .5f);

                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                rect.Rotate(0f, 0f, -resultadoLineas[i].Theta * Mathf.Rad2Deg);
                rect.Translate(resultadoLineas[i].Rho * escala, 0f, 0f, Space.Self);
                rect.Rotate(0f, 0f, 90f);
            }
            else poolDeLineas[i].enabled = false;
        }
    }
    void MostrarSegmentos()
    {
        var escala = mostrarAca.rect.width / tamOrigen.x;
        for (int i = 0; i < poolDeLineas.Count; i++)
        {
            if (poolDeLineas[i].transform.parent != mostrarAca) poolDeLineas[i].transform.SetParent(mostrarAca);
            if (i < resultadoSegmentos.Length && i < maxLineas)
            {
                poolDeLineas[i].enabled = true;
                var rect = poolDeLineas[i].transform as RectTransform;
                rect.sizeDelta = new Vector2((float)resultadoSegmentos[i].P1.DistanceTo(resultadoSegmentos[i].P2)*escala, 1);
                rect.pivot = new Vector2(0f, .5f);

                var pos = resultadoSegmentos[i].P1.ToVector2();
                pos.Scale(new Vector2(escala,-escala));
                rect.localPosition = pos;
                var angulo = Vector2.SignedAngle(Vector2.right,(resultadoSegmentos[i].P1-resultadoSegmentos[i].P2).ToVector2());
                rect.localRotation = Quaternion.Euler(0f,0f,180f-angulo);
                //rect.Rotate(0f, 0f, -resultadoLineas[i].Theta * Mathf.Rad2Deg);
                //rect.Translate(resultadoLineas[i].Rho * escala, 0f, 0f, Space.Self);
                //rect.Rotate(0f, 0f, 90f);
            }
            else poolDeLineas[i].enabled = false;
        }
    }

    public override void Actualizar(IEsCable nodo)
    {

        if (nodo == null || !enabled) return;

        var matEntrante = nodo.MatOut();
        if (matEntrante == null) return;

        // if (sobreescribirMat)
        // {
        //     if (mat != matEntrante) mat = matEntrante;
        // }
        // else
        // {
        //     if (mat == null) mat = new Mat(matEntrante.Size(),matEntrante.Type());
        //     else {
        //         if (mat.Width != matEntrante.Width || mat.Height != matEntrante.Height) mat.Reshape(matEntrante.Width,matEntrante.Height);
        //     }
        // }
        resultadoSegmentos = null;
        resultadoLineas = null;

        if (metodo == HoughMethods.Standard) resultadoLineas = Cv2.HoughLines(matEntrante, rho, Theta, umbral);
        else if (metodo == HoughMethods.MultiScale)
        {
            if (srn <= 0d) srn = 1d;
            if (stn <= 0d) stn = 1d;
            resultadoLineas = Cv2.HoughLines(matEntrante, rho, Theta, umbral, srn, stn);
        }
        else if (metodo == HoughMethods.Probabilistic) resultadoSegmentos = Cv2.HoughLinesP(matEntrante, rho, Theta, umbral, minLineLength, maxLineGap);
        else if (metodo == HoughMethods.Gradient)
        {

        }
        tamOrigen = new Vector2(matEntrante.Width, matEntrante.Height);

        PropagarActualizacion();
        UpdateMostrar();
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(HoughLinesCableCanal))]
    public class HoughLinesEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Actualizar"))
            {
                foreach (HoughLinesCableCanal coso in targets)
                {
                    coso.Actualizar();
                }
            }
            var dispMat = target as HoughLinesCableCanal;

            var resultadoL = dispMat.resultadoLineas;
            if (resultadoL == null) GUILayout.Label("Lineas es NULL");
            else  GUILayout.Label(string.Format("Lineas.Length = {0}", resultadoL.Length));
            var resultadoS = dispMat.resultadoSegmentos;
            if (resultadoS == null) GUILayout.Label("Segmentos es NULL");
            else  GUILayout.Label(string.Format("Segmentos.Length = {0}", resultadoS.Length));

        }
    }
#endif
}
