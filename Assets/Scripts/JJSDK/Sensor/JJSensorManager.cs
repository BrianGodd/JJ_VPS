using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JJSensorManager
{
    public enum SensorType
    {
        ACCELEROMETER_3D = 0,
        GRAVITY_VECTOR = 1,
        GYROMETER_3D = 2,
        AMBIENTLIGHT = 3,
        LINEAR_ACCELEROMETER = 4,
        COMPASS_3D = 5,
        DEVICE_ORIENTATION = 6,
        CUSTOM = 7,
        UNDEFINED = 8
    }
    private AndroidJavaObject sensorManager;
    private AndroidJavaObject javaCameraParameter;

    public JJSensorManager()
    {
        AndroidJavaObject context = ConstructorInit();
        sensorManager = new AndroidJavaObject("com.jorjin.jjsdk.sensor.SensorManager", context);
    }

    private AndroidJavaObject ConstructorInit()
    {
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
        return context;
    }

    public void AddSensorDataListener(SensorDataListener sensorDataListener)
    {
        sensorManager.Call("addSensorDataListener", sensorDataListener);
    }

    public void RemoveSensorDataListener(SensorDataListener sensorDataListener)
    {
        sensorManager.Call("removeSensorDataListener", sensorDataListener);
    }

    public void Open(SensorType sensorType)
    {
        sensorManager.Call("open", (int)sensorType);
    }

    public void Close(SensorType sensorType)
    {
        sensorManager.Call("close", (int)sensorType);
    }

    public void Release()
    {
        sensorManager.Call("release");
    }
}
