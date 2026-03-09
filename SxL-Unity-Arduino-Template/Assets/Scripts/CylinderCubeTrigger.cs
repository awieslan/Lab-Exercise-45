using UnityEngine;

/// <summary>
/// Placed on the cylinder. OnTriggerEnter with a spawned cube notifies the server
/// so the cube is destroyed and score is updated. Assigned by ReadWriteCharsHTTPServer at runtime.
/// </summary>
public class CylinderCubeTrigger : MonoBehaviour
{
    [HideInInspector]
    public ReadWriteCharsHTTPServer server;

    void OnTriggerEnter(Collider other)
    {
        if (server == null) return;
        if (other.GetComponent<CubeInstanceBehavior>() == null) return;

        Renderer r = other.GetComponent<Renderer>();
        if (r == null || r.material == null) return;

        server.OnCylinderHitCube(other.gameObject, r.material.color);
    }
}
