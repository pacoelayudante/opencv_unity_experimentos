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
    readonly static Scalar ColEscalarNegro = new Scalar();
    readonly static Scalar ColEscalarRojo = new Scalar(0, 0, 255);

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
    RetrievalModes organizacionDeContornos = RetrievalModes.Tree;
    public ContourApproximationModes aproximacionContornos = ContourApproximationModes.ApproxSimple;
    public float tamMinimoDeContorno = 0.05f;
    [Range(0, 1)]
    public double umbralPorcentajeAreaIsla = .85d;
    [Space]
    public Color colorFondoContornos = Color.black;
    public Gradient coloresContornos = new Gradient();
    [Min(1)]
    public int loopColoresContornos = 5;
    public LineTypes dibujoContornos = LineTypes.AntiAlias;
    public UnityEngine.UI.RawImage muestraContornos;

    [Header("Recuadro")]
    public int selectorRecuadro = 0;
    [Tooltip("Este coso no esta funcionando, y no estoy tan seguro si tenia sentido o no")]
    public bool diagonalInteresccion = false;
    public float toleranciaLineaRecta = 15f;
    [Space]
    public int tamRoiEsquina = 128;

    HoughMethods houghLinesMetodo = HoughMethods.Standard;
    public int houghUmbral = 30;
    public double houghRho = 1d;
    public double houghThetaDiv = 360d;
    public double Theta { get => System.Math.PI / houghThetaDiv; }
    public Vector2 cannyPreHough = new Vector2(100, 200);
    [Space]
    public UnityEngine.UI.RawImage[] muestraEsquina;
    public UnityEngine.UI.RawImage[] muestraRecuadros;

    Texture2D texturaDescargada;
    public Mat matProcesada, matUmbralEscalado, matContornosDibujados, matGrisSinEscalar, matOriginal;

    [System.NonSerialized]
    public HierarchyIndex[] jerarquiaContornos;
    [System.NonSerialized]
    public Point[][] contornos;
    List<IslaContornos> islas = new List<IslaContornos>();
    [System.NonSerialized]
    public List<Recuadro> recuadros = new List<Recuadro>();

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
        matGrisSinEscalar = matProcesada.Clone();
        // ActualizarMuestra(muestraGris, matGrisSinEscalar);
        if (escalaInput != 1f)
        {
            if (Procesando("Escalando", $"Cambiando escala ({escalaInput})", 1f)) return;
            matProcesada = matProcesada.Resize(new Size(0, 0), escalaInput, escalaInput, interpolacionDeEscala);
            ActualizarMuestra(muestraEscalada, matProcesada);
        }
        if (Procesando("Umbral", "Generando imagen bitonal", 1f)) return;
        Cv2.AdaptiveThreshold(matProcesada, matProcesada, valorNuevoDeUmbral, tipoAdaptativo, tipoUmbral, TamBloqueAdaptativo, constanteAdaptativo);
        matUmbralEscalado = matProcesada.Clone();
        // ActualizarMuestra(muestraAdaptativa, matProcesada);

        //contornos
        if (Procesando("Contornos", "Buscando contornos en la imagen", 1f)) return;
        Cv2.FindContours(matProcesada, out contornos, out jerarquiaContornos, organizacionDeContornos, aproximacionContornos);

        float tamMinimoIsla = tamMinimoDeContorno * Mathf.Min(matProcesada.Width, matProcesada.Height);

        if (muestraAdaptativa)
        {
            Cv2.CvtColor(matProcesada, matProcesada, ColorConversionCodes.GRAY2BGR);
            for (int i = 0; i < contornos.Length; i++)
            {
                if (jerarquiaContornos[i].Parent == -1)
                {
                    var tamRect = Cv2.MinAreaRect(contornos[i]).Size;
                    if (tamRect.Width < tamMinimoIsla || tamRect.Height < tamMinimoIsla) continue;
                    var col = coloresContornos.Evaluate((i % loopColoresContornos) / (float)loopColoresContornos);
                    var colEsc = new Scalar(col.b * 255, col.g * 255, col.r * 255);
                    Cv2.DrawContours(matProcesada, contornos, i, colEsc);
                }
            }
            ActualizarMuestra(muestraAdaptativa, matProcesada);
        }

        if (Procesando("Filtrando", "Filtrando islas segun area", 1f)) return;
        islas.Clear();
        recuadros.Clear();
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
            matContornosDibujados = matOriginal.Clone().Resize(new Size(0, 0), escalaInput, escalaInput, interpolacionDeEscala);
        }

        foreach (var isla in islas)
        {
            var contornosDibujar = isla.hullsHijos;
            var grupoDeRecuadros = new List<Recuadro>();
            for (int i = 0; i < contornosDibujar.Count; i++)
            {
                var tamRect = Cv2.MinAreaRect(contornosDibujar[i]).Size;
                if (tamRect.Width < tamMinimoIsla || tamRect.Height < tamMinimoIsla) continue;

                var colContorno = coloresContornos.Evaluate((i % loopColoresContornos) / (float)loopColoresContornos);

                // Cv2.FillPoly(matGrisSinEscalar,recuadro.verticesCuadrilatero.Select(p=>new Point(p.X,p.Y)),ColEscalarNegro);

                if (contornosDibujar[i].Length > 4)
                {
                    if (Procesando("Recuadro", $"Generando recuadro: {i + 1}/{contornosDibujar.Count}", (i + 1f) / contornosDibujar.Count)) return;
                    var recuadro = new Recuadro(System.Array.IndexOf(contornos,isla.hijos[i]),contornosDibujar[i], toleranciaLineaRecta);
                    grupoDeRecuadros.Add(recuadro);
                    //deberia funcionar porque es un puntero a la lista (no una copia/foto actual)
                    recuadro.GrupoDeRecuadros = grupoDeRecuadros;
                    if (muestraContornos) recuadro.DibujarDebug(matContornosDibujados, colContorno);

                    Cv2.FillConvexPoly(matGrisSinEscalar, recuadro.verticesCuadrilatero.Select(p => new Point(p.X / escalaInput, p.Y / escalaInput)), ColEscalarNegro);

                }
            }
            recuadros.AddRange(grupoDeRecuadros);

        }
        ActualizarMuestra(muestraGris, matGrisSinEscalar);
        // ActualizarMuestra(muestraContornos, matContornosDibujados);

        if (recuadros.Count > 0)
        {
            for (int i = 0; diagonalInteresccion && i < recuadros.Count; i++)
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
            for (int i = 0; diagonalInteresccion && i < recuadros.Count; i++)
            {
                if (Procesando("Calculando", $"Calculando corte con marca diagonal y aspect ratio de {i}", i / (float)recuadros.Count)) return;
                recuadros[i].BuscarInterseccionConMarcaDiagonal(matContornosDibujados,ColEscalarRojo);
            }
            for (int i = 0; i < recuadros.Count; i++)
            {
                if (Procesando("Normalizando", $"Normalizando recuadro de {i}", i / (float)recuadros.Count)) return;
                var matNormalizado = recuadros[i].Normalizar(matOriginal, 1f / escalaInput);

                if (muestraRecuadros != null && muestraRecuadros.Length > i)
                {
                    muestraRecuadros[i].enabled = matNormalizado != null;
                    if (matNormalizado != null && diagonalInteresccion)
                    {
                        Point p1, p2;
                        var diags = new Line2D[]{
                            new Line2D(0.5,0.5,0,0),new Line2D(0.5,-0.5,0,matNormalizado.Height),
                            new Line2D(-0.5,0.5,matNormalizado.Width,0),new Line2D(-0.5,-0.5,matNormalizado.Width,matNormalizado.Height),
                        };
                        foreach (var diag45 in diags)
                        {
                            diag45.FitSize(matNormalizado.Width, matNormalizado.Height, out p1, out p2);
                            Cv2.Line(matNormalizado, p1, p2, ColEscalarRojo);
                        }
                    }
                    ActualizarMuestra(muestraRecuadros[i], matNormalizado);
                }

            }
        }
        ActualizarMuestra(muestraContornos, matContornosDibujados);

        Procesando(null);
    }

    Mat CalcularRoisYLineas(int indice = -1, bool dibujarDebug = false)
    {
        if (indice == -1) indice = selectorRecuadro > 0 ? selectorRecuadro % recuadros.Count : 0;
        var rec = recuadros[indice % recuadros.Count];
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