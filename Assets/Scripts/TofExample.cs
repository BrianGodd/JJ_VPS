using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Runtime.InteropServices;

public class TofExample : MonoBehaviour
{
    private JJTofManager tofManager;
    private TofIncomingFrameListener tofIncomingFrameListener;
    private TofGestureEventListener tofGestureEventListener;

    public Text textRange, textSignalRate, textGesture;

    private string valueRange;
    private string valueSignalRate;
    private bool dataUpdated;
    private string strGesture;
    private bool gestureUpdated;
    private List<string> latestGestureList = new List<string>();

    void Awake() {

        tofManager = new JJTofManager();
        tofIncomingFrameListener = new TofIncomingFrameListener(OnTofIncomingFrame);
        tofManager.SetTofFrameListener(tofIncomingFrameListener);
        tofGestureEventListener = new TofGestureEventListener(OnTofGestureEvent);
        tofManager.SetTofGestureListener(tofGestureEventListener);
    }

    void Start()
    {
        tofManager.Open();
        dataUpdated = false;
        gestureUpdated = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (dataUpdated)
        {
            textRange.text = "Range :"+valueRange;
            textSignalRate.text = "SignalRate :"+valueSignalRate;
            dataUpdated = false;
        }

        if(gestureUpdated)
        {
            //only show last 5 event
            if (latestGestureList.Count < 5)
            {
                latestGestureList.Add(strGesture);
            }
            else
            {
                latestGestureList.RemoveAt(0);
                latestGestureList.Add(strGesture);
            }
            textGesture.text = string.Join("\n", latestGestureList);
            gestureUpdated = false;
        }
    }

    public void OnTofIncomingFrame(float[] range, float[] signalRate)
    {
        float maxSignalRate = 0;
        int maxSignalRateIndex = 0;
        for (int i=0; i<range.Length; i++)
        {
            if (signalRate[i] > maxSignalRate) {
                maxSignalRate = signalRate[i];
                maxSignalRateIndex = i;
            }
        }
        valueRange = string.Join(" ", range[maxSignalRateIndex]);
        valueSignalRate = string.Join(" ", signalRate[maxSignalRateIndex]);
        dataUpdated = true;
    }

    public void OnTofGestureEvent(long eventTime, int action, int gesture)
    {
        string currentAction = "";

        switch (action) {
            case (int)JJTofManager.Action.ACTION_CLEARED:
                currentAction = "Cleared";
                break;
            case (int)JJTofManager.Action.ACTION_RECEIVED:
                currentAction = "Received";
                break;
        }

        string currentGesture = "";

        switch (gesture)
        {
            case (int)JJTofManager.Gesture.GESTURE_UP:
                currentGesture = "Up";
                break;
            case (int)JJTofManager.Gesture.GESTURE_DOWN:
                currentGesture = "Down";
                break;
            case (int)JJTofManager.Gesture.GESTURE_LEFT:
                currentGesture = "Left";
                break;
            case (int)JJTofManager.Gesture.GESTURE_RIGHT:
                currentGesture = "Right";
                break;
            case (int)JJTofManager.Gesture.GESTURE_PULL:
                currentGesture = "Pull";
                break;
            case (int)JJTofManager.Gesture.GESTURE_PUSH:
                currentGesture = "Push";
                break;
            case (int)JJTofManager.Gesture.GESTURE_HALT:
                currentGesture = "Halt";
                break;
            case (int)JJTofManager.Gesture.PRESENCE:
                currentGesture = "Presence";
                break;
        }
        strGesture = "eventTime = " + eventTime + ", action = " + currentAction + ", gesture = " + currentGesture;
        gestureUpdated = true;
    }
}
