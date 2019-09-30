using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class DisplayCableCanal : CableCanalBehaviour
{    
    AspectRatioFitter aspect;
    RawImage rawImage;
    Texture2D textura;
    Mat mat;

    RawImage RawImage { get => rawImage ? rawImage : rawImage = GetComponent<RawImage>(); }
    AspectRatioFitter Aspect { get => aspect ? aspect : aspect = GetComponent<AspectRatioFitter>(); }
    
    public Texture2D Textura
    {
        get
        {
            if (textura == null)
            {
                textura = new Texture2D(2, 2);
                textura.hideFlags = HideFlags.DontSaveInBuild|HideFlags.DontSaveInEditor;
            }
            if (RawImage) RawImage.texture = textura;
            if (Aspect) Aspect.aspectRatio = textura.width / (float)textura.height;
            return textura;
        }
        set
        {
            textura = value;
            if (RawImage) RawImage.texture = textura;
        }
    }
    public Mat Mat
    {
        get
        {
            return mat;
        }
        set
        {
            mat = value;
            if (mat != null)
            {
                var size = mat.Size();
                if (Textura.width != size.Width || Textura.height != size.Height) Textura.Resize(size.Width, size.Height);
                OpenCvSharp.Unity.MatToTexture(mat, Textura);
            }
        }
    }

    public override void Actualizar(IEsCable nodo)
    {
        if (nodo == null) return;

        var niuMat = nodo.MatOut();
        if (niuMat != null)
        {
            Mat = niuMat;
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(DisplayCableCanal))]
    public class DisplayEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var dispMat = target as DisplayCableCanal;
            if (GUILayout.Button("OnEnable")) dispMat.OnEnable();

            EditorGUILayout.ObjectField(dispMat.textura, typeof(Texture2D), true);
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
