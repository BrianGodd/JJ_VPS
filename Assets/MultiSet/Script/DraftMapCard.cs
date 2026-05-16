using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace MultiSet
{
	public class DraftMapCard : MonoBehaviour
	{
		public DraftMap draftMap;

		public TMP_Text nameText;

		public TMP_Text dateText;

		public void SetDraftMapCard(DraftMap map, string creationDate)
		{
			draftMap = map;
			string text = UnityWebRequest.UnEscapeURL(draftMap.mapName);
			if ((Object)(object)nameText != (Object)null)
			{
				nameText.text = text;
			}
			else
			{
				Debug.LogError((object)"Map Name Text component missing!");
			}
			if ((Object)(object)dateText != (Object)null)
			{
				dateText.text = creationDate;
			}
			else
			{
				Debug.LogError((object)"Date Text component missing!");
			}
		}

		public void OnClickDraftCard()
		{
			EventManager<DraftMap>.TriggerEvent("DraftMapSelection", draftMap);
		}
	}
}
