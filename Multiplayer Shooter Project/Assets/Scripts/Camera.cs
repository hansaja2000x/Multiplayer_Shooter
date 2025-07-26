using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target; // The player or target to follow
    public Vector3 targetOffset = new Vector3(0, 1.5f, 0); // Offset from target's position (e.g., head height)

    [Header("Camera Positioning")]
    public float distance = 5f; // Default distance from target
    public float minDistance = 1f; // Minimum distance when colliding
    public float maxDistance = 10f; // Maximum distance
    public float height = 2f; // Height above the target

    [Header("Rotation Settings")]
    public float mouseSensitivity = 3f; // Mouse look speed
    public float verticalAngleMin = -45f; // Min vertical angle (looking down)
    public float verticalAngleMax = 60f; // Max vertical angle (looking up)
    public float rotationDamping = 5f; // Smoothing for rotation

    [Header("Collision Settings")]
    public LayerMask collisionMask; // Layers to check for collisions
    public float collisionBuffer = 0.2f; // Buffer to prevent clipping

    private float currentDistance;
    private float yaw = 0f; // Horizontal rotation
    private float pitch = 0f; // Vertical rotation

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("Target not assigned to ThirdPersonCamera script!");
            return;
        }

        currentDistance = distance;
        Cursor.lockState = CursorLockMode.Locked; // Lock cursor for mouse look
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Handle input for rotation
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, verticalAngleMin, verticalAngleMax); // Clamp vertical angle

        // Calculate desired rotation
        Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0f);

        // Smoothly rotate the camera
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationDamping);

        // Calculate desired position without collision
        Vector3 desiredPosition = target.position + targetOffset - transform.forward * currentDistance;

        // Collision detection
        if (HandleCameraCollision(ref desiredPosition))
        {
            // If collision detected, adjust distance
            currentDistance = Mathf.Clamp(currentDistance - collisionBuffer, minDistance, maxDistance);
        }
        else
        {
            // No collision, lerp back to default distance
            currentDistance = Mathf.Lerp(currentDistance, distance, Time.deltaTime * rotationDamping);
        }

        // Set camera position
        transform.position = desiredPosition;

        // Always look at the target offset
        transform.LookAt(target.position + targetOffset);
    }

    private bool HandleCameraCollision(ref Vector3 desiredPosition)
    {
        RaycastHit hit;
        Vector3 start = target.position + targetOffset;
        Vector3 direction = desiredPosition - start;
        float rayDistance = direction.magnitude;

        // Cast a ray from target to desired camera position
        if (Physics.Raycast(start, direction.normalized, out hit, rayDistance, collisionMask))
        {
            // Collision detected, adjust position to hit point with buffer
            desiredPosition = start + direction.normalized * (hit.distance - collisionBuffer);
            return true;
        }

        // Optional: Use SphereCast for thicker detection (uncomment if needed)
        /*
        if (Physics.SphereCast(start, 0.2f, direction.normalized, out hit, rayDistance, collisionMask))
        {
            desiredPosition = start + direction.normalized * (hit.distance - collisionBuffer);
            return true;
        }
        */

        return false;
    }
}