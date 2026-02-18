using UnityEngine;

/// <summary>
/// Attach to a GameObject to act as a spawn manager. Spawns a prefab at random positions
/// within a volume centered on this object, waits until the spawned object is destroyed,
/// then waits a cooldown before spawning again.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    [Header("Spawning")]
    [Tooltip("Prefab to spawn")]
    [SerializeField] private GameObject prefabToSpawn;

    [Tooltip("Time in seconds to wait after the spawned object is destroyed before spawning again")]
    [SerializeField] private float cooldownTime = 2f;

    [Header("Spawn volume (centered on this object)")]
    [Tooltip("Size of the spawn volume along the X axis")]
    [SerializeField] private float spawnVolumeSizeX = 10f;

    [Tooltip("Size of the spawn volume along the Y axis")]
    [SerializeField] private float spawnVolumeSizeY = 10f;

    [Tooltip("Size of the spawn volume along the Z axis")]
    [SerializeField] private float spawnVolumeSizeZ = 10f;

    private void Start()
    {
        if (prefabToSpawn != null)
            StartCoroutine(SpawnLoop());
        else
            Debug.LogWarning("SpawnManager: No prefab assigned. Assign a prefab in the Inspector.");
    }

    private System.Collections.IEnumerator SpawnLoop()
    {
        while (true)
        {
            Vector3 randomPosition = GetRandomPositionInVolume();
            GameObject instance = Instantiate(prefabToSpawn, randomPosition, Quaternion.identity);

            yield return new WaitUntil(() => instance == null);

            yield return new WaitForSeconds(cooldownTime);
        }
    }

    private Vector3 GetRandomPositionInVolume()
    {
        Vector3 center = transform.position;
        float halfX = spawnVolumeSizeX * 0.5f;
        float halfY = spawnVolumeSizeY * 0.5f;
        float halfZ = spawnVolumeSizeZ * 0.5f;

        float x = center.x + Random.Range(-halfX, halfX);
        float y = center.y + Random.Range(-halfY, halfY);
        float z = center.z + Random.Range(-halfZ, halfZ);

        return new Vector3(x, y, z);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 center = transform.position;
        Vector3 size = new Vector3(spawnVolumeSizeX, spawnVolumeSizeY, spawnVolumeSizeZ);
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.35f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.8f);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
