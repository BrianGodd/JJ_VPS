using UnityEngine;

public class JJCameraManager
{
    public enum Resoltion{
        RES_640x480 = 0,
        RES_800x600 = 1,
        RES_320x240 = 2,
        RES_1280x720 = 3,
        RES_1600x1200 = 4,
        RES_1920x1080 = 5,
        RES_2048x1536 = 6,
        RES_2592x1944 = 7,
        RES_3264x2448 = 8,
    }

    public enum ColorFormat
    {
        COLOR_FORMAT_RGBA = 0,
        COLOR_FORMAT_GRAY = 1,
        COLOR_FORMAT_YUV420P = 2,
        COLOR_FORMAT_NV21 = 3
    }

    private AndroidJavaObject cameraManager;
    private AndroidJavaObject javaCameraParameter;
    private AndroidJavaObject surface;

    private CameraParameter cameraParameter;

    public JJCameraManager()
    {
        AndroidJavaObject context = ConstructorInit();
        cameraManager = new AndroidJavaObject("com.jorjin.jjsdk.camera.CameraManager", context);
        javaCameraParameter = cameraManager.Call<AndroidJavaObject>("getCameraParameter");
        cameraParameter = new CameraParameter(javaCameraParameter);
    }

    private AndroidJavaObject ConstructorInit()
    {
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext");
        return context;
    }

    public void StartCamera(int colorFormat)
    {
        cameraManager.Call("startCamera", colorFormat);
    }

    public bool IsPreviewing()
    {
        return cameraManager.Call<bool>("isPreviewing");
    }

    public void StopCamera()
    {
        cameraManager.Call("stopCamera");
    }

    public void SetCameraFrameListener(FrameListener frameListener)
    {
        if (frameListener.IsNativeListener)
            // Enable native frame listener
            cameraManager.Call("setCameraNativeFrameListener", frameListener);
        else
            // Enable frame listener
            cameraManager.Call("setCameraFrameListener", frameListener);
    }

    public string[] GetResolutionList()
    {
        return cameraManager.Call<string[]>("getResolutionList");
    }

    public void SetResolutionIndex(int index)
    {
        cameraManager.Call("setResolutionIndex", index);
    }

    public CameraParameter GetCameraParmeter()
    {
        return cameraParameter;
    }

    public void SetCameraParameter(AndroidJavaObject cameraParameter)
    {
        cameraManager.Call("setCameraParameter", cameraParameter);
    }
}

