using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

/// <summary>
/// Physics-based coin controller driven by serial input (w/a/s/d for movement, j for jump, r for respawn).
/// Behaves like CoinController but uses characters from a serial port instead of keyboard.
/// Attach to a coin with a Rigidbody component.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CoinControllerSerial : MonoBehaviour
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

    [Header("Serial Port")]
    [Tooltip("Serial port name (e.g. COM3 on Windows, /dev/tty.usbserial-1420 on Mac)")]
    [SerializeField] private string portName = "COM3";

    [Tooltip("Baud rate for serial communication")]
    [SerializeField] private int baudRate = 9600;

    private Rigidbody _rb;
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    private SerialPort _serialPort;
    private Thread _serialThread;
    private bool _isRunning = true;
    private readonly object _queueLock = new object();
    private Queue<char> _receivedChars = new Queue<char>();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _startPosition = transform.position;
        _startRotation = transform.rotation;
    }

    private void Start()
    {
        _serialPort = new SerialPort(portName, baudRate);
        _serialPort.DtrEnable = true;
        _serialPort.RtsEnable = true;
        _serialPort.ReadTimeout = 50;
        _serialPort.Open();

        _serialThread = new Thread(ReadSerial);
        _serialThread.Start();
    }

    private void FixedUpdate()
    {
        HandleMovementFromSerial();
    }

    private void Update()
    {
        HandleJumpFromSerial();
        HandleRespawnFromSerial();
        HandleSendKeysToArduino();
    }

    /// <summary>
    /// Sends 'n' and 'm' to the Arduino when the corresponding keys are pressed.
    /// </summary>
    private void HandleSendKeysToArduino()
    {
        if (Input.GetKeyDown(KeyCode.N))
            SendCharacter('n');
        if (Input.GetKeyDown(KeyCode.M))
            SendCharacter('m');
    }

    /// <summary>
    /// Sends a single character to the Arduino over the serial port.
    /// </summary>
    public void SendCharacter(char sentChar)
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Write("" + sentChar);
            Debug.Log($"[Serial] Sent character: '{sentChar}'");
        }
    }

    /// <summary>
    /// Process queued serial characters and apply movement (w/a/s/d) same as WASD keys.
    /// </summary>
    private void HandleMovementFromSerial()
    {
        Vector3 moveDirection = Vector3.zero;
        lock (_queueLock)
        {
            while (_receivedChars.Count > 0)
            {
                char c = _receivedChars.Dequeue();
                switch (char.ToLowerInvariant(c))
                {
                    case 'w': moveDirection += Vector3.forward; break;
                    case 's': moveDirection += Vector3.back; break;
                    case 'a': moveDirection += Vector3.left; break;
                    case 'd': moveDirection += Vector3.right; break;
                    default:
                        // Re-queue non-movement chars so Update can handle jump/respawn
                        _receivedChars.Enqueue(c);
                        goto doneMovement;
                }
            }
            doneMovement: ;
        }

        moveDirection = moveDirection.normalized;
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            _rb.AddForce(moveDirection * moveForce);
            Vector3 torque = Vector3.Cross(Vector3.up, moveDirection) * torqueForce;
            _rb.AddTorque(torque);
        }
    }

    private void HandleJumpFromSerial()
    {
        bool shouldJump = false;
        lock (_queueLock)
        {
            // Peek and consume 'j' from queue
            if (_receivedChars.Count > 0)
            {
                var temp = new List<char>();
                while (_receivedChars.Count > 0)
                {
                    char c = _receivedChars.Dequeue();
                    if (char.ToLowerInvariant(c) == 'j')
                    {
                        shouldJump = true;
                        foreach (var rest in temp) _receivedChars.Enqueue(rest);
                        break;
                    }
                    temp.Add(c);
                }
                if (!shouldJump)
                {
                    foreach (var rest in temp) _receivedChars.Enqueue(rest);
                }
            }
        }

        if (shouldJump && IsGrounded())
        {
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void HandleRespawnFromSerial()
    {
        bool shouldRespawn = false;
        lock (_queueLock)
        {
            if (_receivedChars.Count > 0)
            {
                var temp = new List<char>();
                while (_receivedChars.Count > 0)
                {
                    char c = _receivedChars.Dequeue();
                    if (char.ToLowerInvariant(c) == 'r')
                    {
                        shouldRespawn = true;
                        foreach (var rest in temp) _receivedChars.Enqueue(rest);
                        break;
                    }
                    temp.Add(c);
                }
                if (!shouldRespawn)
                {
                    foreach (var rest in temp) _receivedChars.Enqueue(rest);
                }
            }
        }

        if (shouldRespawn)
        {
            Respawn();
        }
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundMask);
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

    private void ReadSerial()
    {
        while (_isRunning)
        {
            if (_serialPort != null && _serialPort.IsOpen && _serialPort.BytesToRead > 0)
            {
                char c = (char)_serialPort.ReadByte();
                lock (_queueLock)
                {
                    _receivedChars.Enqueue(c);
                }
                Debug.Log($"[Serial] Received letter: '{c}'");
            }
            Thread.Sleep(10);
        }
    }

    private void OnApplicationQuit()
    {
        _isRunning = false;
        _serialThread?.Join();
        _serialPort?.Close();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, Vector3.down * groundCheckDistance);
    }
#endif
}
