/*
 * PROMPT (requirements for this script):
 * Edit this script so that instead of altering the color of a single cube, this script instantiates
 * a prefab of a cube several times within a predefined area.
 * Cubes of several colors should be instantiated. One color should be the same as the background
 * of the color on the website (determined the same way as the current cube color is determined).
 * In addition there should be a certain number of decoy colors (determined by a public variable).
 * For each color (target and decoys) there should be several cubes, with count randomly determined
 * per color; min and max of this count are public variables.
 * Cubes are instantiated within a volume defined by a rectangular prism configurable in the Unity editor.
 * When the color on the website (HTTP-IoT-LAN-Website-Controller) is changed, all existing cubes
 * are cleared and a new set is generated.
 * All cubes slowly rotate and bounce up and down; relevant variables are adjustable in the Unity editor.
 *
 * Additional prompt: When the cylinder collides with a cube, the cube is destroyed. If the cube was
 * the target color (same as cylinder) add points (tweakable); if wrong color subtract points (tweakable).
 * Implement a timer for game duration; when time ends, all cubes disappear, cylinder is frozen, no more
 * cubes spawn, but tapping the fish can still change the cylinder color. Website shows above the fish:
 * Time (larger), Score, Accuracy% , Misses. Include this prompt in the code as a comment.
 */
using System.Collections.Generic;
using System.Net;
using UnityEngine;

/// <summary>
/// Unity as HTTP Server - Arduino as HTTP Client.
/// Instantiates cube prefabs in a volume; target color matches website background,
/// plus decoy-colored cubes. On website color change, cubes are cleared and regenerated.
/// </summary>
public class ReadWriteCharsHTTPServer : MonoBehaviour
{
    [Header("Server Settings")]
    public int serverPort = 8080;

    [Header("Cube Spawning")]
    [Tooltip("Prefab to instantiate (should have a Renderer and will get CubeInstanceBehavior if missing)")]
    public GameObject cubePrefab;
    [Tooltip("Number of decoy colors in addition to the target (website) color")]
    public int decoyColorCount = 3;
    [Tooltip("Minimum number of cubes to spawn per color")]
    public int minCubesPerColor = 2;
    [Tooltip("Maximum number of cubes to spawn per color (inclusive)")]
    public int maxCubesPerColor = 6;

    [Header("Spawn Volume (Rectangular Prism)")]
    [Tooltip("Center of the spawn volume in world space")]
    public Vector3 spawnVolumeCenter = Vector3.zero;
    [Tooltip("Size of the box (width, height, depth)")]
    public Vector3 spawnVolumeSize = new Vector3(10f, 4f, 10f);

    [Header("Cube Motion (applied to each instantiated cube)")]
    [Tooltip("Rotation speed in degrees per second around Y")]
    public float cubeRotationSpeed = 25f;
    [Tooltip("Bounce amplitude in world units")]
    public float bounceAmplitude = 0.4f;
    [Tooltip("Bounce frequency (cycles per second)")]
    public float bounceFrequency = 1.5f;

    [Header("Cylinder (joystick-controlled)")]
    [Tooltip("Cylinder transform to move on XZ plane via website joystick")]
    public Transform cylinder;
    [Tooltip("Movement speed in world units per second when joystick is at full tilt")]
    public float cylinderMoveSpeed = 5f;

    [Header("Game")]
    [Tooltip("Game duration in seconds; when it reaches 0 the game ends")]
    public float gameDuration = 60f;
    [Tooltip("Points added when cylinder collects a cube of the target color")]
    public int pointsPerCorrect = 1;
    [Tooltip("Points subtracted when cylinder collects a cube of the wrong color (enter as positive, e.g. 5)")]
    public int pointsPerWrong = 5;

    [Header("LED Control")]
    private int ledPin = 4;

    private HTTPServer server;
    private char currentChar = 'a';
    private int charsSent = 0;
    private int sendTimer = 0;
    private int sendTime = 100;

    private Queue<Color> colorQueue = new Queue<Color>();
    private readonly object queueLock = new object();

    private float joystickX;
    private float joystickZ;
    private readonly object joystickLock = new object();

    private List<GameObject> spawnedCubes = new List<GameObject>();

    private float gameTimeRemaining;
    private bool gameOver;
    private int score;
    private int correctHits;
    private int wrongHits;
    private Color currentTargetColor;
    private readonly object gameStateLock = new object();

