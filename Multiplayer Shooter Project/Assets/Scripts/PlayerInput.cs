using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private GameObject cameraObj;
    [SerializeField] private VariableJoystick variableJoystick;

    // New serialized fields for customization
    [SerializeField, Range(0.1f, 1f)] private float joystickDeadzone = 0.2f;
    [SerializeField, Range(0f, 1f)] private float rotationScreenThreshold = 0.3f; // Fraction of screen width for left joystick area
    [SerializeField, Range(0.1f, 2f)] private float rotationSensitivity = 0.35f;
    [SerializeField] private bool invertYRotation = false; // Option to invert vertical rotation
    [SerializeField, Range(0.1f, 1f)] private float rotationDeadzone = 0.1f; // Pixel delta threshold for rotation to ignore micro-movements
    [SerializeField] private bool verticalAimEnabled = true; // Toggle to enable/disable vertical gun aim

    private Button shootButton;
    private float lastTouchX;
    private float lastTouchY;
    private bool isCursorLocked = false;
    private int rotationTouchId = -1; // Track the specific touch ID for rotation to handle multi-touch better

    private bool fLast, bLast, lLast, rLast;
    private float rotLast;
    private float rotUpLast;

    private bool isUsingPCInput = false;

    void Start()
    {
        LockCursor();

        // Hide touch controls by default on non-mobile platforms
        if (!Application.isMobilePlatform)
        {
            isUsingPCInput = true;
            HideTouchControls();
        }
    }

    void Update()
    {
        if (!isCursorLocked) return;

        // Detect PC input only on non-mobile platforms
        if (!isUsingPCInput && !Application.isMobilePlatform)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
                Mathf.Abs(Input.GetAxis("Mouse X")) > 0 || Mathf.Abs(Input.GetAxis("Mouse Y")) > 0)
            {
                isUsingPCInput = true;
                HideTouchControls();
            }
        }

        // PC inputs
        bool f = Input.GetKey(KeyCode.W);
        bool b = Input.GetKey(KeyCode.S);
        bool l = Input.GetKey(KeyCode.A);
        bool r = Input.GetKey(KeyCode.D);
        float rot = Input.GetAxis("Mouse X") * 5f;
        float rotY = Input.GetAxis("Mouse Y") * 5f;

        // Disable vertical aim if toggled off for PC
        if (!verticalAimEnabled) rotY = 0f;

        // Touch movement inputs
        if (variableJoystick != null)
        {
            float vertical = variableJoystick.Vertical;
            float horizontal = variableJoystick.Horizontal;

            // Apply deadzone
            if (Mathf.Abs(vertical) < joystickDeadzone) vertical = 0f;
            if (Mathf.Abs(horizontal) < joystickDeadzone) horizontal = 0f;

            // Calculate absolute values for comparison
            float absV = Mathf.Abs(vertical);
            float absH = Mathf.Abs(horizontal);

            if (absV > 0 || absH > 0)
            {
                bool f_touch = false;
                bool b_touch = false;
                bool l_touch = false;
                bool r_touch = false;

                if (absV > absH)
                {
                    // Vertical dominant
                    const float diagonalThreshold = 2.093f; // approx tan(65°)
                    if (absH == 0 || absV / absH > diagonalThreshold)
                    {
                        // Pure vertical
                        f_touch = vertical > 0;
                        b_touch = vertical < 0;
                    }
                    else
                    {
                        // Diagonal
                        f_touch = vertical > 0;
                        b_touch = vertical < 0;
                        l_touch = horizontal < 0;
                        r_touch = horizontal > 0;
                    }
                }
                else if (absH > absV)
                {
                    // Horizontal dominant
                    const float diagonalThreshold = 2.093f;
                    if (absV == 0 || absH / absV > diagonalThreshold)
                    {
                        // Pure horizontal
                        l_touch = horizontal < 0;
                        r_touch = horizontal > 0;
                    }
                    else
                    {
                        // Diagonal
                        f_touch = vertical > 0;
                        b_touch = vertical < 0;
                        l_touch = horizontal < 0;
                        r_touch = horizontal > 0;
                    }
                }
                else
                {
                    // Equal (45°), treat as diagonal
                    f_touch = vertical > 0;
                    b_touch = vertical < 0;
                    l_touch = horizontal < 0;
                    r_touch = horizontal > 0;
                }

                // Combine with PC inputs (OR for directions)
                f |= f_touch;
                b |= b_touch;
                l |= l_touch;
                r |= r_touch;
            }
        }

        // Handle rotation via touch on right side with multi-touch support
        float screenBlockThreshold = Screen.width * rotationScreenThreshold;
        bool foundRotationTouch = false;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            // Skip if not in rotation area
            if (touch.position.x <= screenBlockThreshold) continue;

            // If we have an active rotation touch, check if it's this one
            if (rotationTouchId != -1 && touch.fingerId != rotationTouchId) continue;

            foundRotationTouch = true;

            if (touch.phase == TouchPhase.Began)
            {
                lastTouchX = touch.position.x;
                lastTouchY = touch.position.y;
                rotationTouchId = touch.fingerId;
            }
            else if (touch.phase == TouchPhase.Moved)
            {
                float deltaX = touch.position.x - lastTouchX;
                float deltaY = touch.position.y - lastTouchY;

                // Apply deadzone to deltas
                if (Mathf.Abs(deltaX) < rotationDeadzone) deltaX = 0f;
                if (Mathf.Abs(deltaY) < rotationDeadzone) deltaY = 0f;

                float rot_touch = deltaX * rotationSensitivity;
                float rotY_touch = deltaY * rotationSensitivity;

                // Invert Y if enabled
                if (invertYRotation) rotY_touch = -rotY_touch;

                // Disable vertical aim if toggled off for touch
                if (!verticalAimEnabled) rotY_touch = 0f;

                // Add to PC rotation
                rot += rot_touch;
                rotY += rotY_touch;

                lastTouchX = touch.position.x;
                lastTouchY = touch.position.y;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                rotationTouchId = -1;
            }

            // Since we're tracking by ID, we can break after processing the relevant touch
            break;
        }

        if (!foundRotationTouch)
        {
            rotationTouchId = -1;
        }

        // Send only if changed
        if (f != fLast || b != bLast || l != lLast || r != rLast || Mathf.Abs(rot - rotLast) > 0.0001f || Mathf.Abs(rotY - rotUpLast) > 0.0001f)
        {
            NetworkManager.Instance.SendInput(f, b, l, r, rot, rotY);
            fLast = f; bLast = b; lLast = l; rLast = r; rotLast = rot; rotUpLast = rotY;
        }

        if (Input.GetMouseButtonDown(0))
            NetworkManager.Instance.SendShoot();
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

    // Public method to toggle vertical aim enabled state
    public void ToggleVerticalAim()
    {
        verticalAimEnabled = !verticalAimEnabled;
    }
}