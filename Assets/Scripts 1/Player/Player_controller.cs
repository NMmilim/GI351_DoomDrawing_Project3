using UnityEngine;
using UnityEngine.InputSystem;

public class Player_controller : MonoBehaviour
{
    public static Player_controller Instance { get; private set; }

    [SerializeField] private Transform mouse_transform;

    [Header("Earth Center & Orbit")]
    [Tooltip("Reference to the Earth object. If empty, automatically finds a GameObject named 'Earth' or defaults to (0,0).")]
    [SerializeField] private Transform earth;
    [Tooltip("Distance from Earth to the gun. If 0, auto-detects from the initial scene position.")]
    [SerializeField] private float orbitDistance = 0f;

    [Header("Rotation & Pull Force")]
    [Tooltip("Maximum rotation speed in degrees per second.")]
    [SerializeField] private float turnSpeed = 240f;
    [Tooltip("Pull force inertia / smoothing time in seconds. Higher = heavier feel with smooth acceleration and deceleration.")]
    [SerializeField] private float smoothTime = 0.2f;

    [Header("Bullet")]
    [SerializeField] private Transform leftPrefab;
    [SerializeField] private Transform rightPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 0.5f;
    private float nextFireTime;

    [Header("Ammunition")]
    [Tooltip("Current regular cannon ammunition.")]
    [SerializeField] private int regularAmmo = 90;
    [Tooltip("Maximum regular cannon ammo capacity. 0 or less = unlimited.")]
    [SerializeField] private int maxRegularAmmo = 180;

    [Tooltip("Current ricochet cannon ammunition.")]
    [SerializeField] private int ricochetAmmo = 15;
    [Tooltip("Maximum ricochet cannon ammo capacity. 0 or less = unlimited.")]
    [SerializeField] private int maxRicochetAmmo = 30;

    [Tooltip("Show current ammo counts on screen.")]
    [SerializeField] private bool showAmmoHUD = true;

    private float currentAngle;
    private float angleVelocity;

