using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private GameObject cameraObj;
    private float lastMouseX;
    private bool isCursorLocked = false;

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
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isCursorLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isCursorLocked = false;
        NetworkManager.Instance.SendInput(false, false, false, false, 0);
    }


}
