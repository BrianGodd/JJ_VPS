using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class Util
	{
		private static Material s_RotationMaterial;

		private const string SHADER_PATH = "Shaders/RotateTexture";

		public static bool IsNetworkAvailable()
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Invalid comparison between Unknown and I4
			return (int)Application.internetReachability > 0;
		}

		public static Texture2D RotateTextureCounterClockwise(Texture2D originalTexture)
		{
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0084: Expected O, but got Unknown
			//IL_009a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Expected O, but got Unknown
			if ((Object)(object)s_RotationMaterial == (Object)null)
			{
				Shader val = Resources.Load<Shader>("Shaders/RotateTexture");
				if ((Object)(object)val == (Object)null)
				{
					Debug.LogError((object)"Could not load shader from Resources at path: 'Shaders/RotateTexture'. Make sure the shader file is in the correct Resources subfolder and the path is correct.");
					return RotateTextureCounterClockwiseFallback(originalTexture);
				}
				s_RotationMaterial = new Material(val);
			}
			int height = ((Texture)originalTexture).height;
			int width = ((Texture)originalTexture).width;
			RenderTexture temporary = RenderTexture.GetTemporary(height, width, 0, (RenderTextureFormat)7);
			Graphics.Blit((Texture)(object)originalTexture, temporary, s_RotationMaterial);
			Texture2D val2 = new Texture2D(height, width, originalTexture.format, false);
			RenderTexture.active = temporary;
			val2.ReadPixels(new Rect(0f, 0f, (float)height, (float)width), 0, 0);
			val2.Apply();
			RenderTexture.active = null;
			RenderTexture.ReleaseTemporary(temporary);
			return val2;
		}

		public static void CleanUp()
		{
			if ((Object)(object)s_RotationMaterial != (Object)null)
			{
				Object.DestroyImmediate((Object)(object)s_RotationMaterial);
				s_RotationMaterial = null;
			}
		}

		public static Texture2D RotateTextureCounterClockwiseFallback(Texture2D originalTexture)
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Expected O, but got Unknown
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			int width = ((Texture)originalTexture).width;
			int height = ((Texture)originalTexture).height;
			Texture2D val = new Texture2D(height, width);
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					val.SetPixel(height - 1 - i, j, originalTexture.GetPixel(j, i));
				}
			}
			val.Apply();
			return val;
		}

		public static string GetMapId(string url)
		{
			string result = null;
			string pattern = "\\/[a-fA-F0-9]{24}\\/([a-fA-F0-9]{24})\\/";
			Match match = Regex.Match(url, pattern);
			if (match.Success && match.Groups.Count > 1)
			{
				result = match.Groups[1].Value;
			}
			return result;
		}

		public static void UpdateMeshPoseAndRotation(GameObject mesh, MapSet mapSet, string mapId)
		{
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
			if (!((Object)(object)mesh == (Object)null) || mapId != null || mapSet != null)
			{
				RelativePose relativePose = (from data in mapSet.mapSetData
					where data.map._id == mapId
					select data.relativePose).FirstOrDefault();
				if (relativePose != null)
				{
					Vector3 localPosition = new Vector3(relativePose.position.x, relativePose.position.y, relativePose.position.z);
					Quaternion localRotation = new Quaternion(relativePose.rotation.qx, relativePose.rotation.qy, relativePose.rotation.qz, relativePose.rotation.qw);
					mesh.transform.localPosition = localPosition;
					mesh.transform.localRotation = localRotation;
				}
			}
		}

		public static bool IsImageBlur(Texture2D texture, float threshold)
		{
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004d: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_006a: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)texture == (Object)null)
			{
				return false;
			}
			int width = ((Texture)texture).width;
			int height = ((Texture)texture).height;
			Color[] pixels = texture.GetPixels();
			float[,] array = new float[width, height];
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					Color val = pixels[i * width + j];
					float num = val.r * 0.299f + val.g * 0.587f + val.b * 0.114f;
					array[j, i] = num * 255f;
				}
			}
			float num2 = 0f;
			float num3 = 0f;
			int num4 = 0;
			for (int k = 1; k < height - 1; k++)
			{
				for (int l = 1; l < width - 1; l++)
				{
					float num5 = array[l - 1, k] + array[l + 1, k] + array[l, k - 1] + array[l, k + 1] - 4f * array[l, k];
					num2 += num5;
					num3 += num5 * num5;
					num4++;
				}
			}
			if (num4 == 0)
			{
				return false;
			}
			float num6 = num2 / (float)num4;
			float num7 = num3 / (float)num4 - num6 * num6;
			return num7 < threshold;
		}

		public static bool WriteToFile(string a_FileName, string a_FileContents)
		{
			string text = Path.Combine(Application.persistentDataPath, a_FileName);
			try
			{
				File.WriteAllText(text, a_FileContents);
				return true;
			}
			catch (Exception arg)
			{
				Debug.LogError((object)$"Failed to write to {text} with exception {arg}");
				return false;
			}
		}

		public static string LoadFromFile(string a_FileName)
		{
			string text = Path.Combine(Application.persistentDataPath, a_FileName);
			try
			{
				return File.ReadAllText(text);
			}
			catch (Exception arg)
			{
				Debug.LogError((object)$"Failed to read from {text} with exception {arg}");
				return "";
			}
		}

		public static void DeleteFile(string filePath)
		{
			try
			{
				if (File.Exists(filePath))
				{
					File.Delete(filePath);
				}
			}
			catch (Exception ex)
			{
				Debug.LogError((object)("Exception while deleting file: " + ex));
			}
		}

		public static void DeleteDirectory(string directory)
		{
			try
			{
				if (Directory.Exists(directory))
				{
					Directory.Delete(directory, recursive: true);
				}
			}
			catch (Exception arg)
			{
				Debug.LogError((object)$"Exception while deleting {directory}: {arg}");
			}
		}

		public static void CreateDirectory(string directory)
		{
			try
			{
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}
			}
			catch (Exception arg)
			{
				Debug.LogError((object)$"Exception while creating {directory}: {arg}");
			}
		}
	}
}
