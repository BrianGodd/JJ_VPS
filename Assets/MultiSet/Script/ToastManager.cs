using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class ToastManager : MonoBehaviour
	{
		public Text toastText;

		public CanvasGroup canvasGroup;

		public float fadeDuration = 0.5f;

		public float displayDuration = 2f;

		public static ToastManager Instance { get; private set; }

		private void Awake()
		{
			if ((Object)(object)Instance == (Object)null)
			{
				Instance = this;
			}
		}

		private void Start()
		{
			canvasGroup.alpha = 0f;
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;
		}

		public void ShowToast(string message)
		{
			toastText.text = message;
			((MonoBehaviour)this).StartCoroutine(FadeInAndOut());
		}

		public void ShowAlert(string alertText)
		{
			toastText.text = alertText;
			((MonoBehaviour)this).StartCoroutine(FadeInAndOut());
		}

		private IEnumerator FadeInAndOut()
		{
			canvasGroup.interactable = true;
			canvasGroup.blocksRaycasts = true;
			yield return ((MonoBehaviour)this).StartCoroutine(Fade(0f, 1f));
			yield return (object)new WaitForSeconds(displayDuration);
			yield return ((MonoBehaviour)this).StartCoroutine(Fade(1f, 0f));
			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;
		}

		private IEnumerator Fade(float startAlpha, float endAlpha)
		{
			float elapsedTime = 0f;
			while (elapsedTime < fadeDuration)
			{
				canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeDuration);
				elapsedTime += Time.deltaTime;
				yield return null;
			}
			canvasGroup.alpha = endAlpha;
		}
	}
}
