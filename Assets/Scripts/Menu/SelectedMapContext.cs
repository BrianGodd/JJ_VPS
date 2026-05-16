using MultiSet;
using UnityEngine;

namespace JJ.Menu
{
    public class SelectedMapContext : MonoBehaviour
    {
        public static SelectedMapContext Instance { get; private set; }

        public string MapId { get; private set; }
        public string MapCode { get; private set; }
        public string MapName { get; private set; }
        public LocalizationType LocalizationType { get; private set; } = LocalizationType.Map;

        public bool HasSelection => !string.IsNullOrWhiteSpace(MapCode);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                MapId = Instance.MapId;
                MapCode = Instance.MapCode;
                MapName = Instance.MapName;
                LocalizationType = Instance.LocalizationType;

                Destroy(Instance.gameObject);
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetSelectedMap(string mapId, string mapCode, string mapName, LocalizationType localizationType = LocalizationType.Map)
        {
            MapId = mapId;
            MapCode = mapCode;
            MapName = mapName;
            LocalizationType = localizationType;
        }

        public void Clear()
        {
            MapId = null;
            MapCode = null;
            MapName = null;
            LocalizationType = LocalizationType.Map;
        }
    }
}
