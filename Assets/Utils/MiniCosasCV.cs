using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using OCVUnity = OpenCvSharp.Unity;
#if UNITY_EDITOR
using UnityEditor;
#endif

// using UnityEngine.UI;
// using OpenCvSharp;
// using OCVUnity = OpenCvSharp.Unity;

// var textureIn = GameObject.FindGameObjectWithTag("Player").GetComponent<RawImage>().texture;

// var matIn = OCVUnity.TextureToMat((Texture2D)textureIn);
// Cv2.Resize(matIn,matIn,new Size(),0.2,0.2);
// var matShow = matIn.Clone();
// Cv2.CvtColor(matIn, matIn, ColorConversionCodes.BGR2GRAY);
// Cv2.Laplacian(matIn,matIn,MatType.CV_8UC1,7,0.1,0);

// Cv2.Dilate(matIn,matIn,new Mat(),null,3);
// Cv2.Erode(matIn,matIn,new Mat(),null,2);

// Cv2.Threshold(matIn,matIn,250,255,ThresholdTypes.Binary);
// Point[][] contrs;
// HierarchyIndex[] jerar;
// Cv2.FindContours(matIn, out contrs, out jerar, RetrievalModes.List, ContourApproximationModes.ApproxNone);

// for(int i=0; i<contrs.Length; i++) {
//  Cv2.DrawContours(matShow,contrs,i,new Scalar(0,0,255));
// }

// GameObject.FindGameObjectWithTag("Finish").GetComponent<RawImage>().texture = CVFast.TexturaNoSave(matShow);

public static class ExtendiendoClasesDeCV{
    public static Vector2 ToVector2(this Point entrada) {
        return new Vector2(entrada.X,entrada.Y);
    }
}

#if UNITY_EDITOR
public static class CVFast {
    public static Texture2D TexturaNoSave(Mat mat)
    {
        var textura = new Texture2D(mat.Width, mat.Height);
        textura.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        OCVUnity.MatToTexture(mat,textura);
        return textura;
    }
}

[CustomPropertyDrawer(typeof(Point2f))]
public class Point2fDrawer : PropertyDrawer
{
    SerializedProperty X, Y;
    public override void OnGUI(UnityEngine.Rect position, SerializedProperty property, GUIContent label)
    {
        X = property.FindPropertyRelative("X");
        Y = property.FindPropertyRelative("Y");

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        var v2 = EditorGUI.Vector2Field(position, label, new Vector2(X.floatValue, Y.floatValue));
        if (EditorGUI.EndChangeCheck())
        {
            X.floatValue = v2.x;
            Y.floatValue = v2.y;
        }
        EditorGUI.EndProperty();
    }
}
#endif