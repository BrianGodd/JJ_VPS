using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TofIncomingFrameListener : AndroidJavaProxy
{
    public delegate void OnTofIncomingFrame(float[] range, float[] signalRate);

    private OnTofIncomingFrame csTofIncomingFrame;

    public TofIncomingFrameListener(OnTofIncomingFrame callback) :
        base("com.jorjin.jjsdk.tof.TofIncomingFrameListener")
    {
        csTofIncomingFrame = callback;
    }

    public void onTofIncomingFrame(AndroidJavaObject frame)
    {
        // NOTE: This try-catch is for the polymorphism of
        //       onTofIncomingFrame(ArrayList frame) in
        //       TofIncomingFrameListener since it will cause
        //       AndroidJavaException and should be ignored.
        try {
            // TODO: remove the try-catch in the next SDK release
            float[] range = frame.Get<float[]>("medianRange");
            float[] signalRate = frame.Get<float[]>("peakSignalRate");

            csTofIncomingFrame?.Invoke(range, signalRate);
        }
        catch (Exception e)
        {
            // Do nothing
        }
    }
}