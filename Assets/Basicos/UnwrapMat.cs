
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class UnwrapMat : MonoBehaviour, IEsCable, IEntraCable
{
    public bool generarNuevoMat;
    public bool correccionAspect = true;
    public float aspectRatioManual = 0f;
    public bool recortar = true;
    [SerializeField]
    MonoBehaviour entrada;
    Mat mat;
    event System.Action<IEsCable> alActualizar;
    public Point2f[] puntos = { new Point2f(0, 0), new Point2f(1, 0), new Point2f(1, 1), new Point2f(0, 1) };

    /*
    Micro cosas genericas
     */
    // IEsCable
    public List<Mat> MatsOut() => null;
    public Mat MatOut() => mat;
    public System.Action<IEsCable> Suscribir(System.Action<IEsCable> accion, bool suscribir = true)
    {
        alActualizar = alActualizar.SuscribirExt(accion, suscribir);
        return accion;
    }
    // IEntraCable
    public IEsCable Entrada() => entrada as IEsCable;
    /*
    END microcosas genericas
     */

    /*
    internas micrificadas
     */
    public Vector2[] PuntosUnity { get => System.Array.ConvertAll(puntos, (Point2f inp) => (new Vector2(inp.X, inp.Y))); }

    void OnEnable()
    {
        this.IniciarConexionesExt();
    }
    void OnDisable()
    {
        this.TerminarConexionesExt();
    }

    void Actualizar()
    {
        Actualizar(Entrada());
    }
    public void Actualizar(IEsCable nodo)
    {
        if (nodo == null) return;

        var niuMat = nodo.MatOut();
        if (niuMat == null) return;
        
        if (generarNuevoMat)
        {
            if (mat == null) mat = new Mat();
        }
        else if (niuMat != null)
        {
            if (mat != niuMat) mat = niuMat;
        }
        mat = UnwrapShape(niuMat, mat, puntos,aspectRatioManual,correccionAspect, recortar);

        if (alActualizar != null) alActualizar.Invoke(this);
    }

    // Using equations from: http://research.microsoft.com/en-us/um/people/zhang/Papers/WhiteboardRectification.pdf
    //https://stackoverflow.com/questions/38285229/calculating-aspect-ratio-of-perspective-transform-destination-image
    //https://stackoverflow.com/users/3427404/yhenon
    //mas ayuda de 
    //http://urbanar.blogspot.com/2011/05/aspect-ratio-of-rectangle-in.html
    public static Mat UnwrapShape(Mat imgIn, Mat imgOut, Point2f[] corners, float aspectRatioManual=0f, bool correccionAspect = true, bool recortar = false, int maxSize = 0)
    {
        if (corners.Length != 4)
            throw new OpenCvSharpException("argument 'points' must be of length = 4");

        // grab bounds corners, sort them in correct order and
        // get width/height of the shape
        double widthT = Point2f.Distance(corners[0], corners[1]);  // lt -> rt
        double heightL = Point2f.Distance(corners[0], corners[3]); // lt -> lb
        double widthB = Point2f.Distance(corners[3], corners[2]);  // lb -> rb
        double heightR = Point2f.Distance(corners[1], corners[2]); // rt -> rb

        //image center
        var u0 = imgIn.Width / 2f;
        var v0 = imgIn.Height / 2f;
        double w = System.Math.Max(widthT, widthB);
        double h = System.Math.Max(heightL, heightR);
        //visible aspect ratio
        double ar_vis = w / h;
        var width = (float)w;
        var height = (float)h;
        if (aspectRatioManual > 0f) {
            correccionAspect = false;
            if (aspectRatioManual > 1f)
            height = width*aspectRatioManual;
            else
            width = height/aspectRatioManual;
        }

        if (correccionAspect)
        {
            //make numpy arrays and append 1 for linear algebra
            double[] m1 = new double[] { corners[0].X, corners[0].Y, 1d };
            double[] m2 = new double[] { corners[1].X, corners[1].Y, 1d };
            double[] m3 = new double[] { corners[2].X, corners[2].Y, 1d };
            double[] m4 = new double[] { corners[3].X, corners[3].Y, 1d };
            // corners 3 d = corners2 + 1forward
            var cor3 = corners.Select(corner => new Vector3(corner.X, corner.Y, 1f)).ToList();

            //calculate the focal disrance
            var k2 = Vector3.Dot(Vector3.Cross(cor3[0], cor3[3]), cor3[2]) / Vector3.Dot(Vector3.Cross(cor3[1], cor3[3]), cor3[2]);
            var k3 = Vector3.Dot(Vector3.Cross(cor3[0], cor3[3]), cor3[1]) / Vector3.Dot(Vector3.Cross(cor3[2], cor3[3]), cor3[1]);

            var n2 = k2 * cor3[1] - cor3[0];
            var n3 = k3 * cor3[2] - cor3[0];

            var n21 = n2[0];
            var n22 = n2[1];
            var n23 = n2[2];

            var n31 = n3[0];
            var n32 = n3[1];
            var n33 = n3[2];

            var f = Mathf.Sqrt(Mathf.Abs((1.0f / (n23 * n33)) * ((n21 * n31 - (n21 * n33 + n23 * n31) * u0 + n23 * n33 * u0 * u0) + (n22 * n32 - (n22 * n33 + n23 * n32) * v0 + n23 * n33 * v0 * v0))));
            //A = np.array([[f,0,u0],[0,f,v0],[0,0,1]]).astype('float32')

            // var A = new Point3f[]{new Point3f(f,0,u0),new Point3f(0,f,v0),new Point3f(0,0,1)};
            var data = new float[] { f, 0f, u0, 0f, f, v0, 0f, 0f, 1f };
            using (Mat A = new Mat(3, 3, MatType.CV_32FC1, data), At = new Mat(), Ati = new Mat(), Ai = new Mat(),
            n2Mat = new Mat(3, 1, MatType.CV_32FC1, new float[] { n2.x, n2.y, n2.z }),
            n3Mat = new Mat(3, 1, MatType.CV_32FC1, new float[] { n3.x, n3.y, n3.z }))
            {
                Cv2.Transpose(A, At);
                Cv2.Invert(At, Ati);
                Cv2.Invert(A, Ai);

                var ar_real2 = (n2Mat.T() * (A.Inv().T()) * (A.Inv()) * n2Mat) / (n3Mat.T() * (A.Inv().T()) * (A.Inv()) * n3Mat);
                var ar_real = Mathf.Sqrt(ar_real2.ToMat().At<float>(0, 0));
                // //calculate the real aspect ratio
                // var ar_real = Mathf.Sqrt( n2Mat.Sum(Ati).Dot(Ai).Dot(n2Mat) / n3Mat.Dot(Ati).Dot(Ai).Dot(n3Mat) )

                // var ar_real = Mathf.sqrt(np.dot(np.dot(np.dot(n2,Ati),Ai),n2)/np.dot(np.dot(np.dot(n3,Ati),Ai),n3))
                if (ar_real < ar_vis)
                {
                    width = (float)w;
                    height = (float)w / ar_real;
                }
                else
                {
                    height = (float)h;
                    width = (float)h * ar_real;
                }

            }
        }

        // compute transform
        Point2f[] destination = new Point2f[]
        {
                new Point2f(0,     0),
                new Point2f(width, 0),
                new Point2f(width, height),
                new Point2f(0,     height)
        };
        var transform = Cv2.GetPerspectiveTransform(corners, destination);

        // un-warp
        Cv2.WarpPerspective(imgIn, imgOut, transform, recortar ? new Size(width, height) : imgOut.Size(), InterpolationFlags.Cubic, BorderTypes.Constant, null);
        //        return img.WarpPerspective(transform, new Size(width, height), InterpolationFlags.Cubic);
        return imgOut;
    }

    public void ActualizarPuntos(List<Vector2> puntosIn)
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying) UnityEditor.Undo.RecordObject(this, "actualizar puntos");
#endif
        for (int i = 0; i < puntos.Length && i < puntosIn.Count; i++)
        {
            puntos[i].X = puntosIn[i].x;
            puntos[i].Y = puntosIn[i].y;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(UnwrapMat))]
    public class UnwrapMatEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var unwrapMat = target as UnwrapMat;
            if (GUILayout.Button("OnEnable")) unwrapMat.OnEnable();
            if (GUILayout.Button("Actualizar")) unwrapMat.Actualizar();
            if (GUILayout.Button("Editar"))
            {
                var tieneTex = unwrapMat.entrada as IContieneTextura;
                var texRef = tieneTex == null ? null : tieneTex.Textura();
                EditorDePuntos.Abrir(unwrapMat.ActualizarPuntos, unwrapMat.PuntosUnity, texRef);
            }

            var mat = unwrapMat.mat;
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
