using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;

public interface IEsCable
{
    Mat MatOut();
    List<Mat> MatsOut();
    //y demas

    // Devuelve el metodo de input, para si pasas un lambda, podes despues dessuscribirlo
    System.Action<IEsCable> Suscribir(System.Action<IEsCable> metodo, bool suscribir = true);//false = desuscribir   
}
public interface IEntraCable
{
    IEsCable Entrada();
    void Actualizar(IEsCable nodo);
}
public abstract class CableCanalBehaviour : MonoBehaviour, IEsCable, IEntraCable {
    public virtual Mat MatOut()=>null;
    public virtual List<Mat> MatsOut()=>null;
    public virtual System.Action<IEsCable> Suscribir(System.Action<IEsCable> metodo, bool suscribir = true) {
        alActualizar = alActualizar.SuscribirExt(metodo, suscribir);
        return metodo;
    }
    
    event System.Action<IEsCable> alActualizar;

    // [SerializeField]
    // bool tieneEntrada = false;
    // [Ocultador("tieneEntrada")]
    [SerializeField]
    CableCanalBehaviour entrada;

    public virtual IEsCable Entrada()=>entrada;
    public virtual void Actualizar(IEsCable nodo){}

    protected void PropagarActualizacion() { 
        if(alActualizar!=null)alActualizar.Invoke(this);
    }
    
    public void Actualizar()=>Actualizar(entrada);
    public virtual void OnEnable()
    {
        this.IniciarConexionesExt();
    }
    public virtual void OnDisable()
    {
        this.TerminarConexionesExt();
    }
}

public static class CablesExt
{
    public static System.Action<IEsCable> SuscribirExt(this System.Action<IEsCable> alActualizar, System.Action<IEsCable> accion, bool suscr = true)
    {
        if (accion != null)
        {
            alActualizar -= accion;
            if (suscr) alActualizar += accion;
        }
        return alActualizar;
    }

    public static void IniciarConexionesExt(this IEntraCable yoMeInicio)
    {
        var entradaAsCable = yoMeInicio.Entrada();
        if (entradaAsCable != null)
        {
            entradaAsCable.Suscribir(yoMeInicio.Actualizar, true);
            yoMeInicio.Actualizar(entradaAsCable);
        }
    }
    public static void TerminarConexionesExt(this IEntraCable yoMeTermino)
    {
        var entradaAsCable = yoMeTermino.Entrada();
        if (entradaAsCable != null)
        {
            entradaAsCable.Suscribir(yoMeTermino.Actualizar, false);
        }
    }
}