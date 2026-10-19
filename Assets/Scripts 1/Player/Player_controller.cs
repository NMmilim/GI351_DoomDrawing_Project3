using UnityEngine;
using UnityEngine.InputSystem;
public class Player_controller : MonoBehaviour
{
    [SerializeField] Transform mouse_transform;
    [Header("Bullet")]
    [SerializeField] Transform leftPrefab;
    [SerializeField] Transform rightPrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] float fireRate = 0.5f;
    private float nextFireTime;

    private void Awake()
    {
        // Serialized fields are NOT auto-filled by Unity, so fall back to sensible defaults
        if (mouse_transform == null) mouse_transform = transform;   // rotate this object
        if (firePoint == null) firePoint = mouse_transform;         // shoot from the aim pivot
        if (leftPrefab == null) Debug.LogError("Left Bullet Prefab is not assigned on " + name, this);
        if (rightPrefab == null) Debug.LogError("Right Bullet Prefab is not assigned on " + name, this);
    }

    private void MouseAim()
    {
        Vector2 mouseposition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()) - mouse_transform.position;
        float angle = Mathf.Atan2(mouseposition.y, mouseposition.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        mouse_transform.rotation = rotation;
    }

    void Update()
    {
MouseAim();
        // isPressed = true every frame while held (full-auto); fireRate = seconds between shots
        if (Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            Instantiate(leftPrefab, firePoint.position, firePoint.rotation);
            nextFireTime = Time.time + fireRate;
        }
        if (Mouse.current.rightButton.wasPressedThisFrame&&Time.time >= nextFireTime)
        {
            Instantiate(rightPrefab, firePoint.position, firePoint.rotation);
            nextFireTime = Time.time + fireRate;
        }
    }
 
}
