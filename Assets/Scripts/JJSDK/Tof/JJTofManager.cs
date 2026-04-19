using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JJTofManager
{
    public enum Action
    {
        ACTION_CLEARED = 0,
        ACTION_RECEIVED = 1
    }

    public enum Gesture
    {
        GESTURE_UP = 0,
        GESTURE_DOWN = 1,
        GESTURE_LEFT = 2,
        GESTURE_RIGHT = 3,
        GESTURE_PULL = 4,
        GESTURE_PUSH = 5,
        GESTURE_HALT = 6,
        PRESENCE = 255
    }

    private AndroidJavaObject tofManager;

    public JJTofManager()
    {
        AndroidJavaObject context = ConstructorInit();
        tofManager = new AndroidJavaObject("com.jorjin.jjsdk.tof.TofManager", context);
    }

    private AndroidJavaObject ConstructorInit()
    {
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
        return context;
    }

    public void SetTofFrameListener(TofIncomingFrameListener tofIncomingFrameListener)
    {
        tofManager.Call("setTofFrameListener", tofIncomingFrameListener);
    }

    public void SetTofGestureListener(TofGestureEventListener tofGestureEventListener)
    {
        tofManager.Call("setTofGestureListener", tofGestureEventListener);
    }

    public void Open()
    {
        tofManager.Call("open");
    }

    public void Close()
    {
        tofManager.Call("close");
    }
}
