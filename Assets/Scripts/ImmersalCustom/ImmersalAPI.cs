using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using System;
using Immersal.XR;
using UnityEngine.UI;
using System.Linq;
using TMPro;

[System.Serializable]
public class MapRequest
{
    public string token;
    public int id;
}

[System.Serializable]
public class MapResponse
{
    public string error;
    public int id;
    public string name;
}

[System.Serializable]
public class SDKMapId
{
    public int id;
}

[System.Serializable]
public class SDKLocalizeRequest
{
    public string token;
    public double fx;    // camera intrinsics focal length x
    public double fy;    // camera intrinsics focal length y
    public double ox;    // camera intrinsics principal point x
    public double oy;    // camera intrinsics principal point y
    public string b64;   // Base64-encoded PNG image, 8-bit grayscale or 24-bit RGB
    public SDKMapId[] mapIds;  // list of maps to localize against
}

[System.Serializable]
public class SDKLocalizeResponse
{
    public string error;
    public bool success;
    public int map;
    public float px, py, pz;
    public float r00, r01, r02;
    public float r10, r11, r12;
    public float r20, r21, r22;
    public int confidence;
    public float time;
}


public class ImmersalAPI : MonoBehaviour
{
    [Serializable]
    public class ImmersalLocalizationSnapshot
    {
        public bool success;
        public int mapId = -1;
        public Vector3 localizedPosition;
        public Quaternion localizedRotation = Quaternion.identity;
        public string rawResponse;
        public string errorMessage;
    }

    public RAGController RAGMaster;
    public IMUManager IMU;
    public GameObject XRPlayer, Player;
    public GameObject[] LocalizeStatusIcon;
    public RawImage ImageSource;
    public CameraPlatformCustom CameraMaster;
    public Text DebugIntrinsics, DebugResponse, DebugBase64, DebugTracking;

    public string imageData;
    private const string API_URL = "https://api.immersal.com/localizeb64";
    private string token = "ffc2d21b616ba364d94cfc4600da099bd6a475be669a7126933f0e532817b864"; 
    public int[] mapIds = new int[] { 118666, 118198, 119285, 120554};
    private Vector4 intrinsics;
    private bool successInfo = false, isLocalizing = false;
    private float time = 0;

    private Vector3 XRPos1, XRPos2, PlayerPos1, PlayerPos2;
    private float XFix = 1, YFix = 1, ZFix = 1;
    public bool isAR = false, isTracking = false;

    public TextMeshProUGUI LocalizeInfoText, MapInfoText;

    public bool IsLocalizing => isLocalizing;

    public ImmersalLocalizationSnapshot LatestLocalization { get; private set; }

    public event Action<ImmersalLocalizationSnapshot> LocalizationCompleted;


    // Start is called before the first frame update
    void Start()
    {
        XRPos1 = Vector3.zero;
        XRPos2 = Vector3.zero;
        PlayerPos1 = Vector3.zero;
        PlayerPos2 = Vector3.zero;
    }

    // Update is called once per frame
    void Update()
    {
        // Debug
        LocalizeInfoText.text = $"Pos: {Player.transform.position:F3}\nRot: {Player.transform.rotation.eulerAngles:F3}";

        RAGMaster.pos_x = Player.transform.position.x;
		RAGMaster.pos_z = Player.transform.position.z;
		RAGMaster.rot = Player.transform.rotation.eulerAngles.y;

        // if(!successInfo)
        // {
        //     successInfo = CameraMaster.GetIntrinsics(out intrinsics);
        //     DebugIntrinsics.text = intrinsics.ToString();
        // }

        if(!isLocalizing)
        {
            //add delay or not
            //if(time >= 0.5f)
            //{
            //time = 0;
            isLocalizing = true;
            imageData = ConvertUIImageToBase64();
            StartCoroutine(LocalizeImage(imageData));
            //}
            //else time += Time.deltaTime;
        }

        if(!isAR) //unable AR Foundation
        {
            if(isTracking)
            {
                DebugTracking.text = "Done";
                Player.transform.GetChild(0).gameObject.SetActive(true);
                Player.transform.localPosition =
                new Vector3(PlayerPos2.x + (XRPlayer.transform.localPosition.x - XRPos2.x)*XFix , 
                            PlayerPos2.y + (XRPlayer.transform.localPosition.y - XRPos2.y)*YFix , 
                            Player.transform.localPosition.z);

            }
            else if(XRPos1 == Vector3.zero) DebugTracking.text = XRPlayer.transform.localPosition.ToString();
            else DebugTracking.text = Vector3.Distance(XRPlayer.transform.localPosition, XRPos1).ToString();
        }

    }

    public void RunLocalizeOnce()
    {
        imageData = ConvertUIImageToBase64();
        StartCoroutine(LocalizeImage(imageData));
    }