    void Start()
    {
        server = new HTTPServer(serverPort);

        server.AddRoute("/", HandleRoot);
        server.AddRoute("/data", HandleData);
        server.AddRoute("/command", HandleCommand);
        server.AddRoute("/joystick", HandleJoystick);
        server.AddRoute("/stats", HandleStats);

        server.Start();

        gameTimeRemaining = gameDuration;
        gameOver = false;
        score = 0;
        correctHits = 0;
        wrongHits = 0;

        if (cylinder != null)
        {
            Rigidbody rb = cylinder.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = cylinder.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
            }
            Collider col = cylinder.GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
            CylinderCubeTrigger trigger = cylinder.GetComponent<CylinderCubeTrigger>();
            if (trigger == null)
                trigger = cylinder.gameObject.AddComponent<CylinderCubeTrigger>();
            trigger.server = this;
        }

        Debug.Log($"Unity HTTP Server started on port {serverPort}");
        Debug.Log("Configure Arduino to connect to this Unity IP");
    }

    void Update()
    {
        if (sendTimer > 0)
        {
            sendTimer -= 1;
        }
        else
        {
            sendTimer = sendTime;
            charsSent += 1;
            currentChar = (charsSent % 2 == 0) ? 'a' : 'b';
        }

        lock (queueLock)
        {
            while (colorQueue.Count > 0)
            {
                Color color = colorQueue.Dequeue();
                if (cylinder != null)
                {
                    Renderer cylRend = cylinder.GetComponent<Renderer>();
                    if (cylRend != null && cylRend.material != null)
                        cylRend.material.color = color;
                }
                lock (gameStateLock)
                {
                    currentTargetColor = color;
                }
                if (!gameOver)
                {
                    ClearAllCubes();
                    SpawnCubesForColor(color);
                    Debug.Log($"[Main Thread] Cubes regenerated and cylinder set to color {color}");
                }
                else
                    Debug.Log($"[Main Thread] Game over: cylinder color updated only");
            }
        }

        if (!gameOver)
        {
            gameTimeRemaining -= Time.deltaTime;
            if (gameTimeRemaining <= 0f)
            {
                gameTimeRemaining = 0f;
                gameOver = true;
                ClearAllCubes();
                Debug.Log("[Main Thread] Game over");
            }
        }

        if (!gameOver)
        {
            float jx, jz;
            lock (joystickLock)
            {
                jx = joystickX;
                jz = joystickZ;
            }
            if (cylinder != null && (jx != 0f || jz != 0f))
            {
                Vector3 move = new Vector3(jx, 0f, jz) * (cylinderMoveSpeed * Time.deltaTime);
                cylinder.position += move;
            }
        }
    }

    void OnDestroy()
    {
        ClearAllCubes();
        server?.Stop();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawCube(spawnVolumeCenter, spawnVolumeSize);
        Gizmos.color = new Color(0f, 1f, 1f, 1f);
        Gizmos.DrawWireCube(spawnVolumeCenter, spawnVolumeSize);
    }

    private void ClearAllCubes()
    {
        foreach (GameObject go in spawnedCubes)
        {
            if (go != null)
                Destroy(go);
        }
        spawnedCubes.Clear();
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b, float epsilon = 0.02f)
    {
        return Mathf.Abs(a.r - b.r) < epsilon && Mathf.Abs(a.g - b.g) < epsilon && Mathf.Abs(a.b - b.b) < epsilon;
    }

    /// <summary>
    /// Called by CylinderCubeTrigger when the cylinder hits a cube. Runs on main thread.
    /// </summary>
    public void OnCylinderHitCube(GameObject cube, Color cubeColor)
    {
        if (cube == null) return;
        if (!spawnedCubes.Remove(cube)) return;

        Destroy(cube);

        Color target;
        lock (gameStateLock)
        {
            target = currentTargetColor;
        }

        bool isCorrect = ColorsApproximatelyEqual(cubeColor, target);
        lock (gameStateLock)
        {
            if (isCorrect)
            {
                score += pointsPerCorrect;
                correctHits++;
            }
            else
            {
                score -= pointsPerWrong;
                wrongHits++;
            }
        }
    }

    private void SpawnCubesForColor(Color targetColor)
    {
        if (cubePrefab == null)
        {
            Debug.LogWarning("ReadWriteCharsHTTPServer: cubePrefab is not assigned.");
            return;
        }

        Vector3 half = spawnVolumeSize * 0.5f;
        Vector3 min = spawnVolumeCenter - half;
        Vector3 max = spawnVolumeCenter + half;

        // Target color (website background)
        int targetCount = Random.Range(minCubesPerColor, maxCubesPerColor + 1);
        SpawnCubesOfColor(targetColor, targetCount, min, max);

        // Decoy colors
        for (int i = 0; i < decoyColorCount; i++)
        {
            Color decoyColor = new Color(
                Random.Range(0f, 1f),
                Random.Range(0f, 1f),
                Random.Range(0f, 1f),
                1f
            );
            int decoyCount = Random.Range(minCubesPerColor, maxCubesPerColor + 1);
            SpawnCubesOfColor(decoyColor, decoyCount, min, max);
        }
    }

    private void SpawnCubesOfColor(Color color, int count, Vector3 volumeMin, Vector3 volumeMax)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(volumeMin.x, volumeMax.x),
                Random.Range(volumeMin.y, volumeMax.y),
                Random.Range(volumeMin.z, volumeMax.z)
            );

            GameObject instance = Instantiate(cubePrefab, pos, Quaternion.identity, transform);
            spawnedCubes.Add(instance);

            Renderer r = instance.GetComponent<Renderer>();
            if (r != null && r.material != null)
                r.material.color = color;

            CubeInstanceBehavior behavior = instance.GetComponent<CubeInstanceBehavior>();
            if (behavior == null)
                behavior = instance.AddComponent<CubeInstanceBehavior>();
            behavior.rotationSpeed = cubeRotationSpeed;
            behavior.bounceAmplitude = bounceAmplitude;
            behavior.bounceFrequency = bounceFrequency;
        }
    }

    string HandleRoot(HttpListenerContext context)
    {
        return "<h1>Unity HTTP Server</h1>" +
               "<p>GET /data - Get current character (a or b)</p>" +
               "<p>GET /command?cmd=c - Legacy: cube RED</p>" +
               "<p>GET /command?cmd=d - Legacy: cube BLUE</p>" +
               "<p>GET /command?color=r,g,b - Set target color and regenerate cubes (e.g. ?color=255,128,0)</p>" +
               "<p>GET /joystick?x=&lt;float&gt;&z=&lt;float&gt; - Cylinder XZ input (-1 to 1)</p>" +
               "<p>GET /stats - JSON: time, score, accuracy, misses</p>";
    }

    string HandleStats(HttpListenerContext context)
    {
        float time;
        int sc, correct, wrong;
        lock (gameStateLock)
        {
            time = gameTimeRemaining;
            sc = score;
            correct = correctHits;
            wrong = wrongHits;
        }
        float accuracy = (correct + wrong) > 0 ? (100f * correct / (correct + wrong)) : 0f;
        string json = "{\"time\":" + time.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                      ",\"score\":" + sc +
                      ",\"accuracy\":" + accuracy.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                      ",\"misses\":" + wrong + "}";
        return json;
    }

    string HandleJoystick(HttpListenerContext context)
    {
        string xParam = context.Request.QueryString["x"];
        string zParam = context.Request.QueryString["z"];
        if (!string.IsNullOrEmpty(xParam) && float.TryParse(xParam, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
            !string.IsNullOrEmpty(zParam) && float.TryParse(zParam, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
        {
            x = Mathf.Clamp(x, -1f, 1f);
            z = Mathf.Clamp(z, -1f, 1f);
            lock (joystickLock)
            {
                joystickX = x;
                joystickZ = z;
            }
            return "ok";
        }
        return "Invalid joystick (expect x and z in -1..1)";
    }

    string HandleData(HttpListenerContext context)
    {
        Debug.Log($"[Server] Sending char: {currentChar}");
        return currentChar.ToString();
    }

    string HandleCommand(HttpListenerContext context)
    {
        string colorParam = context.Request.QueryString["color"];
        if (!string.IsNullOrEmpty(colorParam))
        {
            string[] parts = colorParam.Split(',');
            if (parts.Length == 3 &&
                int.TryParse(parts[0].Trim(), out int r) &&
                int.TryParse(parts[1].Trim(), out int g) &&
                int.TryParse(parts[2].Trim(), out int b))
            {
                r = Mathf.Clamp(r, 0, 255);
                g = Mathf.Clamp(g, 0, 255);
                b = Mathf.Clamp(b, 0, 255);
                Color color = new Color(r / 255f, g / 255f, b / 255f, 1f);
                lock (queueLock)
                {
                    colorQueue.Enqueue(color);
                }
                Debug.Log($"[Server] Command: color - Queued RGB({r},{g},{b}), cubes will regenerate");
                return "Cubes set to color";
            }
        }

        string cmd = context.Request.QueryString["cmd"];
        if (cmd == "c")
        {
            lock (queueLock)
            {
                colorQueue.Enqueue(Color.red);
            }
            Debug.Log("[Server] Command: c - Queued RED, cubes will regenerate");
            return "Cubes set to RED";
        }
        if (cmd == "d")
        {
            lock (queueLock)
            {
                colorQueue.Enqueue(Color.blue);
            }
            Debug.Log("[Server] Command: d - Queued BLUE, cubes will regenerate");
            return "Cubes set to BLUE";
        }

        return "Invalid command";
    }
}
