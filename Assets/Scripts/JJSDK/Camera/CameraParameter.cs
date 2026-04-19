using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraParameter
{
    public AndroidJavaObject javaObject;
    private int brightness;
    public CameraParameter(AndroidJavaObject androidJavaObject)
    {
        this.javaObject = androidJavaObject;
    }

    public string[] GetResolutionList()
    {
         return javaObject.Call<string[]>("getResolutionList");
    }

    public int GetBrightness()
    {
        return javaObject.Call<int>("getBrightness");
    }

    public int GetContrast()
    {
        return javaObject.Call<int>("getContrast");
    }

    public int GetGamma()
    {
        return javaObject.Call<int>("getGamma");
    }

    public int GetHue()
    {
        return javaObject.Call<int>("getHue");
    }

    public int GetSharpness()
    {
        return javaObject.Call<int>("getSharpness");
    }

    public int GetSaturation()
    {
        return javaObject.Call<int>("getSaturation");
    }

    public int GetPowerLineFrequency()
    {
        return javaObject.Call<int>("getPowerLineFrequency");
    }

    public void SetBrightness(int value)
    {
        javaObject.Call("setBrightness", value);
    }

    public void SetContrast(int value)
    {
        javaObject.Call("setContrast", value);
    }

    public void SetGamma(int value)
    {
        javaObject.Call("setGamma", value);
    }

    public void SetHue(int value)
    {
        javaObject.Call("setHue", value);
    }

    public void SetSharpness(int value)
    {
        javaObject.Call("setSharpness", value);
    }

    public void SetSaturation(int value)
    {
        javaObject.Call("setSaturation", value);
    }

    public bool IsAutoFocusOn()
    {
        return javaObject.Call<bool>("isAutoFocusOn");
    }

    public void SetAutoFocus(bool isAutoFocusOn)
    {
        javaObject.Call("setAutoFocus", isAutoFocusOn);
    }

    public void SetPowerLineFrequency(int powerLineFrequency)
    {
        javaObject.Call("setPowerLineFrequency", powerLineFrequency);
    }
}
