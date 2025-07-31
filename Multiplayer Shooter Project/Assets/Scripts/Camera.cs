
using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target; // The player or target to follow
    [SerializeField] private float maxDistance = 5f; // Maximum distance from target
    [SerializeField] private float minDistance = 0.5f; // Minimum distance to avoid clipping too close
    [SerializeField] private float height = 1.5f; // Height offset above target's position
    [SerializeField] private float damping = 5f; // Smoothing factor for camera movement
    [SerializeField] private LayerMask collisionLayers; // Layers to check for collisions (e.g., walls, obstacles)

    private Vector3 desiredPosition;
    private Vector3 currentVelocity; // For smoothing

    void LateUpdate()
    {
        if (target == null) return;

        // Calculate the desired camera position behind and above the target
        Vector3 offset = -target.forward * maxDistance + Vector3.up * height;
        desiredPosition = target.position + offset;

        // Perform a raycast from the target to the desired camera position to detect obstacles
        RaycastHit hit;
        Vector3 rayDirection = desiredPosition - target.position;
        float rayDistance = rayDirection.magnitude;

        if (Physics.Raycast(target.position, rayDirection.normalized, out hit, rayDistance, collisionLayers))
        {
            // If hit an obstacle, adjust the desired position to the hit point, but keep min distance
            float adjustedDistance = Mathf.Clamp(hit.distance, minDistance, maxDistance);
            desiredPosition = target.position + rayDirection.normalized * adjustedDistance;
        }

        // Smoothly move the camera to the desired position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, damping * Time.deltaTime);

        // Make the camera look at the target
        transform.LookAt(target.position + Vector3.up * height);
    }
}