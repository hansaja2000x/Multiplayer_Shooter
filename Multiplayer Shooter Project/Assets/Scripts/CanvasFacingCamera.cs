using UnityEngine;
using System.Collections.Generic;

public class CanvasFacingCamera : MonoBehaviour
{
    [Header("Target Camera")]
    [SerializeField] private GameObject targetCamera;

    private List<GameObject> worldCanvases = new();

    void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 camPos = targetCamera.transform.position;

        foreach (var canvas in worldCanvases)
        {
            if (canvas == null) continue;

            Vector3 dir = camPos - canvas.transform.position;
            dir.y = 0f; 
            if (dir != Vector3.zero)
            {
                canvas.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }

    public void AddCanvas(GameObject canvas)
    {
        print("added");
        if (!worldCanvases.Contains(canvas))
            worldCanvases.Add(canvas);
    }

    public void RemoveCanvas(GameObject canvas)
    {
        if (worldCanvases.Contains(canvas))
            worldCanvases.Remove(canvas);
    }
}
