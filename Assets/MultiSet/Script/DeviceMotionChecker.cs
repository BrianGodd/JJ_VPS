using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class DeviceMotionChecker : MonoBehaviour
	{
		public MappingManager mapper;

		[Space(20f)]
		[Tooltip("Enable this to show alert to user if moving too fast or rotating too fast.")]
		[Header("Movement Alert Settings")]
		public bool showMovementAlert = true;

		[Header("Thresholds")]
		[Tooltip("Linear speed threshold in units per second (e.g., meters/second) above which movement is considered 'too fast'.")]
		public float movementSpeedThreshold = 4f;

		[Tooltip("Angular speed threshold in degrees per second above which rotation is considered 'too fast'.")]
		public float rotationSpeedThreshold = 80f;

		private float smoothingFactor = 0.4f;

		[Header("AR Camera")]
		public Transform arCameraTransform;

		private Vector3 previousPosition;

		private Quaternion previousRotation;

		private float smoothedTranslationSpeed = 0f;

		private float smoothedRotationSpeed = 0f;

		private bool showingAlert = false;

		[Header("UI Elements")]
		public GameObject motionAlertDialog;

		private bool isCheckingMovement = false;

		private float lastTime;

		private void Start()
		{
			//IL_002e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_003f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			if ((Object)(object)arCameraTransform == (Object)null)
			{
				Debug.LogError((object)"AR Camera Transform is not assigned!");
				((Behaviour)this).enabled = false;
			}
			else
			{
				previousPosition = arCameraTransform.position;
				previousRotation = arCameraTransform.rotation;
				lastTime = Time.time;
			}
		}

		private void Update()
		{
			if ((!((Object)(object)mapper == (Object)null) || !((Object)(object)arCameraTransform != (Object)null)) && mapper.IsMapping && showMovementAlert)
			{
				CheckCameraMovementAndRotation();
			}
		}

		private IEnumerator StartCheckingMovementAfterDelay(float delay)
		{
			yield return (object)new WaitForSeconds(delay);
			isCheckingMovement = true;
			lastTime = Time.time;
		}

		private void CheckCameraMovementAndRotation()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			//IL_0052: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			//IL_0062: Unknown result type (might be due to invalid IL or missing references)
			//IL_0159: Unknown result type (might be due to invalid IL or missing references)
			//IL_015a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0160: Unknown result type (might be due to invalid IL or missing references)
			//IL_0161: Unknown result type (might be due to invalid IL or missing references)
			float time = Time.time;
			Vector3 position = arCameraTransform.position;
			Quaternion rotation = arCameraTransform.rotation;
			float num = time - lastTime;
			if (num < 0.01f)
			{
				num = 0.01f;
			}
			if (num > 1f)
			{
				num = 1f;
			}
			float num2 = Vector3.Distance(position, previousPosition) / num;
			float num3 = Quaternion.Angle(rotation, previousRotation);
			num3 = Mathf.Clamp(num3, 0f, 180f);
			float num4 = num3 / num;
			smoothedTranslationSpeed = Mathf.Lerp(smoothedTranslationSpeed, num2, smoothingFactor);
			smoothedRotationSpeed = Mathf.Lerp(smoothedRotationSpeed, num4, smoothingFactor);
			smoothedTranslationSpeed = Mathf.Clamp(smoothedTranslationSpeed, 0f, movementSpeedThreshold * 2f);
			smoothedRotationSpeed = Mathf.Clamp(smoothedRotationSpeed, 0f, rotationSpeedThreshold * 2f);
			bool flag = smoothedTranslationSpeed > movementSpeedThreshold;
			bool flag2 = smoothedRotationSpeed > rotationSpeedThreshold;
			if (!isCheckingMovement)
			{
				((MonoBehaviour)this).StartCoroutine(StartCheckingMovementAfterDelay(2f));
			}
			else if (flag || flag2)
			{
				ShowAlert();
			}
			previousPosition = position;
			previousRotation = rotation;
			lastTime = time;
		}

		private void ShowAlert()
		{
			if (!showingAlert)
			{
				((MonoBehaviour)this).StartCoroutine(ShowAlertUI());
			}
		}

		private IEnumerator ShowAlertUI()
		{
			showingAlert = true;
			GameObject obj = motionAlertDialog;
			if (obj != null)
			{
				obj.SetActive(true);
			}
			yield return (object)new WaitForSeconds(2f);
			HideAlert();
			showingAlert = false;
		}

		private void HideAlert()
		{
			GameObject obj = motionAlertDialog;
			if (obj != null)
			{
				obj.SetActive(false);
			}
		}
	}
}
