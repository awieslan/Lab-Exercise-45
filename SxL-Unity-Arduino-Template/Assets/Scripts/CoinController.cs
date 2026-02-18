using UnityEngine;

/// <summary>
/// Physics-based controller for a coin GameObject. Supports movement, jumping, and respawning.
/// Attach to a coin with a Rigidbody component.
/// 
/// Prompt: Please create a script that will be used for a player to control physics-based movement of a coin. The player should be able to use up-down-left-right inputs on either a keyboard or a joystick to apply a rotational and directional force to the coin gameobject in the indicated direction. They should also be able to jump using a spacebar or a controller button. They should also be able to respawn the coin at its starting position using the R key or another controller button.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CoinController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Force applied in the movement direction")]
    [SerializeField] private float moveForce = 10f;

    [Tooltip("Torque applied to create rotational tumble in the direction of movement")]
    [SerializeField] private float torqueForce = 5f;

    [Header("Jumping")]
    [Tooltip("Upward force applied when jumping")]
    [SerializeField] private float jumpForce = 8f;

    [Tooltip("Layers considered as ground for jump validation")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Tooltip("Distance to check below the coin for ground contact")]
    [SerializeField] private float groundCheckDistance = 0.6f;

    private Rigidbody _rb;
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _startPosition = transform.position;
        _startRotation = transform.rotation;
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private void Update()
    {
        HandleJump();
        HandleRespawn();
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            _rb.AddForce(moveDirection * moveForce);
            Vector3 torque = Vector3.Cross(Vector3.up, moveDirection) * torqueForce;
            _rb.AddTorque(torque);
        }
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && IsGrounded())
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundMask);
    }

    private void HandleRespawn()
    {
        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton4))
        {
            Respawn();
        }
    }

    /// <summary>
    /// Respawns the coin at its starting position and rotation, resetting physics state.
    /// </summary>
    public void Respawn()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.position = _startPosition;
        transform.rotation = _startRotation;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, Vector3.down * groundCheckDistance);
    }
#endif
}
