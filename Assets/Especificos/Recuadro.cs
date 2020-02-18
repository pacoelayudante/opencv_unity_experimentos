using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using System.Linq;
using Mathd = System.Math;
#if UNITY_EDITOR
using UnityEditor;
#endif

class Recuadro
{
    Point[] contornoOriginal;
    double[] distanciasContornoOriginal;
    float[] angulosOriginal;
    double perimetroOriginal;
    Point[] encuadre = new Point[4];
    public List<LineSegmentPoint> ladosCuadrilatero;
    public List<double[]> thetasDeVertices;
    public List<Point2f> verticesCuadrilatero;
    List<Recuadro> grupoDeRecuadros;
    double anchoSupuesto, altoSupuesto;
    List<Point2f> verticesNormalizados;

    public double thetaDiagonal;
    public int indexVerticeMarcaDiagonal = -1;
    public Point PuntoMarca => verticesCuadrilatero[indexVerticeMarcaDiagonal];
    public int ladoDiagonalCortado = -1;
    Point puntoCorteDiagonal;
    Mat matRecuadroNormalizado;

    public List<Recuadro> GrupoDeRecuadros
    {
        get => grupoDeRecuadros;
        set
        {
            UnityEngine.Assertions.Assert.IsNull(grupoDeRecuadros);
            grupoDeRecuadros = value;
        }
    }

