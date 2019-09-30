using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;

public class WebCamToMat : MonoBehaviour//, IEsCable
{
    static WebCamDevice[] Devices{
        get{
            if(devices == null) BuscarDevices();
            return devices;
        }
    }
    static WebCamDevice[] devices;
    public static void BuscarDevices(){
        devices = WebCamTexture.devices;
    }

    public Mat MatOutput(){
        return null;
    }
}
