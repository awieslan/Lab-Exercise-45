using UnityEngine;

/// <summary>
/// Applied to instantiated cube prefabs. Rotates and bounces each cube.
/// Rotation and bounce parameters are set by ReadWriteCharsHTTPServer when spawning.
/// </summary>
public class CubeInstanceBehavior : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Degrees per second around Y axis")]
    public float rotationSpeed = 30f;

    [Header("Bounce")]
    [Tooltip("Vertical bounce amplitude in world units")]
    public float bounceAmplitude = 0.5f;
    [Tooltip("Bounce cycle speed")]
    public float bounceFrequency = 2f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // Rotate around Y
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);

        // Bounce: offset Y from start position
        float offset = Mathf.Sin(Time.time * bounceFrequency) * bounceAmplitude;
        transform.position = startPosition + Vector3.up * offset;
    }
}
