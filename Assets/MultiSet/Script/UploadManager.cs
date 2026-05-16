using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class UploadManager : MonoBehaviour
	{
		public static UploadManager Instance { get; private set; }

		public event Action<string, float> OnUploadProgressChanged;

		public event Action<string, bool, string> OnUploadCompleted;

		private Coroutine m_activeUpload;

		private string m_activeUploadId;

		private void Awake()
		{
			if ((Object)(object)Instance != (Object)null && (Object)(object)Instance != (Object)(object)this)
			{
				Object.Destroy((Object)(object)((Component)this).gameObject);
				return;
			}
			Instance = this;
		}

		private void OnDestroy()
		{
			if ((Object)(object)Instance == (Object)(object)this)
			{
				Instance = null;
			}
		}

		public void StartBackgroundUpload(string filePath, string uploadUrl, string uploadId)
		{
			if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
			{
				OnUploadCompleted?.Invoke(uploadId, false, "Upload file not found.");
				return;
			}
			if (string.IsNullOrEmpty(uploadUrl))
			{
				OnUploadCompleted?.Invoke(uploadId, false, "Upload URL is missing.");
				return;
			}
			if (m_activeUpload != null)
			{
				StopCoroutine(m_activeUpload);
				m_activeUpload = null;
			}
			m_activeUploadId = uploadId;
			m_activeUpload = StartCoroutine(UploadFileCoroutine(filePath, uploadUrl, uploadId));
		}

		private IEnumerator UploadFileCoroutine(string filePath, string uploadUrl, string uploadId)
		{
			byte[] fileData;
			try
			{
				fileData = File.ReadAllBytes(filePath);
			}
			catch (Exception ex)
			{
				m_activeUpload = null;
				OnUploadCompleted?.Invoke(uploadId, false, ex.Message);
				yield break;
			}

			UnityWebRequest request = UnityWebRequest.Put(uploadUrl, fileData);
			try
			{
				request.SetRequestHeader("Content-Type", "application/zip");
				UnityWebRequestAsyncOperation operation = request.SendWebRequest();
				while (!((AsyncOperation)operation).isDone)
				{
					OnUploadProgressChanged?.Invoke(uploadId, request.uploadProgress);
					yield return null;
				}
				OnUploadProgressChanged?.Invoke(uploadId, 1f);
				bool success = (int)request.result == 1;
				OnUploadCompleted?.Invoke(uploadId, success, success ? null : request.error);
			}
			finally
			{
				((IDisposable)request)?.Dispose();
				m_activeUpload = null;
				m_activeUploadId = null;
			}
		}
	}
}
