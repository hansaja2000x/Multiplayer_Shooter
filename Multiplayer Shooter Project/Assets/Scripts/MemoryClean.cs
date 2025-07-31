using UnityEngine;
using System.Collections;

public class MemoryCleaner : MonoBehaviour
{
    [Tooltip("How frequently (in seconds) to clear memory.")]
    public float clearInterval = 30f;  // Adjust based on your game needs

    private void Start()
    {
        // Start periodic memory cleaning
        StartCoroutine(ClearMemoryRoutine());
    }

    private IEnumerator ClearMemoryRoutine()
    {
        var wait = new WaitForSeconds(clearInterval);
        while (true)
        {
            yield return wait;
            ClearMemory();
        }
    }

    public static void ClearMemory()
    {
        // Unload unused assets
        Resources.UnloadUnusedAssets();

        // Force garbage collection
        System.GC.Collect();

        Debug.Log("Memory cleared proactively.");
    }
}
