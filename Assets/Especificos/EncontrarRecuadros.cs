using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using System.Linq;
using Mathd = System.Math;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class EncontrarRecuadros : MonoBehaviour
{
    [Header("Descarga")]
    [GuazuTools.AttrDrawBotonPegador]
    public string origenTextura = "";
    bool descargando = false;
    float progresoDescarga = 0f;
    string estadoDescarga = "inactivo";
    public UnityEngine.UI.RawImage muestraDescargada;

    [Header("Preprocesar")]
    [Range(0.00001f, 1f)]
    public float escalaInput = .25f;
    [Ocultador("escalaInput", 1f, true)]
    public InterpolationFlags interpolacionDeEscala = InterpolationFlags.Linear;
    [Space]
    public AdaptiveThresholdTypes tipoAdaptativo = AdaptiveThresholdTypes.MeanC;
    public ThresholdTypes tipoUmbral = ThresholdTypes.BinaryInv;
    [Range(-20, 50)]
    public double constanteAdaptativo = 8d;
    [Range(0, 255)]
    public double valorNuevoDeUmbral = 255;
    [Range(3, 21)]
    public int tamBloqueAdaptativo = 11;
    public int TamBloqueAdaptativo => (tamBloqueAdaptativo < 3 ? 3 : tamBloqueAdaptativo % 2 == 0 ? tamBloqueAdaptativo + 1 : tamBloqueAdaptativo);
    public UnityEngine.UI.RawImage muestraGris, muestraEscalada, muestraAdaptativa;

    [Header("Contornos")]
    public RetrievalModes organizacionDeContornos = RetrievalModes.Tree;
    public ContourApproximationModes aproximacionContornos = ContourApproximationModes.ApproxSimple;
    public float tamMinimoDeContorno = 0.05f;
    [Range(0, 1)]
    public double umbralPorcentajeAreaIsla = .95d;
    [Space]
    public Color colorFondoContornos = Color.black;
    public Gradient coloresContornos = new Gradient();
    [Min(1)]
    public int loopColoresContornos = 5;
    public LineTypes dibujoContornos = LineTypes.AntiAlias;
    [System.Obsolete]
    public int maxContornosDibujados = 0;
    public UnityEngine.UI.RawImage muestraContornos;

    [Header("Recuadro")]
    public float toleranciaLineaRecta = 25f;
    public int selectorRecuadro = 0;
    public UnityEngine.UI.RawImage[] muestraRecuadros;
    [Space]
    public int tamRoiEsquina = 30;

    public HoughMethods houghLinesMetodo = HoughMethods.Standard;
    public int houghUmbral = 20;
    public double houghRho = 1d;
    public double houghThetaDiv = 180d;
    public double Theta { get => System.Math.PI / houghThetaDiv; }
    [Ocultador("houghLinesMetodo", (int)HoughMethods.MultiScale)]
    [Tooltip("for the multi-scale Hough transform, it is a divisor for the distance resolution rho . The coarse accumulator distance resolution is rho and the accurate accumulator resolution is rho/srn . If both srn=0 and stn=0 , the classical Hough transform is used. Otherwise, both these parameters should be positive.")]
    public double houghSrn = 1d;
    [Ocultador("houghLinesMetodo", (int)HoughMethods.MultiScale)]
    [Tooltip("for the multi-scale Hough transform, it is a divisor for the distance resolution theta.")]
    public double houghStn = 1d;
    [Ocultador("houghLinesMetodo", (int)HoughMethods.Probabilistic)]
    [Tooltip("minimum line length. Line segments shorter than that are rejected.")]
    public double houghMinLineLength = 0d;
    [Ocultador("houghLinesMetodo", (int)HoughMethods.Probabilistic)]
    [Tooltip("maximum allowed gap between points on the same line to link them.")]
    public double houghMaxLineGap = 0d;
    public Vector2 cannyPreHough = new Vector2(50, 150);
    public UnityEngine.UI.RawImage[] muestraEsquina;

    Texture2D texturaDescargada;
    Mat matProcesada, matContornosDibujados, matGrisSinEscalar, matOriginal;

    HierarchyIndex[] jerarquiaContornos;
    List<IslaContornos> islas = new List<IslaContornos>();
    List<Recuadro> recuadros = new List<Recuadro>();
    Point[][] contornos;

    class IslaContornos
    {
        double area = -1, areaHijos = -1;
        public double Area => contorno == null ? 0 : (area == -1 ? area = Cv2.ContourArea(contorno) : area);
        public double AreaHijos
        {
            get
            {
                if (hijos == null || hijos.Count == 0) return -1;
                if (areaHijos == -1)
                {
                    areaHijos = hijos.Select(e => Cv2.ContourArea(e)).Sum();
                }
                return areaHijos;
            }
        }
        public double PorcentajeAreaHijos => Area <= 0 || AreaHijos <= 0 ? 0 : AreaHijos / Area;

        public int indice;
        public Point[][] contornos;
        public HierarchyIndex[] jerarquias;
        public Point[] contorno;
        public List<Point[]> hijos = new List<Point[]>();
        public List<Point[]> hullsHijos = new List<Point[]>();
        public HierarchyIndex jerarq;
        List<int> indicesHijosRecursivo;

        public bool ConHijos => hijos == null ? false : hijos.Count > 0;

        public IslaContornos(int indice, Point[][] contornos, HierarchyIndex[] jerarquias)
        {
            this.indice = indice;
            this.jerarquias = jerarquias;
            this.contornos = contornos;
            jerarq = jerarquias[indice];
            contorno = contornos[indice];

            var indiceHijo = jerarq.Child;
            while (indiceHijo != -1)
            {
                hijos.Add(contornos[indiceHijo]);
                indiceHijo = jerarquias[indiceHijo].Next;
            }
        }
        public void GeneararHijosHulls()
        {
            hullsHijos = hijos.Select(e => Cv2.ConvexHull(e)).ToList();
        }
    }

    List<IslaContornos> LlenarIslasRecursivo(int indice, Point[][] contornos, HierarchyIndex[] jerarquias, List<IslaContornos> islas, double umbralArea = 0d)
    {
        if (islas == null) islas = new List<IslaContornos>();
        var isla = new IslaContornos(indice, contornos, jerarquiaContornos);
        if (isla.PorcentajeAreaHijos >= umbralArea)
        {
            isla.GeneararHijosHulls();
            islas.Add(isla);
        }
        if (isla.PorcentajeAreaHijos < umbralArea || umbralArea == 0)
        {
            var hijo = isla.jerarq.Child;
            while (hijo >= 0)
            {
                LlenarIslasRecursivo(hijo, contornos, jerarquias, islas, umbralArea);
                hijo = jerarquiaContornos[hijo].Next;
            }
        }
        return islas;
    }

    private void ActualizarTexturaOrigen(Texture2D texturaInput)
    {
        //descarga
        texturaDescargada = texturaInput;
        ActualizarMuestra(muestraDescargada, texturaDescargada);
        matProcesada = (matOriginal = OpenCvSharp.Unity.TextureToMat(texturaDescargada)).Clone();

        //preproceso
        if (Procesando("Gris", "Convirtiendo en gris", 1f)) return;
        Cv2.CvtColor(matProcesada, matProcesada, ColorConversionCodes.BGR2GRAY);
        ActualizarMuestra(muestraGris, matProcesada);
        matGrisSinEscalar = matProcesada.Clone();
        if (escalaInput != 1f)
        {
            if (Procesando("Escalando", $"Cambiando escala ({escalaInput})", 1f)) return;
            matProcesada = matProcesada.Resize(new Size(0, 0), escalaInput, escalaInput, interpolacionDeEscala);
            ActualizarMuestra(muestraEscalada, matProcesada);
        }
        if (Procesando("Umbral", "Generando imagen bitonal", 1f)) return;
        Cv2.AdaptiveThreshold(matProcesada, matProcesada, valorNuevoDeUmbral, tipoAdaptativo, tipoUmbral, TamBloqueAdaptativo, constanteAdaptativo);
        ActualizarMuestra(muestraAdaptativa, matProcesada);

        //contornos
        if (Procesando("Contornos", "Buscando contornos en la imagen", 1f)) return;
        Cv2.FindContours(matProcesada, out contornos, out jerarquiaContornos, organizacionDeContornos, aproximacionContornos);

        if (Procesando("Filtrando", "Filtrando islas segun area", 1f)) return;
        islas.Clear();
        recuadros.Clear();
        float tamMinimoIsla = tamMinimoDeContorno * Mathf.Min(matProcesada.Width, matProcesada.Height);
        for (int i = 0; i < contornos.Length; i++)
        {
            if (jerarquiaContornos[i].Parent == -1)
            {
                //ignorar contornos pequeños
                var tamRect = Cv2.MinAreaRect(contornos[i]).Size;
                if (tamRect.Width < tamMinimoIsla || tamRect.Height < tamMinimoIsla) continue;
                LlenarIslasRecursivo(i, contornos, jerarquiaContornos, islas, umbralPorcentajeAreaIsla);
            }
        }

        if (muestraContornos)
        {
            if (matContornosDibujados == null || matContornosDibujados.Rows != matProcesada.Rows || matContornosDibujados.Cols != matProcesada.Cols)
            {
                matContornosDibujados = new Mat(matProcesada.Rows, matProcesada.Cols, MatType.CV_8UC4);
            }
            if (Procesando("Fondo", "Dibujando fondo de muestra de contornos", 1f)) return;

            var colScalar = new Scalar(colorFondoContornos.b * 255, colorFondoContornos.g * 255, colorFondoContornos.r * 255, colorFondoContornos.a * 255);
            // matContornosDibujados.SetTo(colScalar);
            matContornosDibujados = OpenCvSharp.Unity.TextureToMat(texturaDescargada).Resize(new Size(0, 0), escalaInput, escalaInput, interpolacionDeEscala);
        }

        foreach (var isla in islas)
        {
            var contornosDibujar = isla.hullsHijos;
            var grupoDeRecuadros = new List<Recuadro>();
            for (int i = 0; i < contornosDibujar.Count; i++)
            {
                var colContorno = coloresContornos.Evaluate((i % loopColoresContornos) / (float)loopColoresContornos);

                if (contornosDibujar[i].Length > 4)
                {
                    if (Procesando("Recuadro", $"Generando recuadro: {i + 1}/{contornosDibujar.Count}", (i + 1f) / contornosDibujar.Count)) return;
                    var recuadro = new Recuadro(contornosDibujar[i], toleranciaLineaRecta);
                    grupoDeRecuadros.Add(recuadro);
                    //deberia funcionar porque es un puntero a la lista (no una copia/foto actual)
                    recuadro.GrupoDeRecuadros = grupoDeRecuadros;
                    if (muestraContornos) recuadro.DibujarDebug(matContornosDibujados, colContorno);

                }
            }
            recuadros.AddRange(grupoDeRecuadros);

        }
        ActualizarMuestra(muestraContornos, matContornosDibujados);

        if (recuadros.Count > 0)
        {
            for (int i = 0; i < recuadros.Count; i++)
            {
                if (Procesando("Marca", $"Buscando marca diagonal de aspect de {i}", i / (float)recuadros.Count)) return;
                var imgEsquina = muestraEsquina != null && muestraEsquina.Length > i ? muestraEsquina[i] : null;
                var matEsquina = CalcularRoisYLineas(i, imgEsquina);
                if (imgEsquina)
                {
                    imgEsquina.enabled = matEsquina != null;
                    ActualizarMuestra(imgEsquina, matEsquina);
                }
            }
            for (int i = 0; i < recuadros.Count; i++)
            {
                if (Procesando("Calculando", $"Calculando corte con marca diagonal y aspect ratio de {i}", i / (float)recuadros.Count)) return;
                recuadros[i].BuscarInterseccionConMarcaDiagonal();
            }
            for (int i = 0; i < recuadros.Count; i++)
            {
                if (Procesando("Normalizando", $"Normalizando recuadro de {i}", i / (float)recuadros.Count)) return;
                recuadros[i].BuscarInterseccionConMarcaDiagonal();
                var matNormalizado = recuadros[i].Normalizar(matOriginal, 1f / escalaInput);

                if (muestraRecuadros != null && muestraRecuadros.Length > i)
                {
                    muestraRecuadros[i].enabled = matNormalizado != null;
                    ActualizarMuestra(muestraRecuadros[i], matNormalizado);
                }

            }
        }

        // if (muestraRecuadro && recuadros.Count > 0)
        // {
        //     var matResultante = CalcularRoisYLineas();
        //     if (matResultante != null) ProcesarMarcaDiagonalYMostrarDebug(matResultante);
        //     if (Procesando("Normalizando", "Normalizando recuadro", 1f)) return;
        //     ActualizarMuestra(muestraRecuadro, recuadros[selectorRecuadro % recuadros.Count].Normalizar(matOriginal, 1f / escalaInput));
        // }

        Procesando(null);
    }

    Mat CalcularRoisYLineas(int indice = -1, bool dibujarDebug = false)
    {
        indice = selectorRecuadro > 0 ? selectorRecuadro % recuadros.Count : 0;
        var rec = recuadros[selectorRecuadro % recuadros.Count];
        Procesando("Marca", $"Buscando marca diagonal de aspect de {indice}");
        return CalcularRoisYLineas(rec, dibujarDebug);
    }
    Mat CalcularRoisYLineas(Recuadro rec, bool dibujarDebug = false)
    {
        return rec.BuscarMarcaDiagonal(
            matGrisSinEscalar, tamRoiEsquina, escalaInput, toleranciaLineaRecta, dibujarDebug,
            new Recuadro.ConfigFiltroEsquina()
            {
                cannyUmbralMenor = cannyPreHough.x,
                cannyUmbralMayor = cannyPreHough.y,
                houghRho = houghRho,
                houghThetaDiv = houghThetaDiv,
                houghUmbral = houghUmbral,
                colDiagonal = coloresContornos.Evaluate(0)
            });
    }
    void ProcesarMarcaDiagonalYMostrarDebug(Mat matResultante, int indice = -1, UnityEngine.UI.RawImage muestraEsquina = null)
    {
        indice = selectorRecuadro > 0 ? selectorRecuadro % recuadros.Count : 0;
        var rec = recuadros[selectorRecuadro % recuadros.Count];

        var punto = rec.BuscarInterseccionConMarcaDiagonal();
        ActualizarMuestra(muestraEsquina, matResultante);
        if (muestraContornos)
        {
            Cv2.CvtColor(matProcesada, matProcesada, ColorConversionCodes.GRAY2BGR);
            Cv2.Circle(matProcesada, punto, 10, new Scalar(255, 100, 0), 1);
            var offp = new Point(Mathd.Cos(rec.thetaDiagonal) * 1000, Mathd.Sin(rec.thetaDiagonal) * 1000);
            Cv2.Line(matProcesada, rec.PuntoMarca + offp, rec.PuntoMarca - offp, new Scalar(255, 0, 100), 2);

            offp = rec.verticesCuadrilatero[(rec.ladoDiagonalCortado + 3) % rec.verticesCuadrilatero.Count];
            Cv2.Circle(matProcesada, offp, 5, new Scalar(155, 50, 255), 1);

            ActualizarMuestra(muestraContornos, matProcesada);
        }
    }

    bool Procesando(string titulo, string info = "", float progreso = 0f)
    {
#if UNITY_EDITOR
        if (titulo == null)
        {
            EditorUtility.ClearProgressBar();
            EditorWindow view = EditorWindow.GetWindow<SceneView>();
            if (view) view.Repaint();
            Canvas.ForceUpdateCanvases();
            return false;
        }
        if (EditorUtility.DisplayCancelableProgressBar(titulo, info, progreso))
        {
            EditorUtility.ClearProgressBar();
            return true;
        }
#endif
        return false;
    }

    public void ActualizarMuestra(UnityEngine.UI.RawImage rawImage, Mat mat)
    {
        if (rawImage && mat != null)
        {
            var size = mat.Size();

            Texture2D textura = rawImage.texture as Texture2D;
            if (!textura || !textura.isReadable)
            {
                textura = new Texture2D(size.Width, size.Height);
                textura.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            }

            if (textura.width != size.Width || textura.height != size.Height) textura.Resize(size.Width, size.Height);
            OpenCvSharp.Unity.MatToTexture(mat, textura);
            ActualizarMuestra(rawImage, textura);
        }
    }
    public void ActualizarMuestra(UnityEngine.UI.RawImage rawImage, Texture2D textura)
    {
        if (rawImage && textura != null)
        {
            if (rawImage.texture != textura) rawImage.texture = textura;
            var aspect = rawImage.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            if (aspect) aspect.aspectRatio = textura.width / (float)textura.height;
        }
    }

    public void CancelarDescarga() => descargando = false;
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

    // Esto es llamado cuando el URL es null, entonces hace un USER PROMPT para pegar la URL
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
    [ContextMenu("Select Texturas Libres")]
    private void SelectTexturas()
    {
        Selection.objects = FindObjectsOfType<Texture>();
    }
    [MenuItem("Guazu/Clear Progress Bar")]
    public static void ClearProgressBar()
    {
        EditorUtility.ClearProgressBar();
    }

    [CustomEditor(typeof(EncontrarRecuadros))]
    public class EncontrarRecuadrosEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var encontrador = target as EncontrarRecuadros;
            if (encontrador.descargando)
            {
                EditorGUILayout.BeginHorizontal();
                var rectProgressBar = EditorGUILayout.GetControlRect();
                EditorGUI.ProgressBar(rectProgressBar, encontrador.progresoDescarga, encontrador.origenTextura);
                if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                {
                    encontrador.CancelarDescarga();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                if (GUILayout.Button("Descargar"))
                {
                    encontrador.CargarUrl();
                }
            }
            EditorGUILayout.ObjectField(encontrador.texturaDescargada, typeof(Texture2D), true);
            EditorGUI.BeginDisabledGroup(encontrador.descargando ||
                (encontrador.texturaDescargada == null &&
                !(encontrador.muestraDescargada ? encontrador.muestraDescargada.texture : null)));
            if (GUILayout.Button("Reprocesar"))
            {
                try
                {
                    encontrador.ActualizarTexturaOrigen(encontrador.texturaDescargada ?
                    encontrador.texturaDescargada : (encontrador.muestraDescargada.texture as Texture2D));
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                    EditorUtility.ClearProgressBar();
                }
            }
            EditorGUI.EndDisabledGroup();
            DrawDefaultInspector();
        }
    }
#endif
}