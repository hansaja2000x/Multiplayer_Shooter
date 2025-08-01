using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target; // The player or target to follow
    [SerializeField] private float maxDistance = 5f; // Maximum distance from target
    [SerializeField] private float minDistance = 0.5f; // Minimum distance to avoid clipping too close
    [SerializeField] private float height = 1.5f; // Height offset above target's position
    [SerializeField] private float damping = 5f; // Smoothing factor for camera movement
    [SerializeField] private LayerMask collisionLayers; // Layers to check for collisions (e.g., walls, obstacles)
    [SerializeField] private float frontDistance = 3f;
    [SerializeField] private float badassHeightOffset = -0.5f; // Lower the look point to look up at the player for badass effect

    public enum CameraMode { Behind, FrontBadass }
    public CameraMode currentMode = CameraMode.Behind;

    private Vector3 desiredPosition;
    private Vector3 currentVelocity; // For smoothing

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetMode(CameraMode newMode)
    {
        currentMode = newMode;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Calculate the look point at the target's height offset
        Vector3 heightOffset = Vector3.up * height;
        if (currentMode == CameraMode.FrontBadass)
        {
            heightOffset += Vector3.up * badassHeightOffset;
        }
        Vector3 lookPoint = target.position + heightOffset;

        // Calculate the horizontal forward direction (project to XZ plane)
        Vector3 horizontalForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;

        // Determine offset direction based on mode
        Vector3 offsetDirection = (currentMode == CameraMode.Behind) ? -horizontalForward : horizontalForward;

        // Determine distance based on mode
        float currentDistance = (currentMode == CameraMode.Behind) ? maxDistance : frontDistance;

        // Calculate the horizontal offset
        Vector3 horizontalOffset = offsetDirection * currentDistance;

        // Desired position at constant height
        desiredPosition = lookPoint + horizontalOffset;

        // Perform a raycast from the look point in the offset direction to detect obstacles
        RaycastHit hit;
        Vector3 rayDirection = horizontalOffset;
        float rayDistance = currentDistance;

        if (Physics.Raycast(lookPoint, rayDirection.normalized, out hit, rayDistance, collisionLayers))
        {
            // If hit an obstacle, adjust the distance to the hit point, but keep min distance
            float adjustedDistance = Mathf.Clamp(hit.distance, minDistance, currentDistance);
            desiredPosition = lookPoint + rayDirection.normalized * adjustedDistance;
        }

        // Smoothly move the camera to the desired position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, damping * Time.deltaTime);

        // Make the camera look at the look point (keeps horizontal orientation, no y-axis rotation change)
        transform.LookAt(lookPoint);
    }
}