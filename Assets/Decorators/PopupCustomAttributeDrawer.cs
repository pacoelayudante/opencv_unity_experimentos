using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PopupCustomAttribute : PropertyAttribute
{
    int[] ints;
    float[] floats;
    string[] strings;
    bool buttonStyle;
    GUIContent[] lista;
    public PopupCustomAttribute(params int[] ints)
    {
        this.ints = ints;
        lista = ints.Select(e => new GUIContent(e.ToString())).ToArray();
    }
    public PopupCustomAttribute(params float[] floats)
    {
        this.floats = floats;
        lista = floats.Select(e => new GUIContent(e.ToString())).ToArray();
    }
    public PopupCustomAttribute(params string[] strings)
    {
        this.strings = strings;
        lista = strings.Select(e => new GUIContent(e)).ToArray();
    }
    public PopupCustomAttribute(bool buttonStyle, params int[] ints) : this(ints)
    {
        this.buttonStyle = buttonStyle;
    }
    public PopupCustomAttribute(bool buttonStyle, params float[] floats) : this(floats)
    {
        this.buttonStyle = buttonStyle;
    }
    public PopupCustomAttribute(bool buttonStyle, params string[] strings) : this(strings)
    {
        this.buttonStyle = buttonStyle;
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(PopupCustomAttribute))]
    public class PopupCustomAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PopupCustomAttribute popup = attribute as PopupCustomAttribute;
            if (Validar(popup, property))
            {
                var contenido = EditorGUI.BeginProperty(position, label, property);
                EditorGUI.BeginChangeCheck();
                var nuevoValor = EditorGUI.Popup(position, label, SelectedIndex(popup, property), popup.lista);
                if (EditorGUI.EndChangeCheck())
                {
                    if (property.propertyType == SerializedPropertyType.Integer)
                    {
                        property.intValue = popup.ints[nuevoValor];
                    }
                    else if (property.propertyType == SerializedPropertyType.Float)
                    {
                        property.floatValue = popup.floats[nuevoValor];
                    }
                    else if (property.propertyType == SerializedPropertyType.String)
                    {
                        property.stringValue = popup.strings[nuevoValor];
                    }
                }
                EditorGUI.EndProperty();
            }
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }

        int SelectedIndex(PopupCustomAttribute popup, SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Integer) return popup.ints.TakeWhile(e => e != property.intValue).Count();
            else if (property.propertyType == SerializedPropertyType.Float) return popup.floats.TakeWhile(e => e != property.floatValue).Count();
            else if (property.propertyType == SerializedPropertyType.String) return popup.strings.TakeWhile(e => e != property.stringValue).Count();
            return -1;
        }

        bool Validar(PopupCustomAttribute popup, SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Integer && popup.ints != null) return true;
            else if (property.propertyType == SerializedPropertyType.Float && popup.floats != null) return true;
            else if (property.propertyType == SerializedPropertyType.String && popup.strings != null) return true;
            else return false;
        }
    }
#endif  
}
