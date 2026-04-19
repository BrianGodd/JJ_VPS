using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;

public class SensorExample : MonoBehaviour
{
    private JJSensorManager sensorManager;
    private SensorDataListener sensorDataListener;

    public Text textAcc, textGyro, textGravity, textCompass, textRV, textLAcc, textLight;

    private float[] data;

    private string valueGyro;
    private string valueAcc;
    private string valueLAcc;
    private string valueCompass;
    private string valueRV;
    private string valueGravity;
    private string valueLight;
    private GCHandle handle;
    private IntPtr outputPtr;
    private IntPtr dataPtr;

    private bool dataUpdated;

    public Quaternion initQuaternion = Quaternion.identity;
    public Vector3 Compass = Vector3.zero;

    void Awake() {

        sensorManager = new JJSensorManager();
        sensorDataListener = new SensorDataListener(OnSensorDataChanged);
        sensorManager.AddSensorDataListener(sensorDataListener);
    }

    void Start()
    {
        sensorManager.Open(JJSensorManager.SensorType.DEVICE_ORIENTATION);
        sensorManager.Open(JJSensorManager.SensorType.GRAVITY_VECTOR);
        sensorManager.Open(JJSensorManager.SensorType.GYROMETER_3D);
        sensorManager.Open(JJSensorManager.SensorType.ACCELEROMETER_3D);
        sensorManager.Open(JJSensorManager.SensorType.AMBIENTLIGHT);
        sensorManager.Open(JJSensorManager.SensorType.COMPASS_3D);
        sensorManager.Open(JJSensorManager.SensorType.LINEAR_ACCELEROMETER);
        dataUpdated = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (dataUpdated)
        {
            textAcc.text = "Acc :"+valueAcc;
            textGravity.text = "G :"+valueGravity;
            textGyro.text = "GYRO :"+valueGyro;
            textRV.text = "RV :"+valueRV;
            textLAcc.text = "LAcc :"+valueLAcc;
            textCompass.text = "Compass :"+valueCompass;
            textLight.text = "Light :"+valueLight;
        }
    }

    public void OnSensorDataChanged(int sensorType, float[] values, long timestamp)
    {
        switch(sensorType)
        {
            case (int)JJSensorManager.SensorType.ACCELEROMETER_3D:
                valueAcc = string.Join(" ", values);
            break;
            case (int)JJSensorManager.SensorType.COMPASS_3D:
                valueCompass = string.Join(" ", values);
                Compass = new Vector3(values[0], values[1], values[2]);
            break;
            case (int)JJSensorManager.SensorType.DEVICE_ORIENTATION:
                // valueRV = string.Join(" ", values);

                initQuaternion = new Quaternion(values[0], values[1], values[2], values[3]);
                Vector3 eulerAngles = initQuaternion.eulerAngles;
                valueRV = string.Join(" ", eulerAngles.x.ToString("F2"), eulerAngles.y.ToString("F2"), eulerAngles.z.ToString("F2"));
            break;
            case (int)JJSensorManager.SensorType.GRAVITY_VECTOR:
                valueGravity = string.Join(" ", values);
            break;
            case (int)JJSensorManager.SensorType.GYROMETER_3D:
                valueGyro = string.Join(" ", values);
            break;
            case (int)JJSensorManager.SensorType.LINEAR_ACCELEROMETER:
                valueLAcc = string.Join(" ", values);
            break;
            case (int)JJSensorManager.SensorType.AMBIENTLIGHT:
                valueLight = string.Join(" ", values);
            break;
        }
        dataUpdated = true;
    }
}
