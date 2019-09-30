using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;

public static class ManejoDeRecursos
{
    static Dictionary<int,List<Mat>> matsEnUso = new Dictionary<int, List<Mat>>(), matsLibres = new Dictionary<int, List<Mat>>();

    public static Mat Textura(Texture2D textura, Mat matReusable = null){
        Mat salida = matReusable;
        salida = OpenCvSharp.Unity.TextureToMat(textura);
        return salida;
    }
    public static void LiberarTextura(Texture2D liberar){
        
    }
}