using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class ExtendiendoClasesDeCV{
    public static Vector2 ToVector2(this Point entrada) {
        return new Vector2(entrada.X,entrada.Y);
    }
}

#if UNITY_EDITOR
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