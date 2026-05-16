using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class TimerManager : MonoBehaviour
	{
		public TMP_Text timerText;

		public Image background;

		public GameObject minDurationAlert;

		public UnityEvent onMaxDurationReached;

		public Slider timerSlider;

		private float elapsedTime = 0f;

		private float speed = 1f;

		private bool isRunning = false;

		private const float MIN_DURATION = 10f;

		private const float MAX_DURATION = 600f;

		private void Start()
		{
			if ((Object)(object)timerSlider != (Object)null)
			{
				((Component)timerSlider).gameObject.SetActive(false);
				timerSlider.minValue = 0f;
				timerSlider.maxValue = 600f;
				timerSlider.value = 0f;
			}
		}

		private void Update()
		{
			if (isRunning)
			{
				UpdateTimer();
				CheckMaxDuration();
			}
		}

		private void UpdateTimer()
		{
			elapsedTime += Time.deltaTime * speed;
			int num = Mathf.FloorToInt(elapsedTime / 3600f);
			int num2 = Mathf.FloorToInt(elapsedTime % 3600f / 60f);
			int num3 = Mathf.FloorToInt(elapsedTime % 60f);
			string text = $"{num2:00}:{num3:00}";
			if (num > 0)
			{
				text = $"{num:00}:{num2:00}:{num3:00}";
			}
			timerText.text = text + " / 10:00";
			if ((Object)(object)timerSlider != (Object)null)
			{
				timerSlider.value = elapsedTime;
			}
		}

		private void CheckMaxDuration()
		{
			if (elapsedTime >= 600f)
			{
				Debug.LogWarning((object)"Maximum scan duration (10 minutes) reached!");
				UnityEvent obj = onMaxDurationReached;
				if (obj != null)
				{
					obj.Invoke();
				}
				StopTimer();
			}
		}

		public void StartTimer()
		{
			if ((Object)(object)timerText == (Object)null || (Object)(object)background == (Object)null)
			{
				Debug.LogError((object)"TimerManager: timerText or background is not assigned.");
				return;
			}
			if ((Object)(object)timerSlider != (Object)null)
			{
				((Component)timerSlider).gameObject.SetActive(true);
			}
			if ((Object)(object)minDurationAlert != (Object)null)
			{
				minDurationAlert.SetActive(false);
			}
			((Behaviour)timerText).enabled = true;
			((Behaviour)background).enabled = true;
			isRunning = true;
		}

		public void StopTimer()
		{
			isRunning = false;
			if (elapsedTime < 10f && (Object)(object)minDurationAlert != (Object)null)
			{
				minDurationAlert.SetActive(true);
			}
			elapsedTime = 0f;
			((Behaviour)timerText).enabled = false;
			((Behaviour)background).enabled = false;
			if ((Object)(object)timerSlider != (Object)null)
			{
				timerSlider.value = 0f;
				((Component)timerSlider).gameObject.SetActive(false);
			}
		}
	}
}
