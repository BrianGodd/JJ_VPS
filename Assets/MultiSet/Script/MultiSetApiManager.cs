using UnityEngine;

namespace MultiSet
{
	public class MultiSetApiManager
	{
		public delegate void ActionCallback(bool success, string data, long statusCode);

		public static void ApiRequest(Method method, string endPoint, string payload, ActionCallback onComplete = null, bool authRequired = false, WWWForm formData = null)
		{
			MultiSetHttpClient.CallWebAPI(method, endPoint, payload, formData, delegate(bool success, string data, long statusCode)
			{
				onComplete?.Invoke(success, data, statusCode);
			}, authRequired);
		}

		public static void LocalizeRequestMultiQuery(WWWForm formData, ActionCallback onComplete = null)
		{
			ApiRequest(Method.POST, "/v1/vps/map/multi-image-query", null, onComplete, authRequired: true, formData);
		}

		public static void LocalizeRequest(WWWForm formData, ActionCallback onComplete = null)
		{
			ApiRequest(Method.POST, "/v1/vps/map/query-form", null, onComplete, authRequired: true, formData);
		}

		public static void GetMapDetails(string mapId, ActionCallback onComplete = null)
		{
			ApiRequest(Method.GET, "/v1/vps/map/" + mapId, null, onComplete, authRequired: true);
		}

		public static void GetFileUrl(string endPoint, ActionCallback onComplete = null)
		{
			ApiRequest(Method.GET, "/v1/file?key=" + endPoint, null, onComplete, authRequired: true);
		}

		public static void GetMapSetDetails(string mapSetCode, ActionCallback onComplete = null)
		{
			ApiRequest(Method.GET, "/v1/vps/map-set/" + mapSetCode, null, onComplete, authRequired: true);
		}

		public static void UpdateMapSetData(string dataId, string dataPayload, ActionCallback onComplete = null)
		{
			ApiRequest(Method.PUT, "/v1/vps/map-set/data/" + dataId, dataPayload, onComplete, authRequired: true);
		}

		public static void GetPlanDetails(ActionCallback onComplete = null)
		{
			ApiRequest(Method.GET, "/v1/account/plan-details", null, onComplete, authRequired: true);
		}

		public static void GetObjectDetails(string objectId, ActionCallback onComplete = null)
		{
			ApiRequest(Method.GET, "/v1/vps/object/" + objectId, null, onComplete, authRequired: true);
		}

		public static void LocalizeObject(WWWForm formData, ActionCallback onComplete = null)
		{
			ApiRequest(Method.POST, "/v1/vps/object/query", null, onComplete, authRequired: true, formData);
		}

		public static void CreateMap(string mapPayload, ActionCallback onComplete = null)
		{
			ApiRequest(Method.POST, "/v1/vps/map", mapPayload, onComplete, authRequired: true);
		}

		public static void ReUploadMapData(string mapId, ActionCallback onComplete = null)
		{
			ApiRequest(Method.POST, "/v1/vps/map/re-upload/" + mapId, null, onComplete, authRequired: true);
		}

		public static void UploadSimulationData(WWWForm formData, ActionCallback onComplete = null)
		{
			ApiRequest(Method.POST, "/v1/simulation-data", null, onComplete, authRequired: true, formData);
		}

		public static void GetSimulationData(ActionCallback onComplete = null)
		{
			ApiRequest(Method.GET, "/v1/simulation-data?page=1&limit=50", null, onComplete, authRequired: true);
		}
	}
}