    public Recuadro(Point[] contorno, float toleranciaLineaRecta)
    {
        contornoOriginal = contorno;
        distanciasContornoOriginal = contornoOriginal.Select((e, index) => Point.Distance(e, contornoOriginal[(index + 1) % contornoOriginal.Length])).ToArray();
        angulosOriginal = contornoOriginal.Select((e, index) =>
            Mathf.Rad2Deg * Mathf.Atan2(e.Y - contornoOriginal[(index + 1) % contornoOriginal.Length].Y, e.X - contornoOriginal[(index + 1) % contornoOriginal.Length].X))
            .ToArray();
        perimetroOriginal = distanciasContornoOriginal.Sum();

        var distancias = distanciasContornoOriginal;
        var angulos = angulosOriginal;
        ladosCuadrilatero = new List<LineSegmentPoint>();
        Point puntoAPos = contornoOriginal[0];
        var distSumada = distancias[0];
        var anguloActual = angulos[0];

        for (int j = 1; j < distancias.Length + 1; j++)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(angulos[j % distancias.Length], anguloActual)) <= toleranciaLineaRecta)
            {
                distSumada += distancias[j % distancias.Length];
            }
            else
            {
                ladosCuadrilatero.Add(new LineSegmentPoint(puntoAPos, contornoOriginal[j % distancias.Length]));

                puntoAPos = contornoOriginal[j % distancias.Length];
                anguloActual = angulos[j % distancias.Length];
                distSumada = distancias[j % distancias.Length];
            }
        }

        ladosCuadrilatero = ladosCuadrilatero.Select((lin, index) => new { index = index, lin = lin })
            .OrderByDescending(lin => lin.lin.P1.DistanceTo(lin.lin.P2)).Take(4)
            .OrderBy(lin => lin.index).Select(lin => lin.lin).ToList();
        verticesCuadrilatero = new List<Point2f>();
        thetasDeVertices = new List<double[]>();

        for (int j = 0; j < ladosCuadrilatero.Count; j++)
        {
            int j2 = (j + 1) % ladosCuadrilatero.Count;
            thetasDeVertices.Add(new double[]{
                    Mathf.Atan2(ladosCuadrilatero[j].P1.Y-ladosCuadrilatero[j].P2.Y,ladosCuadrilatero[j].P1.X-ladosCuadrilatero[j].P2.X),
                    Mathf.Atan2(ladosCuadrilatero[j2].P2.Y-ladosCuadrilatero[j2].P1.Y,ladosCuadrilatero[j2].P2.X-ladosCuadrilatero[j2].P1.X)
                });
            Point? interx = ladosCuadrilatero[j].LineIntersection(ladosCuadrilatero[j2]);
            if (interx.HasValue)
            {
                verticesCuadrilatero.Add(interx.Value);
            }
        }

        UnityEngine.Assertions.Assert.IsTrue(ladosCuadrilatero.Count == 4);
        UnityEngine.Assertions.Assert.IsTrue(verticesCuadrilatero.Count == 4);
        if (verticesCuadrilatero.Count == 4)
        {
            anchoSupuesto = Mathd.Max(
                verticesCuadrilatero[0].DistanceTo(verticesCuadrilatero[1])
                , verticesCuadrilatero[2].DistanceTo(verticesCuadrilatero[3]));
            altoSupuesto = Mathd.Max(
                verticesCuadrilatero[1].DistanceTo(verticesCuadrilatero[2])
                , verticesCuadrilatero[3].DistanceTo(verticesCuadrilatero[0]));

            verticesNormalizados = new List<Point2f>() {
                    new Point2f(0,0),new Point2f((float)anchoSupuesto,0),
                    new Point2f((float)anchoSupuesto,(float)altoSupuesto),new Point2f(0,(float)altoSupuesto),
                };
        }
    }

    public OpenCvSharp.Rect GetRoi(int indiceVertice, int tam = 30, float escalaInput = 1f)
    {
        return new OpenCvSharp.Rect(
            Mathf.FloorToInt(verticesCuadrilatero[indiceVertice].X / escalaInput - tam / 2),
            Mathf.FloorToInt(verticesCuadrilatero[indiceVertice].Y / escalaInput - tam / 2),
            tam, tam);
    }

    public Point BuscarInterseccionConMarcaDiagonal()
    {
        if (indexVerticeMarcaDiagonal == -1)
        {
            foreach (var vecino in grupoDeRecuadros)
            {
                if (vecino.indexVerticeMarcaDiagonal != -1)
                {
                    this.indexVerticeMarcaDiagonal = vecino.indexVerticeMarcaDiagonal;
                    this.thetaDiagonal = vecino.thetaDiagonal;
                    break;
                }
            }
        }
        if (indexVerticeMarcaDiagonal != -1)
        {
            var pMarca = new Point(verticesCuadrilatero[indexVerticeMarcaDiagonal].X, verticesCuadrilatero[indexVerticeMarcaDiagonal].Y);
            var diagOffPos = new Point(Mathd.Cos(thetaDiagonal) * 100d, Mathd.Sin(thetaDiagonal) * 100d);
            var diagonal = new LineSegmentPoint(pMarca - diagOffPos, pMarca + diagOffPos);
            var l1 = ladosCuadrilatero[(indexVerticeMarcaDiagonal + 2) % ladosCuadrilatero.Count];
            var l2 = ladosCuadrilatero[(indexVerticeMarcaDiagonal + 3) % ladosCuadrilatero.Count];
            Point? interx1 = diagonal.LineIntersection(l1);
            Point? interx2 = diagonal.LineIntersection(l2);
            int resultado = -1;
            if (interx1.HasValue ^ interx2.HasValue)
            {
                if (interx1.HasValue)
                {
                    resultado = 0;
                }
                else
                {
                    resultado = 1;
                }
            }
            else if (interx1.HasValue)
            {
                resultado = interx1.Value.DistanceTo(pMarca) < interx2.Value.DistanceTo(pMarca) ? 0 : 1;
            }
            if (resultado != -1)
            {
                if (resultado == 0)
                {
                    puntoCorteDiagonal = interx1.Value;
                    ladoDiagonalCortado = (indexVerticeMarcaDiagonal + 2) % ladosCuadrilatero.Count;
                }
                else
                {
                    puntoCorteDiagonal = interx2.Value;
                    ladoDiagonalCortado = (indexVerticeMarcaDiagonal + 3) % ladosCuadrilatero.Count;
                }

                double aspect = ladosCuadrilatero[ladoDiagonalCortado].Length(ladosCuadrilatero[ladoDiagonalCortado]) /
                    puntoCorteDiagonal.DistanceTo(ladosCuadrilatero[ladoDiagonalCortado].P1);
                if (resultado == 0) anchoSupuesto = altoSupuesto / aspect;
                else altoSupuesto = anchoSupuesto * aspect;
                verticesNormalizados = new List<Point2f>() {
                    new Point2f(0,0),new Point2f((float)anchoSupuesto,0),
                    new Point2f((float)anchoSupuesto,(float)altoSupuesto),new Point2f(0,(float)altoSupuesto),
                };

            }
        }

        return puntoCorteDiagonal;
    }

    public Mat BuscarMarcaDiagonal(Mat matRefe, int tamCuadrado, float escalaInput,
        double toleranciaLineaRecta, bool dibujarDebug, ConfigFiltroEsquina config)
    {
        var centro = new Point(tamCuadrado / 2, tamCuadrado / 2);
        LineSegmentPolar[] resultadoLineas = null;
        for (int i = 0; i < 4; i++)
        {
            var matRoiClone = new Mat(matRefe, GetRoi(i, tamCuadrado, escalaInput)).Clone();
            config.CannyEdges(matRoiClone);
            resultadoLineas = config.HoughLines(matRoiClone);
            //esto es para dibujar (solo debug)
            if (dibujarDebug)
            {
                Cv2.CvtColor(matRoiClone, matRoiClone, ColorConversionCodes.GRAY2BGR);
            }

            foreach (var segm in resultadoLineas)
            {
                var ang = segm.Theta + Mathd.PI / 2d;
                var sin = Mathd.Sin(ang);
                if (sin < 0)
                {
                    sin = -sin;
                    ang += Mathd.PI;
                }
                var cos = Mathd.Cos(ang);
                var angdeg = (float)ang * Mathf.Rad2Deg;
                bool alineado = thetasDeVertices[i].Any(thet =>
                {
                    return Mathf.Abs(Mathf.DeltaAngle((float)thet * Mathf.Rad2Deg, angdeg)) < toleranciaLineaRecta
                    || Mathf.Abs(Mathf.DeltaAngle((float)thet * Mathf.Rad2Deg, angdeg + 180f)) < toleranciaLineaRecta;
                });
                if (!alineado)
                {
                    if (dibujarDebug)
                    {
                        var offp = new Point(cos * tamCuadrado * 2d, sin * tamCuadrado * 2d);
                        Cv2.Line(matRoiClone, centro - offp, centro + offp, config.ColScalar);
                    }
                    indexVerticeMarcaDiagonal = i;
                    thetaDiagonal = ang;
                    break;
                }
            }
            if (indexVerticeMarcaDiagonal != -1)
            {
                return matRoiClone;
            }
        }
        return null;
    }

    public void DibujarDebug(Mat imagen, Color col, int grosorLado = 2, int radioVertices = 10)
    {
        if (imagen == null) return;
        var colEscalar = new Scalar(col.b * 255, col.g * 255, col.r * 255, col.a * 255);
        foreach (var vertice in verticesCuadrilatero) Cv2.Circle(imagen, vertice, radioVertices, colEscalar);
        foreach (var lin in ladosCuadrilatero) Cv2.Line(imagen, lin.P1, lin.P2, colEscalar, grosorLado);
    }

    public Mat Normalizar(Mat origen, float escala = 1f)
    {
        if (origen == null) return null;
        var tam = new Size(Mathf.FloorToInt((float)anchoSupuesto), Mathf.FloorToInt((float)altoSupuesto));
        var vertsIn = verticesCuadrilatero;
        var vertsOut = verticesNormalizados;

        if (escala != 1f)
        {
            tam = new Size(Mathf.FloorToInt((float)anchoSupuesto * escala), Mathf.FloorToInt((float)altoSupuesto * escala));
            vertsIn = vertsIn.Select(e => e * escala).ToList();
            vertsOut = vertsOut.Select(e => e * escala).ToList();
        }
        matRecuadroNormalizado = new Mat(tam, origen.Type());

        var transform = Cv2.GetPerspectiveTransform(vertsIn, vertsOut);
        Cv2.WarpPerspective(origen, matRecuadroNormalizado, transform, tam, InterpolationFlags.Cubic, BorderTypes.Constant, null);

        return matRecuadroNormalizado;

    }

    public struct ConfigFiltroEsquina
    {
        public double cannyUmbralMenor, cannyUmbralMayor, houghRho, houghThetaDiv;
        public int houghUmbral;
        public Color colDiagonal;
        public Scalar ColScalar => new Scalar(colDiagonal.b * 255, colDiagonal.g * 255, colDiagonal.r * 255);
        public double Theta => System.Math.PI / houghThetaDiv;

        public void CannyEdges(Mat mat)
        {
            Cv2.Canny(mat, mat, cannyUmbralMenor, cannyUmbralMayor, 3);
        }
        public LineSegmentPolar[] HoughLines(Mat mat)
        {
            return Cv2.HoughLines(mat, houghRho, Theta, houghUmbral);
        }
    }

}