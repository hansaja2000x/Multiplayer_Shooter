using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private GameObject cameraObj;
    [SerializeField] private VariableJoystick variableJoystick;

    [SerializeField, Range(0.1f, 1f)] private float joystickDeadzone = 0.2f;
    [SerializeField, Range(0f, 1f)] private float rotationScreenThreshold = 0.3f;
    [SerializeField, Range(0.1f, 2f)] private float rotationSensitivity = 0.35f;
    [SerializeField] private bool invertYRotation = false;
    [SerializeField, Range(0.1f, 1f)] private float rotationDeadzone = 0.1f;
    [SerializeField] private bool verticalAimEnabled = true;

    private Button shootButton;
    private float lastTouchX;
    private float lastTouchY;
    private bool isCursorLocked = false;
    private int rotationTouchId = -1;

    private bool fLast, bLast, lLast, rLast;
    private float rotLast;
    private float rotUpLast;

    private float smoothRot = 0f;
    private float smoothRotY = 0f;

    private bool isUsingPCInput = false;

    void Start()
    {
        LockCursor();

        if (!Application.isMobilePlatform)
        {
            isUsingPCInput = true;
            HideTouchControls();
        }
    }

    void Update()
    {
        if (!isCursorLocked) return;

        bool f = Input.GetKey(KeyCode.W);
        bool b = Input.GetKey(KeyCode.S);
        bool l = Input.GetKey(KeyCode.A);
        bool r = Input.GetKey(KeyCode.D);
        float rot = 0f;
        float rotY = 0f;

        if (!Application.isMobilePlatform || isUsingPCInput)
        {
            rot = Input.GetAxis("Mouse X") * 5f;
            rotY = Input.GetAxis("Mouse Y") * 5f;
            if (!verticalAimEnabled) rotY = 0f;
        }

        // ✅ Handle joystick movement (mobile)
        if (variableJoystick != null && Application.isMobilePlatform)
        {
            float vertical = variableJoystick.Vertical;
            float horizontal = variableJoystick.Horizontal;

            if (Mathf.Abs(vertical) < joystickDeadzone) vertical = 0f;
            if (Mathf.Abs(horizontal) < joystickDeadzone) horizontal = 0f;

            f |= vertical > 0;
            b |= vertical < 0;
            l |= horizontal < 0;
            r |= horizontal > 0;
        }

        // ✅ Handle rotation (right half of screen only, horizontal only)
        if (Application.isMobilePlatform)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    continue;

                if (touch.position.x < Screen.width * 0.5f)
                    continue;

                if (touch.phase == TouchPhase.Began)
                {
                    lastTouchX = touch.position.x;
                    lastTouchY = touch.position.y;
                    rotationTouchId = touch.fingerId;
                }
                else if (touch.fingerId == rotationTouchId && touch.phase == TouchPhase.Moved)
                {
                    float deltaX = touch.position.x - lastTouchX;
                    float deltaY = touch.position.y - lastTouchY;

                    if (Mathf.Abs(deltaX) < rotationDeadzone) deltaX = 0f;
                    if (Mathf.Abs(deltaY) < rotationDeadzone) deltaY = 0f;

                    rot = deltaX * rotationSensitivity;
                    if (verticalAimEnabled)
                    {
                        rotY = deltaY * rotationSensitivity * (invertYRotation ? -1f : 1f);
                    }

                    lastTouchX = touch.position.x;
                    lastTouchY = touch.position.y;
                    break;
                }
                else if (touch.fingerId == rotationTouchId &&
                         (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
                {
                    rotationTouchId = -1;
                }
            }
        }

        // ✅ Smooth rotation
        smoothRot = Mathf.Lerp(smoothRot, rot, Time.deltaTime * 10f);
        smoothRotY = Mathf.Lerp(smoothRotY, rotY, Time.deltaTime * 10f);

        if (f != fLast || b != bLast || l != lLast || r != rLast ||
            Mathf.Abs(smoothRot - rotLast) > 0.0001f || Mathf.Abs(smoothRotY - rotUpLast) > 0.0001f)
        {
            NetworkManager.Instance.SendInput(f, b, l, r, smoothRot, smoothRotY);
            fLast = f; bLast = b; lLast = l; rLast = r; rotLast = smoothRot; rotUpLast = smoothRotY;
        }
        if (Input.GetMouseButtonDown(0))
        {
            NetworkManager.Instance.SendShoot();
        }
    }


    private void HideTouchControls()
    {
        if (variableJoystick != null)
        {
            variableJoystick.gameObject.SetActive(false);
        }
        if (shootButton != null)
        {
            shootButton.gameObject.SetActive(false);
        }
    }

    private void OnShootButtonClicked()
    {
        NetworkManager.Instance.SendShoot();
    }

    public void SetShootButton(Button shootButton)
    {
        this.shootButton = shootButton;
        shootButton.onClick.AddListener(OnShootButtonClicked);
    }

    public void SetVariableJoystick(VariableJoystick variableJoystick)
    {
        this.variableJoystick = variableJoystick;
    }

    public void DeactivateCameraObject()
    {
        if (cameraObj != null)
            cameraObj.SetActive(false);
    }

    public void EndGame()
    {
        UnlockCursor();
    }

    public void BeginRound()
    {
        fLast = false;
        bLast = false;
        lLast = false;
        rLast = false;
        rotLast = 0f;
        rotUpLast = 0f;
        rotationTouchId = -1;
        LockCursor();
    }

    private void LockCursor()
    {
        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        isCursorLocked = true;
    }

    private void UnlockCursor()
    {
        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        isCursorLocked = false;
        NetworkManager.Instance.SendInput(false, false, false, false, 0f, 0f);
    }

    public void ToggleVerticalAim()
    {
        verticalAimEnabled = !verticalAimEnabled;
    }
}
