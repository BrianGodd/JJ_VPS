using System.Runtime.InteropServices;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
using System;
using System.Diagnostics;

public class CamRenderer : MonoBehaviour
{
    public Text DebugMessage;
    public RawImage image;
    private Texture2D texture;
    private JJCameraManager cameraManager;
    private FrameListener frameListener;

    private Color32[] camData;
    public byte[] camBytes;
    private GCHandle handle;
    private bool frameUpdated;
    private int width = 1280;
    private int height = 720;
    private Stopwatch stopwatch;
    private int frameCount = 0;

    public bool debugMode = false;
    // Start is called before the first frame update
    void Start()
    {
        int size = width * height * 4;
        camBytes = new byte[size];
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);

        //debug
        if(!debugMode) image.texture = texture;

        cameraManager = new JJCameraManager();
        // create a NativeFrameListener
        //frameListener = new FrameListener(onIncomingFrame);
        // create a FrameListener
        frameListener = new FrameListener(onIncomingBytes);
        frameUpdated = false;

        cameraManager.SetResolutionIndex((int)JJCameraManager.Resoltion.RES_1280x720);

        cameraManager.SetCameraFrameListener(frameListener);
        cameraManager.StartCamera((int)JJCameraManager.ColorFormat.COLOR_FORMAT_RGBA);
        stopwatch = new Stopwatch();
    }

    // Update is called once per frame
    void Update()
    {
        if (frameUpdated)
        {
            if (frameListener.IsNativeListener)
                texture.SetPixels32(camData);
            else
            // NOTE: The length of camBytes is not the same with the
            //       size of the actual pixel bytes. It appears that the first 4
            //       bytes and the last 3 bytes in camBytes are all dummy ones. So
            //       we need to use remaining() to get the actual size then set the
            //       start offset to 4 to fetch the pixel bytes in Unity.
                texture.SetPixelData(camBytes, 0, 4);
            FlipTexture(ref texture);
            //texture.Apply();
            DebugMessage.text = camBytes.Length.ToString();
            image.texture = texture;
            frameUpdated = false;
        }

        // CameraParameter cp =  cameraManager.GetCameraParmeter();
        // cp.SetContrast(100);
        // cameraManager.SetCameraParameter(cp.javaObject);
    }

    public static void FlipTexture(ref Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;

        Color[] pixels = texture.GetPixels();
        Color[] flippedPixels = new Color[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int originalIndex = x + y * width;
                int flippedIndex = x + (height - y - 1) * width;
                flippedPixels[flippedIndex] = pixels[originalIndex];
            }
        }

        texture.SetPixels(flippedPixels);
        texture.Apply();
    }

    private void onIncomingFrame(in Color32[] data, int width, int height, int format)
    {
        camData = data;
        frameUpdated = true;
        // calculate FPS
        if (stopwatch.ElapsedMilliseconds >= 1000)
        {
            stopwatch.Stop();
            UnityEngine.Debug.Log("FPS: " + frameCount*1000/stopwatch.ElapsedMilliseconds);
            frameCount = 0;
            stopwatch.Restart();
        }
        else if (frameCount == 0)
            stopwatch.Start();
        frameCount+=1;
    }

    private void onIncomingBytes(in byte[] bytes, int width, int height, int format)
    {
        camBytes = bytes;
        frameUpdated = true;
        // calculate FPS
        if (stopwatch.ElapsedMilliseconds >= 1000)
        {
            stopwatch.Stop();
            UnityEngine.Debug.Log("FPS: " + frameCount*1000/stopwatch.ElapsedMilliseconds);
            frameCount = 0;
            stopwatch.Restart();
        }
        else if (frameCount == 0)
            stopwatch.Start();
        frameCount+=1;
    }

    public void SaveGlassesJPG()
    {
#if !UNITY_EDITOR 
                NativeGallery.Permission permission = NativeGallery.SaveImageToGallery(texture,
                 "JJUnityPlugin", "CaptureImage.png", (success, path) => UnityEngine.Debug.Log("Media save result: " + success + " " + path));
#endif
    }

}
