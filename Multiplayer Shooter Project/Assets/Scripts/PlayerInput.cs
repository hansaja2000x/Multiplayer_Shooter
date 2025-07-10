using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private GameObject cameraObj;
    private float lastMouseX;
    private bool isCursorLocked = false;

    void Start()
    {
        lastMouseX = Input.mousePosition.x;
        LockCursor();
    }

    void Update()
    {
        bool forward = Input.GetKey(KeyCode.W);
        bool backward = Input.GetKey(KeyCode.S);
        bool left = Input.GetKey(KeyCode.A);
        bool right = Input.GetKey(KeyCode.D);

        float mouseDeltaX = Input.GetAxis("Mouse X") * 5f;

        NetwokManager.Instance.SendInput(forward, backward, left, right, mouseDeltaX);

        if (Input.GetMouseButtonDown(0))
            NetwokManager.Instance.SendShoot();
    }

    public void DeactivateCameraObject()
    {
        cameraObj.SetActive(false);
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isCursorLocked = true;
    }
}
