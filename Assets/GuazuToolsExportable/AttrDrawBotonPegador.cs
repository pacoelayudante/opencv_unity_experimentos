using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if  UNITY_EDITOR
using UnityEditor;
#endif

namespace GuazuTools
{

    public class AttrDrawBotonPegador : PropertyAttribute
    {

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(AttrDrawBotonPegador))]
        public class AttrDrawBotonPegadorDrawer : PropertyDrawer
        {
            GUIContent content = new GUIContent("Ctrl+V","Debe ser String, Integer o Float");
            float anchoContent = -1f;

            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                if (anchoContent < 0f)
                {
                    anchoContent = EditorStyles.label.CalcSize(content).x;
                }
                position.width -= anchoContent;
                EditorGUI.PropertyField(position, property, label);

                position = new Rect(position.xMax, position.y, anchoContent, position.height);
                var tipoIntrabajable = (property.propertyType != SerializedPropertyType.String
                    && property.propertyType != SerializedPropertyType.Float
                    && property.propertyType != SerializedPropertyType.Integer);
                EditorGUI.BeginDisabledGroup(tipoIntrabajable);
                if (GUI.Button(position, content))
                {
                    EditorGUI.BeginProperty(position, content, property);
                    RealizarCopia(property);
                    EditorGUI.EndProperty();
                }
                EditorGUI.EndDisabledGroup();
            }

            void RealizarCopia(SerializedProperty property)
            {
                var contenido = GUIUtility.systemCopyBuffer;
                if (!string.IsNullOrEmpty(contenido))
                {
                    if (property.propertyType == SerializedPropertyType.String)
                    {
                        property.stringValue = contenido;
                    }
                    else if (property.propertyType == SerializedPropertyType.Float)
                    {
                        property.floatValue = float.Parse(contenido);
                    }
                    else if (property.propertyType == SerializedPropertyType.Integer)
                    {
                        property.intValue = int.Parse(contenido);
                    }
                }
            }
        }
#endif

    }

}