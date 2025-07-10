using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private GameObject cameraObj;
    float lastMouseX;

    void Start()
    {
        lastMouseX = Input.mousePosition.x;
    }

    void Update()
    {
        bool forward = Input.GetKey(KeyCode.W);
        bool backward = Input.GetKey(KeyCode.S);
        bool left = Input.GetKey(KeyCode.A);
        bool right = Input.GetKey(KeyCode.D);

        float mouseDeltaX = Input.GetAxis("Mouse X") * 2.5f;

        NetwokManager.Instance.SendInput(forward, backward, left, right, mouseDeltaX);
    }

    public void DeactivateCameraObject()
    {
        cameraObj.SetActive(false);
    }
}
