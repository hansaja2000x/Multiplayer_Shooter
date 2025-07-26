using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private GameObject cameraObj;
    [SerializeField] private VariableJoystick variableJoystick;
    [SerializeField] private bool isOnPC;
    private Button shootButton;
    private float lastTouchX;
    private bool isCursorLocked = false;
    private bool isTouching = false;

    private bool fLast, bLast, lLast, rLast;
    private float rotLast;

    void Start()
    {
        LockCursor();
    }

    void Update()
    {
        if (!isCursorLocked) return;

        if (isOnPC)
        {
            PcCalculations();
        }
        else
        {
            TouchScreenCalculations();
        }
    }

    private void PcCalculations()
    {
        bool f = Input.GetKey(KeyCode.W);
        bool b = Input.GetKey(KeyCode.S);
        bool l = Input.GetKey(KeyCode.A);
        bool r = Input.GetKey(KeyCode.D);
        float rot = Input.GetAxis("Mouse X") * 5f;

        // Send only if changed
        if (f != fLast || b != bLast || l != lLast || r != rLast || Mathf.Abs(rot - rotLast) > 0.0001f)
        {
            NetworkManager.Instance.SendInput(f, b, l, r, rot);
            fLast = f; bLast = b; lLast = l; rLast = r; rotLast = rot;
        }

        if (Input.GetMouseButtonDown(0))
            NetworkManager.Instance.SendShoot();
    }

    private void TouchScreenCalculations()
    {
        if (variableJoystick == null) return;

        float vertical = variableJoystick.Vertical;
        float horizontal = variableJoystick.Horizontal;
        float rot = 0f;

        bool f = false;
        bool b = false;
        bool l = false;
        bool r = false;

        // Avoid division by zero
        if (Mathf.Abs(vertical) < 0.001f) vertical = 0f;
        if (Mathf.Abs(horizontal) < 0.001f) horizontal = 0f;

        // Calculate absolute values for comparison
        float absV = Mathf.Abs(vertical);
        float absH = Mathf.Abs(horizontal);

        // Threshold for considering input significant (deadzone)
        const float inputThreshold = 0.2f;
        if (absV < inputThreshold && absH < inputThreshold)
        {
            // No significant input
        }
        else if (absV > absH)
        {
            // Vertical dominant
            const float diagonalThreshold = 2.093f; // approx tan(65°)
            if (absV / (absH > 0 ? absH : 0.001f) > diagonalThreshold)
            {
                // Pure vertical
                f = vertical > 0;
                b = vertical < 0;
            }
            else
            {
                // Diagonal
                f = vertical > 0;
                b = vertical < 0;
                l = horizontal < 0;
                r = horizontal > 0;
            }
        }
        else if (absH > absV)
        {
            // Horizontal dominant
            const float diagonalThreshold = 2.093f;
            if (absH / (absV > 0 ? absV : 0.001f) > diagonalThreshold)
            {
                // Pure horizontal
                l = horizontal < 0;
                r = horizontal > 0;
            }
            else
            {
                // Diagonal
                f = vertical > 0;
                b = vertical < 0;
                l = horizontal < 0;
                r = horizontal > 0;
            }
        }
        else
        {
            // Equal (45°), treat as diagonal
            f = vertical > 0;
            b = vertical < 0;
            l = horizontal < 0;
            r = horizontal > 0;
        }

        // Handle rotation via touch on right side
        const float sensitivity = 0.35f;
        float screenBlockThreshold = Screen.width * 0.3f;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (touch.position.x <= screenBlockThreshold) continue; // Ignore left side (joystick area)

            if (touch.phase == TouchPhase.Began)
            {
                lastTouchX = touch.position.x;
                isTouching = true;
            }
            else if (touch.phase == TouchPhase.Moved && isTouching)
            {
                float deltaX = touch.position.x - lastTouchX;
                rot = deltaX * sensitivity * Time.deltaTime * 60f; // Frame-rate independent, assuming 60 FPS target
                lastTouchX = touch.position.x;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                rot = 0f;
                isTouching = false;
            }

            // Process only the first valid right-side touch to avoid conflicts
            break;
        }

        // Send only if changed (use a small epsilon for float comparison)
        if (f != fLast || b != bLast || l != lLast || r != rLast || Mathf.Abs(rot - rotLast) > 0.0001f)
        {
            NetworkManager.Instance.SendInput(f, b, l, r, rot);
            fLast = f; bLast = b; lLast = l; rLast = r; rotLast = rot;
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

    public void SetIsOnPC(bool isOnPC)
    {
        this.isOnPC = isOnPC;
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
        isTouching = false;
        LockCursor();
    }

    private void LockCursor()
    {
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;
        isCursorLocked = true;
    }

    private void UnlockCursor()
    {
        // Cursor.lockState = CursorLockMode.None;
        // Cursor.visible = true;
        isCursorLocked = false;
        NetworkManager.Instance.SendInput(false, false, false, false, 0f);
    }
}