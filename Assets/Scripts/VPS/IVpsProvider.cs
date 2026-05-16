using System;

namespace JJ.Vps
{
    public interface IVpsProvider
    {
        string ProviderId { get; }

        bool IsLocalizing { get; }

        event Action<VpsLocalizationResult> LocalizationSucceeded;

        event Action<VpsLocalizationResult> LocalizationFailed;

        void Localize(VpsLocalizationRequest request);
    }
}
