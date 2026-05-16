using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiSet
{
	[ExecuteInEditMode]
	public class MapDataReference : MonoBehaviour
	{
		[Header("Map Identification")]
		[Tooltip("The _id of the MapSetData entry")]
		public string dataId;

		[Tooltip("The _id of the Map")]
		public string mapId;

		[Tooltip("The map code")]
		public string mapCode;

		[Tooltip("The map name")]
		public string mapName;

		[Header("MapSet Information")]
		[Tooltip("The MapSet code this map belongs to")]
		public string mapSetCode;

		[Tooltip("The MapSet name")]
		public string mapSetName;

		[Header("Current Relative Pose (Read-Only)")]
		[SerializeField]
		private Vector3 currentPosition;

		[SerializeField]
		private Quaternion currentRotation;

		[Header("Original Relative Pose (From Server)")]
		[SerializeField]
		private Vector3 originalPosition;

		[SerializeField]
		private Quaternion originalRotation;

		[Header("State")]
		[Tooltip("Indicates if the pose has been modified from the original")]
		public bool hasUnsavedChanges = false;

		[Tooltip("Indicates if an API update is in progress")]
		public bool isUpdating = false;

		[Tooltip("Last update status message")]
		public string lastUpdateStatus = "";

		[HideInInspector]
		public MapsetAlignmentManager parentManager;

		public Action<UnityEngine.Object> onSetDirty;

		private Vector3 previousPosition;

		private Quaternion previousRotation;

		public Vector3 CurrentPosition => currentPosition;

		public Quaternion CurrentRotation => currentRotation;

		public Vector3 OriginalPosition => originalPosition;

		public Quaternion OriginalRotation => originalRotation;

		public void Initialize(MapSetData mapSetData, MapSet mapSet, MapsetAlignmentManager manager)
		{
			//IL_015d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0162: Unknown result type (might be due to invalid IL or missing references)
			//IL_016e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0173: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0143: Unknown result type (might be due to invalid IL or missing references)
			//IL_0148: Unknown result type (might be due to invalid IL or missing references)
			if (mapSetData == null || mapSetData.map == null)
			{
				Debug.LogError((object)"MapDataReference: Cannot initialize with null MapSetData or Map");
				return;
			}
			dataId = mapSetData._id;
			mapId = mapSetData.map._id;
			mapCode = mapSetData.map.mapCode;
			mapName = mapSetData.map.mapName;
			if (mapSet != null)
			{
				mapSetCode = mapSet.mapSetCode;
				mapSetName = mapSet.name;
			}
			parentManager = manager;
			if (mapSetData.relativePose != null)
			{
				if (mapSetData.relativePose.position != null)
				{
					originalPosition = new Vector3(mapSetData.relativePose.position.x, mapSetData.relativePose.position.y, mapSetData.relativePose.position.z);
				}
				if (mapSetData.relativePose.rotation != null)
				{
					originalRotation = new Quaternion(mapSetData.relativePose.rotation.qx, mapSetData.relativePose.rotation.qy, mapSetData.relativePose.rotation.qz, mapSetData.relativePose.rotation.qw);
				}
			}
			UpdateCurrentPoseFromTransform();
			previousPosition = ((Component)this).transform.localPosition;
			previousRotation = ((Component)this).transform.localRotation;
			hasUnsavedChanges = false;
		}

		private void OnEnable()
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			previousPosition = ((Component)this).transform.localPosition;
			previousRotation = ((Component)this).transform.localRotation;
		}

		private void Update()
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			if (!Application.isPlaying && (((Component)this).transform.localPosition != previousPosition || ((Component)this).transform.localRotation != previousRotation))
			{
				UpdateCurrentPoseFromTransform();
				CheckForChanges();
				previousPosition = ((Component)this).transform.localPosition;
				previousRotation = ((Component)this).transform.localRotation;
			}
		}

		public void UpdateCurrentPoseFromTransform()
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			currentPosition = ((Component)this).transform.localPosition;
			currentRotation = ((Component)this).transform.localRotation;
		}

		private void CheckForChanges()
		{
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			float num = 0.0001f;
			float num2 = 0.0001f;
			bool flag = Vector3.Distance(currentPosition, originalPosition) > num;
			bool flag2 = Quaternion.Angle(currentRotation, originalRotation) > num2;
			hasUnsavedChanges = flag || flag2;
		}

		public RelativePose GetRelativePosePayload()
		{
			return new RelativePose
			{
				position = new Position
				{
					x = currentPosition.x,
					y = currentPosition.y,
					z = currentPosition.z
				},
				rotation = new RotationData
				{
					qx = currentRotation.x,
					qy = currentRotation.y,
					qz = currentRotation.z,
					qw = currentRotation.w
				}
			};
		}

		public void ResetToOriginalPose()
		{
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			((Component)this).transform.localPosition = originalPosition;
			((Component)this).transform.localRotation = originalRotation;
			UpdateCurrentPoseFromTransform();
			hasUnsavedChanges = false;
			onSetDirty?.Invoke((Object)(object)this);
		}

		public void OnPoseUpdateSuccess()
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			originalPosition = currentPosition;
			originalRotation = currentRotation;
			hasUnsavedChanges = false;
			isUpdating = false;
			lastUpdateStatus = "Update successful";
			onSetDirty?.Invoke((Object)(object)this);
		}

		public void OnPoseUpdateFailed(string errorMessage)
		{
			isUpdating = false;
			lastUpdateStatus = "Update failed: " + errorMessage;
			onSetDirty?.Invoke((Object)(object)this);
		}
	}
}