    public void LocalizeEncodedImage(byte[] encodedImageBytes, Vector4 intrinsicsOverride, string imageMimeType = "image/jpeg")
    {
        if (encodedImageBytes == null || encodedImageBytes.Length == 0)
        {
            NotifyLocalizationCompleted(new ImmersalLocalizationSnapshot
            {
                success = false,
                errorMessage = "Encoded image bytes are empty."
            });
            return;
        }

        isLocalizing = true;
        string base64Image = Convert.ToBase64String(encodedImageBytes);
        StartCoroutine(LocalizeImage(base64Image, intrinsicsOverride));
    }

    //tracking again
    public void TrackActive()
    {
        XRPos1 = Vector3.zero;
        XRPos2 = Vector3.zero;
        PlayerPos1 = Vector3.zero;
        PlayerPos2 = Vector3.zero;
        isTracking = false;
    }

    void ApplyTransform(SDKLocalizeResponse response)
    {
        if(response.map == -1) return;

        IMU.isVPSUpdating = true;

        // if(!isAR)
        // {
        //     if(!isTracking)
        //     {
        //         Player.transform.GetChild(0).gameObject.SetActive(true);
        //         Player.transform.localPosition = new Vector3(response.px, response.py, response.pz);
        //         if(XRPos1 == Vector3.zero)
        //         {
        //             XRPos1 = XRPlayer.transform.localPosition;
        //             PlayerPos1 = Player.transform.localPosition;
        //         }
        //         else if(XRPos2 == Vector3.zero && Vector3.Distance(XRPlayer.transform.localPosition, XRPos1) > 0.3f)
        //         {
        //             XRPos2 = XRPlayer.transform.localPosition;
        //             PlayerPos2 = Player.transform.localPosition;
        //             XFix = (float)(PlayerPos2.x - PlayerPos1.x)/(float)(XRPos2.x - XRPos1.x);
        //             YFix = (float)(PlayerPos2.y - PlayerPos1.y)/(float)(XRPos2.y - XRPos1.y);
        //             ZFix = (float)(PlayerPos2.z - PlayerPos1.z)/(float)(XRPos2.z - XRPos1.z);
        //             isTracking = true;
        //         }
        //     }
        //     else
        //     {
        //         Player.transform.GetChild(0).gameObject.SetActive(true);
        //         Player.transform.localPosition =
        //         new Vector3(PlayerPos2.x + (XRPlayer.transform.localPosition.x - XRPos2.x)*XFix , 
        //                     PlayerPos2.y + (XRPlayer.transform.localPosition.y - XRPos2.y)*YFix , 
        //                     Player.transform.localPosition.z);
        //     }
        // }
        // else
        // {
        //     Player.transform.GetChild(0).gameObject.SetActive(true);
        //     Player.transform.localPosition = new Vector3(response.px, response.py, response.pz);
        // }
        
        // 建立旋轉矩陣
        Matrix4x4 rotationMatrix = new Matrix4x4();
        rotationMatrix.SetRow(0, new Vector4(response.r00, response.r01, response.r02, 0));
        rotationMatrix.SetRow(1, new Vector4(response.r10, response.r11, response.r12, 0));
        rotationMatrix.SetRow(2, new Vector4(response.r20, response.r21, response.r22, 0));
        rotationMatrix.SetRow(3, new Vector4(0, 0, 0, 1));

        // 進行鏡像轉換：針對 X 軸做反轉
        Matrix4x4 mirrorX = Matrix4x4.Scale(new Vector3(-1, 1, 1));
        Matrix4x4 correctedMatrix = mirrorX * rotationMatrix;

        // 從修正後矩陣中取出旋轉與位置
        Quaternion rotation = Quaternion.LookRotation(
            correctedMatrix.GetColumn(2), // Forward (Z)
            correctedMatrix.GetColumn(1)  // Up (Y)
        );

        rotation = rotation * Quaternion.Euler(0, 0, 180);

        // 位置鏡像處理：只反轉 X 座標
        Vector3 position = new Vector3(
            -response.px, // ← X 軸反轉
            response.py,
            response.pz
        );

        Debug.Log($"Corrected pos: {position}, rot: {rotation}");

        // 套用到角色
        Player.transform.SetPositionAndRotation(position, rotation);

        // update IMU
        IMU.UpdateWorldSpaceByVPS();

        // 設定旋轉
        //Player.transform.localRotation = rotation;
    }

    public IEnumerator LocalizeImage(string base64Image)
    {
        yield return LocalizeImage(base64Image, GetDefaultIntrinsics());
    }

