using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class ShaderProgressAnimator : MonoBehaviour
	{
		[Header("Animation Settings")]
		public bool loop = true;

		public float delayBetweenLoops = 10f;

		public bool keepMaterialActiveInPause = true;

		[Header("Auto Size Settings")]
		[Tooltip("If true, maxRadius and animationDuration will be calculated from mesh bounds")]
		public bool autoSizeFromMesh = true;

		[Tooltip("Extra padding to add to the calculated radius")]
		public float radiusPadding = 5f;

		[Header("Duration Scaling (defines the curve for duration calculation)")]
		[Tooltip("Reference small radius value")]
		public float smallRadius = 20f;

		[Tooltip("Duration for small radius")]
		public float smallRadiusDuration = 5f;

		[Tooltip("Reference large radius value")]
		public float largeRadius = 200f;

		[Tooltip("Duration for large radius")]
		public float largeRadiusDuration = 20f;

		[Header("Manual Settings (used when autoSizeFromMesh is false)")]
		[Tooltip("Manual animation duration in seconds")]
		public float animationDuration = 30f;

		[Tooltip("Manual max radius")]
		public float maxRadius = 150f;

		[Header("Center Settings")]
		public Vector3 center = Vector3.zero;

		[Header("Camera Center Settings")]
		[Tooltip("If true, the radial effect center will be set to the camera's position")]
		public bool useCameraAsCenter = true;

		[Tooltip("Reference to the camera. If null, Camera.main will be used")]
		public Camera targetCamera;

		private Material[] materials;

		private Renderer[] allRenderers;

		private float startTime;

		private float pauseEndTime;

		private bool isPaused;

		private bool isInitialized;

		private void Start()
		{
			Initialize();
		}

		private void OnEnable()
		{
			if (isInitialized && materials != null && materials.Length != 0)
			{
				UpdateCenterFromCamera();
				startTime = Time.time;
				isPaused = false;
				SetProgressOnAllMaterials(0f);
			}
		}

		private void UpdateCenterFromCamera()
		{
			//IL_0045: Unknown result type (might be due to invalid IL or missing references)
			//IL_004a: Unknown result type (might be due to invalid IL or missing references)
			//IL_004f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			if (useCameraAsCenter)
			{
				Camera val = (Camera)(((Object)(object)targetCamera != (Object)null) ? ((object)targetCamera) : ((object)Camera.main));
				if ((Object)(object)val != (Object)null)
				{
					center = ((Component)this).transform.InverseTransformPoint(((Component)val).transform.position);
					SetCenterOnAllMaterials(center);
				}
			}
		}

		private void SetCenterOnAllMaterials(Vector3 newCenter)
		{
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			if (materials == null)
			{
				return;
			}
			Material[] array = materials;
			foreach (Material val in array)
			{
				if ((Object)(object)val != (Object)null)
				{
					val.SetVector("_Center", new Vector4(newCenter.x, newCenter.y, newCenter.z, 0f));
				}
			}
		}

		private void CalculateSizeFromMeshBounds()
		{
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_009d: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			if (!autoSizeFromMesh || allRenderers == null || allRenderers.Length == 0)
			{
				return;
			}
			Bounds val = default(Bounds);
			bool flag = false;
			Renderer[] array = allRenderers;
			foreach (Renderer val2 in array)
			{
				if ((Object)(object)val2 != (Object)null)
				{
					if (!flag)
					{
						val = val2.bounds;
						flag = true;
					}
					else
					{
						val.Encapsulate(val2.bounds);
					}
				}
			}
			if (flag)
			{
				Vector3 size = val.size;
				float magnitude = size.magnitude;
				maxRadius = magnitude + radiusPadding;
				animationDuration = CalculateDurationFromRadius(maxRadius);
			}
		}

		private float CalculateDurationFromRadius(float radius)
		{
			radius = Mathf.Max(radius, 1f);
			float num = Mathf.Log(largeRadiusDuration / smallRadiusDuration) / Mathf.Log(largeRadius / smallRadius);
			float num2 = smallRadiusDuration / Mathf.Pow(smallRadius, num);
			return num2 * Mathf.Pow(radius, num);
		}

		private void Initialize()
		{
			//IL_0224: Unknown result type (might be due to invalid IL or missing references)
			//IL_0229: Unknown result type (might be due to invalid IL or missing references)
			//IL_01db: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
			if (isInitialized)
			{
				return;
			}
			Renderer component = ((Component)this).GetComponent<Renderer>();
			if ((Object)(object)component == (Object)null)
			{
				Renderer[] componentsInChildren = ((Component)this).GetComponentsInChildren<Renderer>();
				if (componentsInChildren != null && componentsInChildren.Length != 0)
				{
					allRenderers = componentsInChildren;
					int num = 0;
					Renderer[] array = componentsInChildren;
					foreach (Renderer val in array)
					{
						if ((Object)(object)val != (Object)null && (Object)(object)val.sharedMaterial != (Object)null)
						{
							num++;
						}
					}
					if (num > 0)
					{
						materials = (Material[])(object)new Material[num];
						int num2 = 0;
						Renderer[] array2 = componentsInChildren;
						foreach (Renderer val2 in array2)
						{
							if ((Object)(object)val2 != (Object)null && (Object)(object)val2.sharedMaterial != (Object)null)
							{
								materials[num2] = val2.material;
								num2++;
							}
						}
					}
				}
			}
			else if ((Object)(object)component.sharedMaterial != (Object)null)
			{
				allRenderers = (Renderer[])(object)new Renderer[1] { component };
				materials = (Material[])(object)new Material[1] { component.material };
			}
			if (materials == null || materials.Length == 0)
			{
				Debug.LogWarning((object)("[ShaderProgressAnimator] No valid materials found on " + ((Object)((Component)this).gameObject).name + ". Disabling animator."));
				((Behaviour)this).enabled = false;
				return;
			}
			CalculateSizeFromMeshBounds();
			if (useCameraAsCenter)
			{
				Camera val3 = (Camera)(((Object)(object)targetCamera != (Object)null) ? ((object)targetCamera) : ((object)Camera.main));
				if ((Object)(object)val3 != (Object)null)
				{
					center = ((Component)this).transform.InverseTransformPoint(((Component)val3).transform.position);
				}
			}
			startTime = Time.time;
			Material[] array3 = materials;
			foreach (Material val4 in array3)
			{
				if ((Object)(object)val4 != (Object)null)
				{
					val4.SetVector("_Center", new Vector4(center.x, center.y, center.z, 0f));
					val4.SetFloat("_Radius", maxRadius);
					val4.SetFloat("_Progress", 0f);
				}
			}
			isInitialized = true;
		}

		private void Update()
		{
			if (materials == null || materials.Length == 0)
			{
				((Behaviour)this).enabled = false;
				return;
			}
			if (isPaused)
			{
				if (keepMaterialActiveInPause)
				{
					SetProgressOnAllMaterials(1f);
				}
				if (Time.time >= pauseEndTime)
				{
					UpdateCenterFromCamera();
					isPaused = false;
					startTime = Time.time;
					SetProgressOnAllMaterials(0f);
				}
				return;
			}
			float num = (Time.time - startTime) / animationDuration;
			SetProgressOnAllMaterials(Mathf.Clamp01(num));
			if (!(num >= 1f))
			{
				return;
			}
			if (loop)
			{
				if (delayBetweenLoops > 0f)
				{
					isPaused = true;
					pauseEndTime = Time.time + delayBetweenLoops;
				}
				else
				{
					UpdateCenterFromCamera();
					startTime = Time.time;
					SetProgressOnAllMaterials(0f);
				}
			}
			else
			{
				((Behaviour)this).enabled = false;
			}
		}

		private void SetProgressOnAllMaterials(float progress)
		{
			if (materials == null)
			{
				return;
			}
			Material[] array = materials;
			foreach (Material val in array)
			{
				if ((Object)(object)val != (Object)null)
				{
					val.SetFloat("_Progress", progress);
				}
			}
		}

		public void SetLooping(bool enableLoop)
		{
			loop = enableLoop;
			if (loop && !((Behaviour)this).enabled)
			{
				((Behaviour)this).enabled = true;
				startTime = Time.time;
			}
		}

		public void SetShaderParameters(Vector3 newCenter, float newRadius)
		{
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			center = newCenter;
			maxRadius = newRadius;
			if (materials == null)
			{
				return;
			}
			Material[] array = materials;
			foreach (Material val in array)
			{
				if ((Object)(object)val != (Object)null)
				{
					val.SetVector("_Center", new Vector4(center.x, center.y, center.z, 0f));
					val.SetFloat("_Radius", maxRadius);
				}
			}
		}

		public void RestartAnimation()
		{
			UpdateCenterFromCamera();
			startTime = Time.time;
			isPaused = false;
			((Behaviour)this).enabled = true;
			SetProgressOnAllMaterials(0f);
		}

		private void OnDestroy()
		{
			if (materials == null)
			{
				return;
			}
			Material[] array = materials;
			foreach (Material val in array)
			{
				if ((Object)(object)val != (Object)null)
				{
					Object.Destroy((Object)(object)val);
				}
			}
			materials = null;
		}
	}
}
