using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiSet
{
	[CreateAssetMenu(fileName = "MultiSetConfig", menuName = "Configuration/MultiSetConfig", order = 1)]
	public class MultiSetConfig : ScriptableObject
	{
		[Header("MultiSet SDK Credentials")]
		[Tooltip("Enter clientId for SDK authorization.")]
		public string clientId;

		[Tooltip("Enter clientSecret for SDK authorization.")]
		public string clientSecret;

		private static MultiSetConfig _instance;

		public static MultiSetConfig Instance
		{
			get
			{
				if ((Object)(object)_instance == (Object)null)
				{
					_instance = Resources.Load<MultiSetConfig>("MultiSetConfig");
					if ((Object)(object)_instance == (Object)null)
					{
						Debug.LogError((object)"MultiSetConfig.asset not found in Resources folder. Please ensure it exists.");
					}
				}
				return _instance;
			}
		}
	}
}
