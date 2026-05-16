using UnityEngine;

public class WorldSpaceBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    public void SetTargetCamera(Camera cameraToFace)
    {
        targetCamera = cameraToFace;
    }

    private void LateUpdate()
    {
        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return;
        }

        Vector3 forward = ((Component)this).transform.position - cameraToUse.transform.position;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        ((Component)this).transform.rotation = Quaternion.LookRotation(forward.normalized, cameraToUse.transform.up);
    }
}
