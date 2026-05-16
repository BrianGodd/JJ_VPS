using UnityEngine;

namespace MultiSet
{
	public static class ApiErrorHelper
	{
		public static string ParseErrorMessage(string data)
		{
			if (string.IsNullOrEmpty(data))
			{
				return "Unknown error";
			}
			try
			{
				ErrorJSON errorJSON = JsonUtility.FromJson<ErrorJSON>(data);
				return errorJSON?.error ?? errorJSON?.message ?? data;
			}
			catch
			{
				return data;
			}
		}
	}
}
