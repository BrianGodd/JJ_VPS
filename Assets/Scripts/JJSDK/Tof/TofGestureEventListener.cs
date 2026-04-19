using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TofGestureEventListener : AndroidJavaProxy
{
    public delegate void OnTofGestureEvent(long eventTime, int action, int gesture);

    private OnTofGestureEvent csTofGestureEvent;

    public TofGestureEventListener(OnTofGestureEvent callback) :
        base("com.jorjin.jjsdk.tof.TofGestureEventListener")
    {
        csTofGestureEvent = callback;
    }

    public void onTofGestureEvent(AndroidJavaObject gestureEvent)
    {
        long time = gestureEvent.Call<long>("getEventTime");
        int action = gestureEvent.Call<int>("getAction");
        int gesture = gestureEvent.Call<int>("getGesture");

        csTofGestureEvent?.Invoke(time, action, gesture);
    }
}
