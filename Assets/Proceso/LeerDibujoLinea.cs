using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using OpenCvSharp;

public class LeerDibujoLinea : MonoBehaviour
{
    [System.Serializable]
    public class TexturaOrigenActualizada : UnityEngine.Events.UnityEvent<Texture2D> { }
    public TexturaOrigenActualizada AlOrigenActualizado = new TexturaOrigenActualizada();

    [Range(0, 1)]
    public float umbral = .5f;

    [GuazuTools.AttrDrawBotonPegador]
    public string origenTextura = "";
    bool descargando = false;
    float progresoDescarga = 0f;
    string estadoDescarga = "inactivo";
    public enum TipoDeEntrada
    {
        WebCam, Archivo, Url
    }
    Mat entrada;
    Texture2D texturaOrigen;
    Texture2D texturaDescargada;

    public void Actualizar()
    {
        var tresh = new Mat();
        Cv2.Threshold(entrada, tresh, umbral * 255d, 255d, ThresholdTypes.Binary);
        AlOrigenActualizado.Invoke(OpenCvSharp.Unity.MatToTexture(tresh));
    }

    private void ActualizarTexturaOrigen(Texture2D texturaInput)
    {
        ManejoDeRecursos.LiberarTextura(texturaOrigen);
        texturaOrigen = texturaInput;
        entrada = ManejoDeRecursos.Textura(texturaOrigen, entrada);
        AlOrigenActualizado.Invoke(texturaOrigen);
        Actualizar();
    }

    public void CancelarDescarga()
    {
        descargando = false;
    }
    public void CargarUrl()
    {
        if (string.IsNullOrEmpty(origenTextura))
        {
            ApuntarOrigenTextura(() => CargarUrl(origenTextura));
        }
        else CargarUrl(origenTextura);
    }
    public void CargarUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("string.IsNullOrEmpty(url)");
            return;
        }

        progresoDescarga = 0f;
        estadoDescarga = "descargando...";
        descargando = true;
        var uwr = new UnityEngine.Networking.UnityWebRequest(url, UnityEngine.Networking.UnityWebRequest.kHttpVerbGET);
        uwr.downloadHandler = new UnityEngine.Networking.DownloadHandlerTexture(true);
        EsperarUWR(uwr, () =>
        {
            descargando = false;
            if (uwr.downloadProgress == -1){
                Debug.LogError("Descarga ni siquiera iniciada");
            }
            else if (uwr.isHttpError || uwr.isNetworkError)
            {
                Debug.LogError(uwr.error + "\nPedido (GET) " + uwr.url);
                estadoDescarga = uwr.error;
            }
            else
            {
                texturaDescargada = UnityEngine.Networking.DownloadHandlerTexture.GetContent(uwr);
                ActualizarTexturaOrigen(texturaDescargada);
            }
        });

    }

    void ApuntarOrigenTextura(System.Action alTerminar)
    {
        if (Application.isEditor && !Application.isPlaying)
        {
#if UNITY_EDITOR

            alTerminar.Invoke();
#endif
        }
        else
        {
            // version runtime de esto???
            alTerminar.Invoke();
        }
    }
#if UNITY_EDITOR
    public void VentanaEditorInputTexto(System.Action<string> alInputear)
    {
        //aca una cosa magica la verda, que hace un popup e input y cuando pierde focus o haces enter
        // invoca una accion o algo asi?
        // alInputear.Invoke( input capturado )
    }
#endif

    void EsperarUWR(UnityEngine.Networking.UnityWebRequest uwr, System.Action alTerminar)
    {
        if (Application.isEditor && !Application.isPlaying)
        {
#if UNITY_EDITOR
            EsperarUWRVersionEditor(uwr, alTerminar);
#endif
        }
        else
        {
            StartCoroutine(EsperarUWRVersionRuntime(uwr, alTerminar));
        }

    }
    IEnumerator EsperarUWRVersionRuntime(UnityEngine.Networking.UnityWebRequest uwr, System.Action alTerminar)
    {
        uwr.SendWebRequest();
        while (!uwr.isDone)
        {
            progresoDescarga = uwr.downloadProgress;
            if (!descargando)
            {//descarga cancelada por otro lado
                uwr.Abort();
                break;
            }
            yield return null;
        }
        progresoDescarga = uwr.downloadProgress;
        alTerminar.Invoke();
    }
#if UNITY_EDITOR
    void EsperarUWRVersionEditor(UnityEngine.Networking.UnityWebRequest uwr, System.Action alTerminar)
    {
        EditorApplication.CallbackFunction esperarQueTermine = null;
        esperarQueTermine = () =>
        {
            progresoDescarga = uwr.downloadProgress;
            if (uwr.isDone)
            {
                EditorApplication.update -= esperarQueTermine;
                alTerminar.Invoke();
            }
            else
            {
                if (!descargando)
                {//descarga cancelada por otro lado
                    uwr.Abort();
                    EditorApplication.update -= esperarQueTermine;
                    alTerminar.Invoke();
                }
            }
        };
        EditorApplication.update += esperarQueTermine;
        uwr.SendWebRequest();
    }
#endif

#if UNITY_EDITOR
    [CustomEditor(typeof(LeerDibujoLinea))]
    public class LeerDibujoLineaEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var leerDibujoLinea = target as LeerDibujoLinea;
            if (leerDibujoLinea.descargando)
            {
                EditorGUILayout.BeginHorizontal();
                var rectProgressBar = EditorGUILayout.GetControlRect();
                EditorGUI.ProgressBar(rectProgressBar, leerDibujoLinea.progresoDescarga, leerDibujoLinea.origenTextura);
                if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                {
                    leerDibujoLinea.CancelarDescarga();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                if (GUILayout.Button("Descargar"))
                {
                    leerDibujoLinea.CargarUrl();
                }
            }
            DrawDefaultInspector();
        }
    }
#endif
}
