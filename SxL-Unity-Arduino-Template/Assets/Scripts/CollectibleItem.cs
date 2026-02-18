using UnityEngine;

/*
 * Prompt used to create this script:
 * Create a new script to be assigned to the prefab instantiated by SpawnManagerScript.cs.
 * This script should have two main behaviors:
 * 1) The object should slowly move up and down while rotating as long as it exists
 * 2) When the object collides with the coin object it should be destroyed
 * Make all relevant values adjustable via public variables.
 */

/// <summary>
/// Attach to the prefab spawned by SpawnManager. Bobs and rotates while alive;
/// destroys itself when it collides with an object that has a CoinController.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CollectibleItem : MonoBehaviour
{
    [Header("Bob (up/down movement)")]
    [Tooltip("Height of the up-down bob")]
    public float bobAmplitude = 0.3f;

    [Tooltip("Speed of the bob (cycles per second)")]
    public float bobFrequency = 1f;

    [Header("Rotation")]
    [Tooltip("Speed of rotation in degrees per second")]
    public float rotationSpeed = 45f;

    [Tooltip("Axis to rotate around (e.g. Y-up = 0,1,0)")]
    public Vector3 rotationAxis = Vector3.up;

    private Vector3 _startPosition;

    private void Start()
    {
        _startPosition = transform.position;
    }

    private void Update()
    {
        float bobOffset = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        transform.position = _startPosition + Vector3.up * bobOffset;
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<CoinController>() != null)
        {
            Destroy(gameObject);
        }
    }
}