    public int RegularAmmo => regularAmmo;
    public int MaxRegularAmmo => maxRegularAmmo;
    public int RicochetAmmo => ricochetAmmo;
    public int MaxRicochetAmmo => maxRicochetAmmo;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        // Serialized fields are NOT auto-filled by Unity, so fall back to sensible defaults
        if (mouse_transform == null) mouse_transform = transform;   // rotate this object
        if (firePoint == null) firePoint = mouse_transform;         // shoot from the aim pivot
        if (leftPrefab == null) Debug.LogError("Left Bullet Prefab is not assigned on " + name, this);
        if (rightPrefab == null) Debug.LogError("Right Bullet Prefab is not assigned on " + name, this);
    }

    private void Start()
    {
        if (earth == null)
        {
            GameObject earthObj = GameObject.Find("Earth");
            if (earthObj != null) earth = earthObj.transform;
        }

        Vector2 earthCenter = GetEarthCenter();
        Vector2 startOffset = (Vector2)transform.position - earthCenter;

        // Auto-detect orbit radius from starting position if left at 0
        if (orbitDistance <= 0.001f)
        {
            orbitDistance = startOffset.magnitude;
        }

        // Initialize angle smoothly from starting placement so it never snaps on frame 1
        if (startOffset.sqrMagnitude > 0.01f)
        {
            currentAngle = Mathf.Atan2(startOffset.y, startOffset.x) * Mathf.Rad2Deg;
        }
        else
        {
            currentAngle = mouse_transform.eulerAngles.z;
        }
    }

    private void MouseAim()
    {
        if (Mouse.current == null || Camera.main == null) return;

        Vector2 earthCenter = GetEarthCenter();
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 dirToMouse = mouseWorld - earthCenter;

        // Only update target angle if mouse is moved outside Earth center
        if (dirToMouse.sqrMagnitude > 0.01f)
        {
            float targetAngle = Mathf.Atan2(dirToMouse.y, dirToMouse.x) * Mathf.Rad2Deg;

            // Apply pull force & speed limits instead of instant snapping
            if (smoothTime > 0.001f)
            {
                currentAngle = Mathf.SmoothDampAngle(currentAngle, targetAngle, ref angleVelocity, smoothTime, turnSpeed);
            }
            else
            {
                currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnSpeed * Time.deltaTime);
            }
        }

        // Position smoothly on the circular orbit around Earth
        if (orbitDistance > 0.001f)
        {
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector2 orbitPos = earthCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitDistance;
            transform.position = orbitPos;
        }

        // Face outward along orbit
        Quaternion rotation = Quaternion.AngleAxis(currentAngle, Vector3.forward);
        mouse_transform.rotation = rotation;
    }

    void Update()
    {
        MouseAim();

        if (Mouse.current == null) return;

        // Regular fire (Left click, full-auto): requires at least 1 regular ammo
        if (Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            if (regularAmmo > 0)
            {
                regularAmmo--;
                Instantiate(leftPrefab, firePoint.position, firePoint.rotation);
                nextFireTime = Time.time + fireRate;
            }
        }

        // Ricochet fire (Right click, semi-auto): requires at least 1 ricochet ammo
        if (Mouse.current.rightButton.wasPressedThisFrame && Time.time >= nextFireTime)
        {
            if (ricochetAmmo > 0)
            {
                ricochetAmmo--;
                Instantiate(rightPrefab, firePoint.position, firePoint.rotation);
                nextFireTime = Time.time + fireRate;
            }
        }
    }

    /// <summary>
    /// Adds regular cannon ammo up to maximum capacity.
    /// </summary>
    public void AddRegularAmmo(int amount)
    {
        regularAmmo = maxRegularAmmo > 0 ? Mathf.Min(regularAmmo + amount, maxRegularAmmo) : regularAmmo + amount;
        Debug.Log($"[Ammo] +{amount} Regular Ammo! Current: {regularAmmo}/{(maxRegularAmmo > 0 ? maxRegularAmmo.ToString() : "∞")}");
    }

    /// <summary>
    /// Adds ricochet cannon ammo up to maximum capacity.
    /// </summary>
    public void AddRicochetAmmo(int amount)
    {
        ricochetAmmo = maxRicochetAmmo > 0 ? Mathf.Min(ricochetAmmo + amount, maxRicochetAmmo) : ricochetAmmo + amount;
        Debug.Log($"[Ammo] +{amount} Ricochet Ammo! Current: {ricochetAmmo}/{(maxRicochetAmmo > 0 ? maxRicochetAmmo.ToString() : "∞")}");
    }

    public Vector2 GetEarthCenter()
    {
        return earth != null ? (Vector2)earth.position : Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 center = GetEarthCenter();
        float radius = orbitDistance > 0.001f ? orbitDistance : Vector2.Distance(transform.position, center);
        if (radius > 0.001f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(center, radius);
        }
    }

    private void OnGUI()
    {
        if (!showAmmoHUD) return;

        // Clean ammo HUD at bottom-left corner
        float cardWidth = 200f;
        float cardHeight = 75f;
        float x = 20f;
        float y = Screen.height - cardHeight - 20f;

        GUI.Box(new Rect(x, y, cardWidth, cardHeight), GUIContent.none);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        titleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x + 12, y + 8, cardWidth - 24, 16), "AMMUNITION", titleStyle);

        GUIStyle ammoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        // Regular Cannon Ammo (Cyan)
        ammoStyle.normal.textColor = regularAmmo > 0 ? new Color(0.2f, 0.85f, 1f) : Color.red;
        string regText = maxRegularAmmo > 0 ? $"Regular: {regularAmmo} / {maxRegularAmmo}" : $"Regular: {regularAmmo}";
        GUI.Label(new Rect(x + 12, y + 26, cardWidth - 24, 20), regText, ammoStyle);

        // Ricochet Cannon Ammo (Orange/Yellow)
        ammoStyle.normal.textColor = ricochetAmmo > 0 ? new Color(1f, 0.75f, 0.2f) : Color.red;
        string ricoText = maxRicochetAmmo > 0 ? $"Ricochet: {ricochetAmmo} / {maxRicochetAmmo}" : $"Ricochet: {ricochetAmmo}";
        GUI.Label(new Rect(x + 12, y + 48, cardWidth - 24, 20), ricoText, ammoStyle);
    }
}
