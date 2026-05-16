using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MultiSet
{
	public static class ModelsFrameworkBridge
	{
		public struct LocalizationResult
		{
			public bool poseFound;

			public float positionX;

			public float positionY;

			public float positionZ;

			public float rotationX;

			public float rotationY;

			public float rotationZ;

			public float rotationW;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
			public string mapIds;

			public float confidence;

			public Vector3 Position => new Vector3(positionX, positionY, positionZ);

			public Quaternion Rotation => new Quaternion(rotationX, rotationY, rotationZ, rotationW);

			public string errorMessage
			{
				get
				{
					return poseFound ? "Success" : "Localization failed";
				}
				set
				{
				}
			}
		}

		private static bool IsIOSDevice
		{
			get
			{
				//IL_0007: Unknown result type (might be due to invalid IL or missing references)
				//IL_000d: Invalid comparison between Unknown and I4
				string operatingSystem = SystemInfo.operatingSystem;
				return (int)Application.platform == 8 || operatingSystem.Contains("iOS") || operatingSystem.Contains("iPadOS");
			}
		}

		[DllImport("__Internal")]
		private static extern bool _LoadModels();

		[DllImport("__Internal")]
		private static extern bool _LoadMaps(string dbDirPath, string mapIdentifier);

		[DllImport("__Internal", CharSet = CharSet.Ansi)]
		private static extern LocalizationResult _Localize([MarshalAs(UnmanagedType.LPStr)] string mapCode, [MarshalAs(UnmanagedType.LPStr)] string mapSetCode, bool isRightHanded, bool convertToGeoCoordinates, float fx, float fy, float px, float py, IntPtr imageData, int width, int height, [MarshalAs(UnmanagedType.LPStr)] string hintMapCodes, [MarshalAs(UnmanagedType.LPStr)] string hintPosition, [MarshalAs(UnmanagedType.LPStr)] string geoCoordinates);

		public static bool LoadModels()
		{
			if (IsIOSDevice)
			{
				try
				{
					return _LoadModels();
				}
				catch (Exception ex)
				{
					Debug.LogError((object)("LoadModels() exception: " + ex.Message));
					return false;
				}
			}
			Debug.LogWarning((object)"LoadModels() - Running in Editor/non-iOS platform, returning mock success");
			return true;
		}

		public static bool LoadMaps(string databasePath, string mapIdentifier)
		{
			if (IsIOSDevice)
			{
				try
				{
					return _LoadMaps(databasePath, mapIdentifier);
				}
				catch (Exception ex)
				{
					Debug.LogError((object)("LoadMaps() exception: " + ex.Message));
					return false;
				}
			}
			Debug.LogWarning((object)("LoadMaps(" + databasePath + ") - Running in Editor/non-iOS platform, returning mock success"));
			return true;
		}

		public static LocalizationResult Localize(string mapCode, string mapSetCode, bool isRightHanded, bool convertToGeoCoordinates, Vector4 cameraIntrinsics, byte[] imageData, int imageWidth, int imageHeight, string hintMapCodes, string hintPosition, string geoCoordinates)
		{
			//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
			if (IsIOSDevice)
			{
				try
				{
					if (string.IsNullOrEmpty(mapCode) && string.IsNullOrEmpty(mapSetCode))
					{
						Debug.LogError((object)"Both MapCode and MapSetCode are empty - must provide at least one");
						return new LocalizationResult
						{
							poseFound = false,
							errorMessage = "Both MapCode and MapSetCode are empty"
						};
					}
					if (imageData != null && imageData.Length != 0)
					{
						GCHandle gCHandle = default(GCHandle);
						try
						{
							gCHandle = GCHandle.Alloc(imageData, GCHandleType.Pinned);
							IntPtr imageData2 = gCHandle.AddrOfPinnedObject();
							return _Localize(mapCode, mapSetCode ?? "", isRightHanded, convertToGeoCoordinates, cameraIntrinsics.x, cameraIntrinsics.y, cameraIntrinsics.z, cameraIntrinsics.w, imageData2, imageWidth, imageHeight, hintMapCodes ?? "", hintPosition ?? "", geoCoordinates ?? "");
						}
						catch (Exception ex)
						{
							Debug.LogError((object)("GCHandle or native call failed: " + ex.Message));
							throw;
						}
						finally
						{
							if (gCHandle.IsAllocated)
							{
								gCHandle.Free();
							}
						}
					}
					Debug.LogError((object)"Image data is null or empty");
					return new LocalizationResult
					{
						poseFound = false,
						errorMessage = "Image data is null or empty"
					};
				}
				catch (Exception ex2)
				{
					Debug.LogError((object)("Localize() exception: " + ex2.Message));
					Debug.LogError((object)$"Exception type: {ex2.GetType()}");
					Debug.LogError((object)("Stack trace: " + ex2.StackTrace));
					return new LocalizationResult
					{
						poseFound = false
					};
				}
			}
			return default(LocalizationResult);
		}
	}
}
