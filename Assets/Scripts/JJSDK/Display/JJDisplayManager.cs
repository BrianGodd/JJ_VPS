using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JJDisplayManager
{
    public enum DisplayMode
    {
        DISPLAY_2D = 0,
        DISPLAY_3D = 1
    }

    private AndroidJavaObject displayManger;
    private AndroidJavaObject javaCameraParameter;
    private AndroidJavaObject surface;

    public JJDisplayManager()
    {
        AndroidJavaObject context = ConstructorInit();
        displayManger = new AndroidJavaObject("com.jorjin.jjsdk.display.DisplayManager", context);
    }

    private AndroidJavaObject ConstructorInit()
    {
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
        return context;
    }

    public void open()
    {
        displayManger.Call("open");
    }

    public void close()
    {
        displayManger.Call("close");
    }

    public void SetDisplayMode(DisplayMode mode)
    {
        displayManger.Call("setDisplayMode", (int)mode);
    }

    public int GetDisplayMode()
    {
        return displayManger.Call<int>("getDisplayMode");
    }

    public void SetBrightness(int value)
    {
        displayManger.Call("setBrightness", value);
    }

    public int GetBrightness()
    {
        return displayManger.Call<int>("getBrightness");
    }

    public void SetAutoBrightnessMode(bool isAuto)
    {
        displayManger.Call("setAutoBrightnessMode", isAuto);
    }
}
