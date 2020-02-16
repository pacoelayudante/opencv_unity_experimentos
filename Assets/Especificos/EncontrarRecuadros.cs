using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using System.Linq;
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
    [Range(0, 1)]
    public double umbralPorcentajeAreaIsla = .95d;
    [Space]
    public Color colorFondoContornos = Color.black;
    public Gradient coloresContornos = new Gradient();
    [Min(1)]
    public int loopColoresContornos = 5;
    public LineTypes dibujoContornos = LineTypes.AntiAlias;
    public int maxContornosDibujados = 0;
    public UnityEngine.UI.RawImage muestraContornos;

    [Header("Recuadro")]
    public float toleranciaLineaRecta = 25f;

    Texture2D texturaDescargada;
    Mat matProcesada, matContornosDibujados, matGrisEscalada;

    HierarchyIndex[] jerarquiaContornos;
    List<IslaContornos> islas = new List<IslaContornos>();
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
                    // areaHijos = hijos.Aggregate(0d, (cant, conto) => Cv2.ContourArea(conto) + cant, res => res);
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

    class Recuadro {
        Point[] contorno;
        double[] distanciasContornoOriginal;
        float[] angulosOriginal;
        double perimetroOriginal;
        Point[] encuadre = new Point[4];

        public Recuadro(int indice, Point[][] contornosOriginal, float toleranciaLineaRecta):this(indice,contornosOriginal,toleranciaLineaRecta,Color.red){}
        public Recuadro(int indice, Point[][] contornosOriginal, float toleranciaLineaRecta, Color color) {
            contorno = contornosOriginal[indice];
            distanciasContornoOriginal = contorno.Select((e, index) => Point.Distance(e, contorno[(index + 1) % contorno.Length])).ToArray();
            angulosOriginal = contorno.Select((e, index) =>
                Mathf.Rad2Deg*Mathf.Atan2(e.Y - contorno[(index + 1) % contorno.Length].Y, e.X - contorno[(index + 1) % contorno.Length].X) )
                .ToArray();
            perimetroOriginal = distanciasContornoOriginal.Sum();
            
            var distancias = distanciasContornoOriginal;
            var angulos = angulosOriginal;
                    var lineas = new List<LineSegmentPoint>();
                    Point puntoAPos = contorno[0];
                    var distSumada = distancias[0];
                    var anguloActual = angulos[0];
                    int conter = 0;
                    for (int j = 1; j < distancias.Length + 1; j++)
                    {
                        if (Mathf.Abs(Mathf.DeltaAngle(angulos[j % distancias.Length], anguloActual)) <= toleranciaLineaRecta)
                        {
                            distSumada += distancias[j % distancias.Length];
                        }
                        else
                        {
                            // if (distSumada >= minLadoElipse / 4d)
                            {
                                // Cv2.Line(matContornosDibujados, puntoAPos, contorno[j%distancias.Length], colScalar);
                                lineas.Add(new LineSegmentPoint(puntoAPos, contorno[j % distancias.Length]));
                                conter++;
                            }
                            puntoAPos = contorno[j % distancias.Length];
                            anguloActual = angulos[j % distancias.Length];
                            distSumada = distancias[j % distancias.Length];
                        }
                    }
                    lineas.OrderByDescending(lin=>lin.P1.DistanceTo(lin.P2)).Take(4).ToArray();
                    List<Point> polis = new List<Point>();
                    
                    var colEscalar = new Scalar(color.b,color.g,color.r,color.a);
                    for (int j = 0; j < lineas.Count; j++)
                    {
                        Point? interx = lineas[j].LineIntersection(lineas[(j + 1) % lineas.Count]);
                        if (interx.HasValue)
                        {
                            // Cv2.Circle(matContornosDibujados,interx.Value,10,colScalar);
                            polis.Add(interx.Value);
                        }
                    }
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
        matProcesada = OpenCvSharp.Unity.TextureToMat(texturaDescargada);

        //preproceso
        if (Procesando("Gris", "Convirtiendo en gris", 1f)) return;
        Cv2.CvtColor(matProcesada, matProcesada, ColorConversionCodes.BGR2GRAY);
        ActualizarMuestra(muestraGris, matProcesada);
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
        for (int i = 0; i < contornos.Length; i++)
        {
            if (jerarquiaContornos[i].Parent == -1)
            {
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

            var contornosDibujar = islas.SelectMany(e => e.hullsHijos).ToArray();
            if (maxContornosDibujados > 0)
            {
                if (Procesando("Ordenando", "Ordenando contornos por area", 1f)) return;
                contornosDibujar = contornosDibujar.OrderByDescending(e => Cv2.ContourArea(e)).ToArray();
            }
            for (int i = 0; i < contornosDibujar.Length && (i < maxContornosDibujados || maxContornosDibujados <= 0); i++)
            {
                var colContorno = coloresContornos.Evaluate((i % loopColoresContornos) / (float)loopColoresContornos);
                colScalar = new Scalar(colContorno.b * 255, colContorno.g * 255, colContorno.r * 255, colContorno.a * 255);
                if (Procesando("Dibujando", $"Dibujando el contorno {i}", i / (float)contornosDibujar.Length)) return;
                // Cv2.DrawContours(matContornosDibujados, contornosDibujar, i, colScalar, 1, dibujoContornos);
                // Cv2.FillConvexPoly(matContornosDibujados, contornosDibujar[i], colScalar, dibujoContornos);

                if (contornosDibujar[i].Length > 4)
                {
                    colScalar = new Scalar(0 * 255, 1 * 255, 0 * 255, colContorno.a * 255);
                    var elipse = Cv2.FitEllipse(contornosDibujar[i]);
                    var minLadoElipse = Mathf.Min(elipse.Size.Width, elipse.Size.Height);
                    // Cv2.Ellipse(matContornosDibujados, elipse, colScalar);

                    var contorno = contornosDibujar[i];
                    var distancias = contorno.Select((e, index) => Point.Distance(e, contorno[(index + 1) % contorno.Length])).ToArray();
                    var angulos = contorno.Select((e, index) =>
                        Mathf.Rad2Deg * Mathf.Atan2(e.Y - contorno[(index + 1) % contorno.Length].Y, e.X - contorno[(index + 1) % contorno.Length].X))
                        .ToArray();
                    double perimetro = distancias.Sum();

                    var lineas = new List<LineSegmentPoint>();
                    Point puntoAPos = contorno[0];
                    var distSumada = distancias[0];
                    var anguloActual = angulos[0];
                    int conter = 0;
                    for (int j = 1; j < distancias.Length + 1; j++)
                    {
                        colContorno = coloresContornos.Evaluate((conter % loopColoresContornos) / (float)loopColoresContornos);
                        colScalar = new Scalar(colContorno.b * 255, colContorno.g * 255, colContorno.r * 255, colContorno.a * 255);

                        if (Mathf.Abs(Mathf.DeltaAngle(angulos[j % distancias.Length], anguloActual)) < toleranciaLineaRecta)
                        {
                            distSumada += distancias[j % distancias.Length];
                        }
                        else
                        {
                            // if (distSumada >= minLadoElipse / 4d)
                            {
                                Cv2.Line(matContornosDibujados, puntoAPos, contorno[j%distancias.Length], colScalar);
                                lineas.Add(new LineSegmentPoint(puntoAPos, contorno[j % distancias.Length]));
                                conter++;
                            }
                            puntoAPos = contorno[j % distancias.Length];
                            anguloActual = angulos[j % distancias.Length];
                            distSumada = distancias[j % distancias.Length];
                        }
                    }
                    
                    colContorno = coloresContornos.Evaluate((i % loopColoresContornos) / (float)loopColoresContornos);
                    colScalar = new Scalar(colContorno.b * 255, colContorno.g * 255, colContorno.r * 255, colContorno.a * 50);

                    lineas=lineas.Select((lin,index)=>new {index=index,lin=lin})
                        .OrderByDescending(lin=>lin.lin.P1.DistanceTo(lin.lin.P2)).Take(4)
                        .OrderBy(lin=>lin.index).Select(lin=>lin.lin).ToList();
                    foreach(var lin in lineas) {                        
                        Cv2.Line(matContornosDibujados, lin.P1,lin.P2, colScalar,2);
                    }

                    List<Point> polis = new List<Point>();
                    for (int j = 0; j < lineas.Count; j++)
                    {
                        Point? interx = lineas[j].LineIntersection(lineas[(j + 1) % lineas.Count]);
                        if (interx.HasValue)
                        {
                            Cv2.Circle(matContornosDibujados,interx.Value,10,colScalar);
                            polis.Add(interx.Value);
                        }
                    }
                    // if (polis.Count == 4) Cv2.FillConvexPoly(matContornosDibujados, polis, colScalar, dibujoContornos);
                    
                }
            }
            ActualizarMuestra(muestraContornos, matContornosDibujados);
        }

        Procesando(null);
    }

    bool Procesando(string titulo, string info = "", float progreso = 0f)
    {
#if UNITY_EDITOR
        if (titulo == null)
        {
            EditorUtility.ClearProgressBar();
            EditorWindow view = EditorWindow.GetWindow<SceneView>();
            if(view) view.Repaint();
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
     private void SelectTexturas() {
        Selection.objects = FindObjectsOfType<Texture>();
    }

    [CustomEditor(typeof(EncontrarRecuadros))]
    public class EncontrarRecuadrosEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
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
        }
    }
#endif
}