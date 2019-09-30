using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WebRequestToMat : MonoBehaviour, IEsCable, IContieneTextura
{
    [GuazuTools.AttrDrawBotonPegador]
    public string origenTextura = "";
    bool descargando = false;
    float progresoDescarga = 0f;
    string estadoDescarga = "inactivo";
    public UnityEngine.UI.RawImage muestraDescargada;

    Texture2D texturaDescargada;
    Mat matSalida;

    System.Action<IEsCable> alActualizar;

    /*
    Micro cosas genericas
     */
    // IEsCable
    public List<Mat> MatsOut() => null;
    public Mat MatOut() => matSalida == null && texturaDescargada!=null ? matSalida = ManejoDeRecursos.Textura(texturaDescargada, matSalida) : matSalida;
    public System.Action<IEsCable> Suscribir(System.Action<IEsCable> accion, bool suscribir = true)
    {
        alActualizar = alActualizar.SuscribirExt(accion, suscribir);
        return accion;
    }
    //IContieneTextura
    public Texture2D Textura() => texturaDescargada;
    /*
    END microcosas genericas
     */

    private void ActualizarTexturaOrigen(Texture2D texturaInput)
    {
        ManejoDeRecursos.LiberarTextura(texturaDescargada);
        texturaDescargada = texturaInput;
        if (muestraDescargada)
        {
            muestraDescargada.texture = texturaDescargada;
            var aspect = muestraDescargada.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            if (aspect) aspect.aspectRatio = texturaDescargada.width / (float)texturaDescargada.height;
        }
        matSalida = ManejoDeRecursos.Textura(texturaDescargada, matSalida);
        if (alActualizar != null) alActualizar.Invoke(this);
    }

    public void CancelarDescarga()
    {
        descargando = false;
    }
    public void CargarUrl(System.Action alCompletar = null)
    {
        if (string.IsNullOrEmpty(origenTextura))
        {
            ApuntarOrigenTextura(() => CargarUrl(origenTextura));
        }
        else CargarUrl(origenTextura);
    }
    public void CargarUrl(string url, System.Action alCompletar = null)
    {
        if (descargando)
        {
            Debug.LogError("ya estoy descargando");
        }
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
            if (uwr.downloadProgress == -1)
            {
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
                if (alCompletar != null) alCompletar.Invoke();
            }
        });

    }

    void ApuntarOrigenTextura(System.Action alTerminar)
    {
        if (Application.isEditor && !Application.isPlaying)
        {
#if UNITY_EDITOR
            //VentanaEditorInputTexto I guess?
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
    [CustomEditor(typeof(WebRequestToMat))]
    public class WebRequestToMatEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var webRequest = target as WebRequestToMat;
            if (webRequest.descargando)
            {
                EditorGUILayout.BeginHorizontal();
                var rectProgressBar = EditorGUILayout.GetControlRect();
                EditorGUI.ProgressBar(rectProgressBar, webRequest.progresoDescarga, webRequest.origenTextura);
                if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                {
                    webRequest.CancelarDescarga();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                if (GUILayout.Button("Descargar"))
                {
                    webRequest.CargarUrl();
                }
            }
            EditorGUILayout.ObjectField(webRequest.texturaDescargada, typeof(Texture2D), true);
            var mat = webRequest.matSalida;
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
