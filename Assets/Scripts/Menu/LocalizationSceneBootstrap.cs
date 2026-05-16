using MultiSet;
using TMPro;
using UnityEngine;
using JJ.Vps;

namespace JJ.Menu
{
    public class LocalizationSceneBootstrap : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private SelectedMapContext selectedMapContext;
        [SerializeField] private SingleFrameLocalizationManager singleFrameLocalizationManager;
        [SerializeField] private VpsLocalizationController vpsLocalizationController;

        [Header("Optional UI")]
        [SerializeField] private TMP_Text selectedMapNameText;

        [Header("Automation")]
        [SerializeField] private bool triggerLocalizationOnStart;

        private void Awake()
        {
            if (selectedMapContext == null)
            {
                selectedMapContext = SelectedMapContext.Instance;
            }

            if (singleFrameLocalizationManager == null)
            {
                singleFrameLocalizationManager = FindFirstObjectByType<SingleFrameLocalizationManager>();
            }

            if (vpsLocalizationController == null)
            {
                vpsLocalizationController = FindFirstObjectByType<VpsLocalizationController>();
            }

            ApplySelectedMap();
        }

        private void Start()
        {
            if (triggerLocalizationOnStart)
            {
                vpsLocalizationController?.TriggerLocalization();
            }
        }

        public void ApplySelectedMap()
        {
            if (selectedMapContext == null || !selectedMapContext.HasSelection)
            {
                UpdateSelectedMapText(null);
                return;
            }

            if (singleFrameLocalizationManager != null)
            {
                singleFrameLocalizationManager.localizationType = selectedMapContext.LocalizationType;
                singleFrameLocalizationManager.mapOrMapsetCode = selectedMapContext.MapCode;
            }

            UpdateSelectedMapText(selectedMapContext.MapName);
        }

        private void UpdateSelectedMapText(string mapName)
        {
            if (selectedMapNameText == null)
            {
                return;
            }

            selectedMapNameText.text = string.IsNullOrWhiteSpace(mapName) ? "Map: Not Selected" : $"Map: {mapName}";
        }
    }
}
