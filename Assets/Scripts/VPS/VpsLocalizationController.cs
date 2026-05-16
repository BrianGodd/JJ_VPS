using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace JJ.Vps
{
    public class VpsLocalizationController : MonoBehaviour
    {
        [Header("Routing")]
        [SerializeField] private VpsProviderType providerType = VpsProviderType.MultiSet;
        [SerializeField] private MonoBehaviour imageSourceBehaviour;
        [SerializeField] private MonoBehaviour multiSetProviderBehaviour;
        [SerializeField] private MonoBehaviour immersalProviderBehaviour;
        [SerializeField] private MonoBehaviour worldAlignerBehaviour;

        [Header("Automation")]
        [SerializeField] private bool localizeOnStart;
        [SerializeField] private bool autoLocalize;
        [SerializeField, Min(0.1f)] private float autoLocalizeInterval = 1f;

        [Header("Events")]
        public UnityEvent OnLocalizationSuccess = new UnityEvent();
        public UnityEvent OnLocalizationFailure = new UnityEvent();

        private IVpsImageSource m_imageSource;
        private IVpsProvider m_multiSetProvider;
        private IVpsProvider m_immersalProvider;
        private IVpsWorldAligner m_worldAligner;
        private Coroutine m_autoLocalizeCoroutine;
        private bool m_previousAutoLocalize;

        public VpsProviderType ProviderType
        {
            get => providerType;
            set => providerType = value;
        }

        private void Awake()
        {
            m_imageSource = imageSourceBehaviour as IVpsImageSource;
            m_multiSetProvider = multiSetProviderBehaviour as IVpsProvider;
            m_immersalProvider = immersalProviderBehaviour as IVpsProvider;
            m_worldAligner = worldAlignerBehaviour as IVpsWorldAligner;
        }

        private void OnEnable()
        {
            Subscribe(m_multiSetProvider);
            Subscribe(m_immersalProvider);
            m_previousAutoLocalize = autoLocalize;

            if (localizeOnStart)
            {
                TriggerLocalization();
            }

            if (autoLocalize)
            {
                m_autoLocalizeCoroutine = StartCoroutine(AutoLocalizeLoop());
            }
        }

        private void OnDisable()
        {
            StopAutoLocalizeLoop();

            Unsubscribe(m_multiSetProvider);
            Unsubscribe(m_immersalProvider);
        }

        private void Update()
        {
            if (m_previousAutoLocalize == autoLocalize)
            {
                return;
            }

            SetAutoLocalize(autoLocalize);
        }

        public void TriggerLocalization()
        {
            IVpsProvider activeProvider = GetActiveProvider();
            if (m_imageSource == null)
            {
                Debug.LogError("VpsLocalizationController: image source does not implement IVpsImageSource.");
                return;
            }

            if (activeProvider == null)
            {
                Debug.LogError($"VpsLocalizationController: no provider configured for {providerType}.");
                return;
            }

            if (activeProvider.IsLocalizing)
            {
                return;
            }

            if (!m_imageSource.TryBuildLocalizationRequest(out VpsLocalizationRequest request))
            {
                return;
            }

            activeProvider.Localize(request);
        }

        public void SetAutoLocalize(bool enabled)
        {
            autoLocalize = enabled;
            m_previousAutoLocalize = enabled;

            if (!isActiveAndEnabled)
            {
                return;
            }

            if (enabled)
            {
                if (m_autoLocalizeCoroutine == null)
                {
                    m_autoLocalizeCoroutine = StartCoroutine(AutoLocalizeLoop());
                }
            }
            else
            {
                StopAutoLocalizeLoop();
            }
        }

        private IEnumerator AutoLocalizeLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(autoLocalizeInterval);
            while (autoLocalize)
            {
                TriggerLocalization();
                yield return wait;
            }

            m_autoLocalizeCoroutine = null;
        }

        private IVpsProvider GetActiveProvider()
        {
            return providerType == VpsProviderType.Immersal ? m_immersalProvider : m_multiSetProvider;
        }

        private void Subscribe(IVpsProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            provider.LocalizationSucceeded += HandleLocalizationSucceeded;
            provider.LocalizationFailed += HandleLocalizationFailed;
        }

        private void Unsubscribe(IVpsProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            provider.LocalizationSucceeded -= HandleLocalizationSucceeded;
            provider.LocalizationFailed -= HandleLocalizationFailed;
        }

        private void HandleLocalizationSucceeded(VpsLocalizationResult result)
        {
            if (!result.providerAppliedWorldPoseInternally)
            {
                m_worldAligner?.ApplyLocalization(result);
            }

            OnLocalizationSuccess.Invoke();
        }

        private void HandleLocalizationFailed(VpsLocalizationResult result)
        {
            OnLocalizationFailure.Invoke();
        }

        private void StopAutoLocalizeLoop()
        {
            if (m_autoLocalizeCoroutine != null)
            {
                StopCoroutine(m_autoLocalizeCoroutine);
                m_autoLocalizeCoroutine = null;
            }
        }
    }
}
