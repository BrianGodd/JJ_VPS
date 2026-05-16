namespace JJ.Vps
{
    public interface IVpsImageSource
    {
        bool TryBuildLocalizationRequest(out VpsLocalizationRequest request);
    }
}