    public IEnumerator LocalizeImage(string base64Image, Vector4 intrinsicsOverride)
    {
        base64Image = base64Image.Replace("\n", "").Replace("\r", "");
        SDKMapId[] sdkMapIds = mapIds.Select(id => new SDKMapId { id = id }).ToArray();

        SDKLocalizeRequest requestData = new SDKLocalizeRequest
        {
            token = token,
            mapIds = sdkMapIds,
            b64 = base64Image,
            ox = intrinsicsOverride.z,
            oy = intrinsicsOverride.w,
            fx = intrinsicsOverride.x,
            fy = intrinsicsOverride.y
            
            //PHONE
            // ox = 2000,
            // oy = 1600,
            // fx = 3000,
            // fy = 3000
        };

        string jsonPayload = JsonUtility.ToJson(requestData);
        //DebugBase64.text = base64Image;
        //DebugIntrinsics.text = jsonPayload;
        byte[] bodyRaw = Encoding.ASCII.GetBytes(jsonPayload);

        using (UnityWebRequest request = new UnityWebRequest(API_URL, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error: " + request.error);
                DebugResponse.text = "Error: " + request.error;
                isLocalizing = false;
                ShowLocalizeStatus(2);
                NotifyLocalizationCompleted(new ImmersalLocalizationSnapshot
                {
                    success = false,
                    errorMessage = request.error
                });
            }
            else
            {
                string responseText = request.downloadHandler.text;

                SDKLocalizeResponse response = JsonUtility.FromJson<SDKLocalizeResponse>(responseText);
                ApplyTransform(response);
                StartCoroutine(GetMapName(response.map));

                Debug.Log("Response: " + responseText);
                DebugResponse.text = "Response: " + responseText;

                isLocalizing = false;
                if(response.map == -1) ShowLocalizeStatus(1);
                else ShowLocalizeStatus(0);
                GetUnityPose(response, out Vector3 localizedPosition, out Quaternion localizedRotation);
                NotifyLocalizationCompleted(new ImmersalLocalizationSnapshot
                {
                    success = response.map != -1,
                    mapId = response.map,
                    localizedPosition = localizedPosition,
                    localizedRotation = localizedRotation,
                    rawResponse = responseText,
                    errorMessage = response.map == -1 ? "Pose not found." : null
                });
            }
        }
    }

    private Vector4 GetDefaultIntrinsics()
    {
        return new Vector4(1161.352133f, 1162.871712f, 630.465034f, 368.853068f);
    }

    private void GetUnityPose(SDKLocalizeResponse response, out Vector3 position, out Quaternion rotation)
    {
        Matrix4x4 rotationMatrix = new Matrix4x4();
        rotationMatrix.SetRow(0, new Vector4(response.r00, response.r01, response.r02, 0));
        rotationMatrix.SetRow(1, new Vector4(response.r10, response.r11, response.r12, 0));
        rotationMatrix.SetRow(2, new Vector4(response.r20, response.r21, response.r22, 0));
        rotationMatrix.SetRow(3, new Vector4(0, 0, 0, 1));

        Matrix4x4 mirrorX = Matrix4x4.Scale(new Vector3(-1, 1, 1));
        Matrix4x4 correctedMatrix = mirrorX * rotationMatrix;

        rotation = Quaternion.LookRotation(correctedMatrix.GetColumn(2), correctedMatrix.GetColumn(1));
        rotation *= Quaternion.Euler(0, 0, 180);
        position = new Vector3(-response.px, response.py, response.pz);
    }

    private void NotifyLocalizationCompleted(ImmersalLocalizationSnapshot snapshot)
    {
        LatestLocalization = snapshot;
        LocalizationCompleted?.Invoke(snapshot);
    }

    public IEnumerator GetMapName(int id)
    {
        string url = "https://api.immersal.com/metadataget";

        // 建立 JSON 請求內容
        string jsonBody = JsonUtility.ToJson(new MapRequest()
        {
            token = token,
            id = id
        });

        // 建立 POST 請求
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        // 發送請求
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.Log("API 錯誤: " + request.error);
        }
        else
        {
            string responseText = request.downloadHandler.text;
            MapResponse res = JsonUtility.FromJson<MapResponse>(responseText);

            if (res.error == "none")
            {
                MapInfoText.text = res.name;
                Debug.Log($"Map ID {id} 名稱: {res.name}");
            }
            else
            {
                Debug.Log($"查詢失敗: {res.error}");
            }
        }
    }

    public void ShowLocalizeStatus(int status)
    {
        switch(status)
        {
            case 0:
                LocalizeStatusIcon[0].SetActive(true);
                LocalizeStatusIcon[1].SetActive(false);
                LocalizeStatusIcon[2].SetActive(false);
                break;
            case 1:
                LocalizeStatusIcon[0].SetActive(false);
                LocalizeStatusIcon[1].SetActive(true);
                LocalizeStatusIcon[2].SetActive(false);
                break;
            case 2:
                LocalizeStatusIcon[0].SetActive(false);
                LocalizeStatusIcon[1].SetActive(false);
                LocalizeStatusIcon[2].SetActive(true);
                break;
        }
    }

    public string ConvertUIImageToBase64()
    {
        Texture2D sourceTexture = ImageSource.texture as Texture2D;

        Texture2D rgbTexture = ConvertToRGB24(sourceTexture);

        byte[] imageBytes = rgbTexture.EncodeToPNG();

        string base64String = System.Convert.ToBase64String(imageBytes);

        Destroy(rgbTexture);

        return base64String;
    }

    private Texture2D ConvertToRGB24(Texture2D source)
    {
        Texture2D newTexture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
        newTexture.SetPixels(source.GetPixels());
        newTexture.Apply();
        return newTexture;
    }
}
