using UnityEngine;

namespace JJ.Vps.WorldAlignment
{
    public class NoOpVpsWorldAligner : MonoBehaviour, IVpsWorldAligner
    {
        public void ApplyLocalization(VpsLocalizationResult result)
        {
        }
    }
}
