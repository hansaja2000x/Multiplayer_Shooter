using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private GameObject cameraObj;
    [SerializeField] private VariableJoystick variableJoystick;
    [SerializeField] private bool isOnPC;
    private Button shootButton;
    private float lastMouseX;
    private float lastTouchX;
    private bool isCursorLocked = false;
    private bool isTouching = false;

    private bool fLast, bLast, lLast, rLast;
    private float rotLast;

    void Start()
    {
        lastMouseX = Input.mousePosition.x;
        LockCursor();
    }

    void Update()
    {
        if (!isCursorLocked) return;
        if(isOnPC == true)
        {
            PcCalculations();
        }
        else
        {
            TouchScreenalculations();
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
        if (f != fLast || b != bLast || l != lLast || r != rLast || Mathf.Abs(rot - rotLast) > 0.0000001f)
        {
            NetworkManager.Instance.SendInput(f, b, l, r, rot);
            fLast = f; bLast = b; lLast = l; rLast = r; rotLast = rot;
        }

        if (Input.GetMouseButtonDown(0))
            NetworkManager.Instance.SendShoot();
    }

    private void TouchScreenalculations()
    {
        if(variableJoystick == null)
        {
            return;
        }
        float vertical = variableJoystick.Vertical;
        float horizontal = variableJoystick.Horizontal;
        float rot = 0f;

        bool f = false;
        bool b = false;
        bool l = false;
        bool r = false;

        if (vertical == 0)
        {
            vertical = 0.0001f;
        }

        if (horizontal == 0)
        {
            horizontal = 0.0001f;
        }

        if (Mathf.Abs(vertical) - Mathf.Abs(horizontal) > 0f)
        {
            if (Mathf.Abs(vertical) / Mathf.Abs(horizontal) > 2.093f)
            {

                if(variableJoystick.Vertical > 0)
                {
                    f = true;
                    b = false;
                }
                else if(variableJoystick.Vertical < 0)
                {
                    f = false;
                    b = true;
                }
            }
            else
            {
                if (variableJoystick.Vertical > 0)
                {
                    f = true;
                    b = false;
                }
                else if (variableJoystick.Vertical < 0)
                {
                    f = false;
                    b = true;
                }

                if (variableJoystick.Horizontal > 0)
                {
                    r = true;
                    l = false;
                }
                else if (variableJoystick.Horizontal < 0)
                {
                    r = false;
                    l = true;
                }
            }
        }

        if (Mathf.Abs(horizontal) - Mathf.Abs(vertical) > 0f)
        {
            if (Mathf.Abs(horizontal) / Mathf.Abs(vertical) > 2.093f)
            {
                if (variableJoystick.Horizontal > 0)
                {
                    r = true;
                    l = false;
                }
                else if (variableJoystick.Horizontal < 0)
                {
                    r = false;
                    l = true;
                }
            }
            else
            {
                if (variableJoystick.Vertical > 0)
                {
                    f = true;
                    b = false;
                }
                else if (variableJoystick.Vertical < 0)
                {
                    f = false;
                    b = true;
                }

                if (variableJoystick.Horizontal > 0)
                {
                    r = true;
                    l = false;
                }
                else if (variableJoystick.Horizontal < 0)
                {
                    r = false;
                    l = true;
                }
            }
        }

        /*if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            float screenBlockThreshold = Screen.width * 0.3f;
            if (touch.position.x <= screenBlockThreshold)
                return;

            if (touch.phase == TouchPhase.Began)
            {
                lastTouchX = touch.position.x;
                isTouching = true;
            }
            else if (touch.phase == TouchPhase.Moved && isTouching)
            {
                float deltaX = touch.position.x - lastTouchX;
                rot = deltaX * 0.35f; 
                lastTouchX = touch.position.x;
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                rot = 0.0000001f;
                isTouching = false;
            }
        }*/

        if (Input.touchCount > 0)
        {
            float screenBlockThreshold = Screen.width * 0.3f;

            // Find a touch on the right side (above 30% of screen width)
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (touch.position.x <= screenBlockThreshold)
                    continue;

                if (touch.phase == TouchPhase.Began)
                {
                    lastTouchX = touch.position.x;
                    isTouching = true;
                }
                else if (touch.phase == TouchPhase.Moved && isTouching)
                {
                    float deltaX = touch.position.x - lastTouchX;
                    rot = deltaX * 0.35f;
                    lastTouchX = touch.position.x;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    rot = 0.0000001f;
                    isTouching = false;
                }

                // We found the right-side touch to handle — break to avoid conflicting touches
                break;
            }
        }


        // Send only if changed
        if (f != fLast || b != bLast || l != lLast || r != rLast || Mathf.Abs(rot - rotLast) >= 0.0000001f)
        {
            NetworkManager.Instance.SendInput(f, b, l, r, rot);
            fLast = f; bLast = b; lLast = l; rLast = r; rotLast = rot;
        }




    }


    // Setters
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

    //

    public void DeactivateCameraObject()
    {
        cameraObj.SetActive(false);
    }

    public void EndGame()
    {
        UnlockCursor();
    }

    private void LockCursor()
    {
        /*Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;*/
        isCursorLocked = true;
    }

    private void UnlockCursor()
    {
        /*Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;*/
        isCursorLocked = false;
        NetworkManager.Instance.SendInput(false, false, false, false, 0);
    }


}
