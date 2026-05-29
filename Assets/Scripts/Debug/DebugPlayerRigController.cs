using UnityEngine;

public class DebugPlayerRigController : MonoBehaviour
{
    [Header("Rig References")]
    [SerializeField] private Transform trackingRoot;
    [SerializeField] private Transform debugMotionRoot;
    [SerializeField] private Camera debugCamera;

    [Header("Activation")]
    [SerializeField] private bool enableInEditor = true;
    [SerializeField] private bool enableInStandalone = true;
    [SerializeField] private bool requireRightMouseForLook = false;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float fastMoveMultiplier = 2f;
    [SerializeField] private float verticalMoveSpeed = 1.5f;
    [SerializeField] private float yawSpeedDegrees = 120f;
    [SerializeField] private float mouseLookSensitivity = 2f;
    [SerializeField] private bool enableMouseLook = true;
    [SerializeField] private float minPitchDegrees = -70f;
    [SerializeField] private float maxPitchDegrees = 70f;

    [Header("Controls")]
    [SerializeField] private KeyCode moveUpKey = KeyCode.R;
    [SerializeField] private KeyCode moveDownKey = KeyCode.F;
    [SerializeField] private KeyCode turnLeftKey = KeyCode.Q;
    [SerializeField] private KeyCode turnRightKey = KeyCode.E;
    [SerializeField] private KeyCode fastMoveKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode resetOffsetKey = KeyCode.BackQuote;

    private Vector3 m_initialLocalPosition;
    private Quaternion m_initialLocalRotation;
    private Quaternion m_initialCameraLocalRotation;
    private float m_pitchDegrees;

    public Transform TrackingRoot => trackingRoot;
    public Transform DebugMotionRoot => debugMotionRoot;
    public Camera DebugCamera => debugCamera;

    private void Reset()
    {
        trackingRoot = transform;
        debugMotionRoot = transform;
        debugCamera = GetComponentInChildren<Camera>();
    }

    private void Awake()
    {
        CacheInitialState();
    }

    private void OnEnable()
    {
        CacheInitialState();
    }

    private void Update()
    {
        if (!IsControlEnabled())
        {
            return;
        }

        if (debugMotionRoot == null)
        {
            return;
        }

        HandleMovement();
        HandleRotation();

        if (Input.GetKeyDown(resetOffsetKey))
        {
            ResetDebugOffset();
        }
    }

    public void ResetDebugOffset()
    {
        if (debugMotionRoot != null)
        {
            debugMotionRoot.localPosition = m_initialLocalPosition;
            debugMotionRoot.localRotation = m_initialLocalRotation;
        }

        if (debugCamera != null)
        {
            debugCamera.transform.localRotation = m_initialCameraLocalRotation;
            m_pitchDegrees = NormalizePitch(debugCamera.transform.localEulerAngles.x);
        }
    }

    private void CacheInitialState()
    {
        if (debugMotionRoot != null)
        {
            m_initialLocalPosition = debugMotionRoot.localPosition;
            m_initialLocalRotation = debugMotionRoot.localRotation;
        }

        if (debugCamera != null)
        {
            m_initialCameraLocalRotation = debugCamera.transform.localRotation;
            m_pitchDegrees = NormalizePitch(debugCamera.transform.localEulerAngles.x);
        }
    }

    private bool IsControlEnabled()
    {
        if (Application.isEditor)
        {
            return enableInEditor;
        }

        return enableInStandalone;
    }

    private void HandleMovement()
    {
        float forward = 0f;
        if (Input.GetKey(KeyCode.W))
        {
            forward += 1f;
        }
        if (Input.GetKey(KeyCode.S))
        {
            forward -= 1f;
        }

        float right = 0f;
        if (Input.GetKey(KeyCode.D))
        {
            right += 1f;
        }
        if (Input.GetKey(KeyCode.A))
        {
            right -= 1f;
        }

        float up = 0f;
        if (Input.GetKey(moveUpKey))
        {
            up += 1f;
        }
        if (Input.GetKey(moveDownKey))
        {
            up -= 1f;
        }

        Vector3 planarMove = new Vector3(right, 0f, forward);
        if (planarMove.sqrMagnitude > 1f)
        {
            planarMove.Normalize();
        }

        float speedMultiplier = Input.GetKey(fastMoveKey) ? fastMoveMultiplier : 1f;
        debugMotionRoot.Translate(planarMove * (moveSpeed * speedMultiplier * Time.deltaTime), Space.Self);
        debugMotionRoot.Translate(Vector3.up * (up * verticalMoveSpeed * speedMultiplier * Time.deltaTime), Space.World);
    }

    private void HandleRotation()
    {
        float yawInput = 0f;
        if (Input.GetKey(turnLeftKey))
        {
            yawInput -= 1f;
        }
        if (Input.GetKey(turnRightKey))
        {
            yawInput += 1f;
        }

        if (Mathf.Abs(yawInput) > 0.001f)
        {
            debugMotionRoot.Rotate(Vector3.up, yawInput * yawSpeedDegrees * Time.deltaTime, Space.World);
        }

        if (!enableMouseLook || debugCamera == null)
        {
            return;
        }

        if (requireRightMouseForLook && !Input.GetMouseButton(1))
        {
            return;
        }

        /*float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        if (Mathf.Abs(mouseX) > 0.001f)
        {
            debugMotionRoot.Rotate(Vector3.up, mouseX * mouseLookSensitivity, Space.World);
        }

        if (Mathf.Abs(mouseY) > 0.001f)
        {
            m_pitchDegrees = Mathf.Clamp(m_pitchDegrees - mouseY * mouseLookSensitivity, minPitchDegrees, maxPitchDegrees);
            debugCamera.transform.localRotation = Quaternion.Euler(m_pitchDegrees, 0f, 0f);
        }*/
    }

    private static float NormalizePitch(float pitchDegrees)
    {
        if (pitchDegrees > 180f)
        {
            pitchDegrees -= 360f;
        }

        return pitchDegrees;
    }
}
