using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
public class EditorDePuntos : EditorWindow
{
    Vector2 scroll;
    List<Vector2> puntos;
    Texture2D referencia;
    System.Action<List<Vector2>> actualizar;
    Material mat;
    int drageando = -1;

    public static EditorDePuntos Abrir(System.Action<List<Vector2>> actualizar, Vector2[] puntos, Texture2D referencia = null)
    {
        var edit = EditorDePuntos.CreateWindow<EditorDePuntos>();
        edit.puntos = new List<Vector2>(puntos);
        edit.referencia = referencia;
        edit.actualizar = actualizar;
        return edit;
    }

    private void GenMat()
    {
        var shader = Shader.Find("Hidden/Internal-Colored");
        mat = new Material(shader);
    }
    private void OnDestroy()
    {
        DestroyImmediate(mat);
    }

    public void Shift<T>(List<T> items, bool paraAlla) {
        int i = paraAlla?0:items.Count-1;
        var mueve = items[i];
        items.RemoveAt(i);
        i = items.Count-i;
        items.Insert(i,mueve);
    }

    private void OnGUI()
    {
        if (referencia)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<< Shift")) {
                Shift(puntos,true);
                if(actualizar!=null)actualizar.Invoke(puntos);
            }
            if (GUILayout.Button("Shift >>")) {
                Shift(puntos,false);
                if(actualizar!=null)actualizar.Invoke(puntos);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            var pos = GUILayoutUtility.GetAspectRect(referencia.width / (float)referencia.height);
            float escala = pos.width / (float)referencia.width;
            EditorGUI.DrawTextureTransparent(pos, referencia);
            GUI.BeginClip(pos);
            var mPos = Event.current.mousePosition;
            // mPos.y = pos.height - mPos.y;
            mPos /= escala;
            if (Event.current.type == EventType.MouseDown)
            {
                drageando = -1;
                for (int i = 0; i < puntos.Count; i++)
                {
                    if (Vector2.Distance(puntos[i], mPos) < 20f / escala) drageando = i;
                }
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                drageando = -1;
            }
            else if (drageando != -1 && Event.current.type == EventType.MouseDrag)
            {
                var delta = Event.current.delta;
                // delta.y = - delta.y;
                delta /= escala;
                puntos[drageando] += delta;
                if(actualizar!=null)actualizar.Invoke(puntos);
                Repaint();
            }
            if (Event.current.type == EventType.Repaint)
            {
                GL.PushMatrix();
                if (!mat) GenMat();
                mat.SetPass(0);
                Vector2 origen = Vector2.zero;//Vector2.down * pos.height;
                GL.Begin(GL.LINES);
                GL.Color(drageando==-1? Color.red:Color.blue);
                for (int i = 0; i < puntos.Count; i++)
                {
                    int i2 = (i + 1) % puntos.Count;
                    // DrawLine(puntos[i] * escala + origen, puntos[i2] * escala + origen, Color.red, 30);
                    // DrawLine(puntos[i] * escala + origen, Vector2.zero, Color.blue, 30);
                    // GL.Clear(true, false, Color.black);

                    var pA = puntos[i] * escala + origen;
                    GL.Vertex3(pA.x, pA.y, 0f);
                    pA = puntos[i2] * escala + origen;
                    GL.Vertex3(pA.x, pA.y, 0);
                    // GL.Vertex3(0,0, 0);
                }
                GL.PopMatrix();
                GL.End();
            }
            GUI.EndClip();
            GUILayout.Space(10f);
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }
    }
}
#endif