using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IMUManager : MonoBehaviour
{
    public GameObject PlayerCam;
    public SensorExample sensor;

    private Quaternion calibrationOffset = Quaternion.identity;
    private bool isCalibrated = false;
    public bool isVPSUpdating = false;

    void Update()
    {
        if (sensor.initQuaternion != Quaternion.identity && !isVPSUpdating)
            UpdateExternalQuaternion(sensor.initQuaternion);
    }

    // 右手 IMU -> Unity 左手（Z 取反）的穩定轉換
    static Vector3 RotateRH(Quaternion q, Vector3 v)
    {
        // v' = v + 2 * cross(q.xyz, cross(q.xyz, v) + q.w * v)
        var u = new Vector3(q.x, q.y, q.z);
        var uv = Vector3.Cross(u, v);
        var uuv = Vector3.Cross(u, uv);
        return v + 2.0f * (q.w * uv + uuv);
    }

    static Quaternion RightHandToUnity(Quaternion qRH)
    {
        // 以 +Z 為前、+Y 為上；右手→Unity 左手：Z 取反
        Vector3 f_s = RotateRH(qRH, Vector3.forward);
        Vector3 u_s = RotateRH(qRH, Vector3.up);

        Vector3 f_u = new Vector3( f_s.x,  f_s.y, -f_s.z);
        Vector3 u_u = new Vector3( u_s.x,  u_s.y, -u_s.z);

        // 先組成 Unity 四元數
        Quaternion q = Quaternion.LookRotation(f_u, u_u);

        // 關鍵修正：共軛於 Y 軸 180°（翻 X、Z 的旋向；Y 不變）
        // 等價於 q = Quaternion.Euler(0,180,0) * q * Quaternion.Euler(0,-180,0)
        q = new Quaternion(-q.x, q.y, -q.z, q.w);

        return q;
    }

    public void UpdateExternalQuaternion(Quaternion rightHandedQuat)
    {
        Quaternion unityQuat = RightHandToUnity(rightHandedQuat);

        // 第一次：把「現在量到的感測器姿態」貼齊「目前相機世界姿態」
        if (!isCalibrated)
        {
            calibrationOffset = PlayerCam.transform.rotation * Quaternion.Inverse(unityQuat);
            isCalibrated = true;
        }

        // 之後每幀：前乘校正
        PlayerCam.transform.rotation = calibrationOffset * unityQuat;
    }

    public void UpdateWorldSpaceByVPS()
    {
        var qRH = sensor.initQuaternion;
        if (qRH != Quaternion.identity)
        {
            var unityQuat = RightHandToUnity(qRH);

            // 用 VPS 目前的相機姿態當「目標世界姿態」
            calibrationOffset = PlayerCam.transform.rotation * Quaternion.Inverse(unityQuat);
            isCalibrated = true;
        }
        isVPSUpdating = false;
    }
}
