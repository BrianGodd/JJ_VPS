using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class FrameListener : AndroidJavaProxy
{
    public delegate void OnIncomingFrame(in Color32[] data, int width, int height, int format);
    public delegate void OnIncomingBytes(in byte[] bytes, int width, int height, int format);

    private Color32[] data;
    private OnIncomingFrame csIncomingFrame;
    private OnIncomingBytes csIncomingBytes;
    public bool IsNativeListener;

    public FrameListener(OnIncomingFrame callback) :
        base("com.jorjin.jjsdk.camera.NativeFrameListener")
    {
        IsNativeListener = true;
        csIncomingFrame = callback;
    }

    public FrameListener(OnIncomingBytes callback) :
        base("com.jorjin.jjsdk.camera.FrameListener")
    {
        IsNativeListener = false;
        csIncomingBytes = callback;
    }

    public void onIncomingFrame(long pointer, int width, int height, int format, long timestampUs)
    {
        IntPtr srcPtr = new IntPtr(pointer);

        int size = width * height;
        if (data == null || data.Length != size)
        {
            data = new Color32[size];
        }
        GCHandle handle = default;
        try
        {
            handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr dstPtr = handle.AddrOfPinnedObject();

            unsafe
            {
                Buffer.MemoryCopy(srcPtr.ToPointer(), dstPtr.ToPointer(), size*4, size*4);
            }
        }
        finally
        {
            handle.Free();
        }
        csIncomingFrame?.Invoke(data, width, height, format);
    }

    public void onIncomingFrame(AndroidJavaObject byteBuffer, int width, int height, int format)
    {
        // NOTE: The length of frameRawData is not the same with the
        //       size of the actual pixel bytes. It appears that the first 4
        //       bytes and the last 3 bytes in frameRawData are all dummy ones. So
        //       we need to use remaining() to get the actual size then set the
        //       start offset to 4 to fetch the pixel bytes in Unity.
        int size = byteBuffer.Call<int>("remaining");
        byte[] bytes = (byte[]) (Array)byteBuffer.Call<sbyte[]>("array");
        csIncomingBytes?.Invoke(bytes, width, height, format);
    }
}