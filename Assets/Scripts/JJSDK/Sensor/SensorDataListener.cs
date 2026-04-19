using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SensorDataListener : AndroidJavaProxy
{
    public delegate void OnSensorDataChanged(int sensorType, float[] values, long timestamp);

    private OnSensorDataChanged csIncomingSensorData;

    public SensorDataListener(OnSensorDataChanged callback) :
        base("com.jorjin.jjsdk.sensor.SensorDataListener")
    {
        csIncomingSensorData = callback;
    }

    public void onSensorDataChanged(int sensorType, float[] values, long timestamp)
    {
        csIncomingSensorData?.Invoke(sensorType, values, timestamp);
    }
}