using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon;

/// <summary>
/// Spawns and returns objects from a VRCObjectPool.
/// Demonstrates:
///   - VRCObjectPool.TryToSpawn() and Return()
///   - Pool objects receiving OnEnable when spawned
///   - Ownership transfer on pool objects
///   - Simple spawn/despawn button pattern
/// 
/// Setup in Unity:
///   1. Create an empty GameObject and add VRCObjectPool component.
///   2. Populate VRCObjectPool.Pool[] with the objects you want to recycle.
///   3. Assign the pool GameObject to the `pool` field on this script.
///   4. (Optional) Add an UdonBehaviour to each pool object that has OnPooledObjectSpawn().
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ObjectPoolExample : UdonSharpBehaviour
{
    [Header("References")]
    [Tooltip("The VRCObjectPool to draw from.")]
    [SerializeField] private VRCObjectPool pool;

    [Tooltip("Spawn point for newly spawned objects.")]
    [SerializeField] private Transform spawnPoint;

    /// <summary>
    /// Spawn the next available object from the pool, positioned at spawnPoint.
    /// Call this from a UI button or Interact.
    /// </summary>
    public void SpawnObject()
    {
        if (pool == null)
        {
            Debug.LogError("[ObjectPoolExample] Pool reference is null.");
            return;
        }

        GameObject obj = pool.TryToSpawn();
        if (obj == null)
        {
            Debug.LogWarning("[ObjectPoolExample] Pool is empty — no objects available.");
            return;
        }

        // Position/rotate the object at the spawn point
        if (spawnPoint != null)
        {
            obj.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        // Transfer ownership to the local player
        Networking.SetOwner(Networking.LocalPlayer, obj);
    }

    /// <summary>
    /// Return a specific object back to the pool.
    /// This deactivates it for all players and makes it available for future spawns.
    /// </summary>
    public void ReturnObject(GameObject obj)
    {
        if (pool == null || obj == null) return;
        pool.Return(obj);
    }

    /// <summary>
    /// Return ALL active pool objects back to the pool.
    /// Only the master should call this to maintain authority.
    /// </summary>
    public void ReturnAll()
    {
        if (!Networking.IsMaster) return;
        if (pool == null) return;

        GameObject[] poolObjects = pool.Pool;
        for (int i = 0; i < poolObjects.Length; i++)
        {
            if (poolObjects[i] != null && poolObjects[i].activeSelf)
                pool.Return(poolObjects[i]);
        }
    }
}
