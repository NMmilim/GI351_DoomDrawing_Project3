using UnityEngine;

public enum AmmoType
{
    Regular,
    Ricochet
}

/// <summary>
/// Floating ammunition drop in space.
/// The player gathers ammo by SHOOTING at this drop.
/// Uses a trigger collider and frozen physics so bullets/enemies CANNOT push it away.
/// </summary>
public class AmmoDrop : MonoBehaviour
{
    [Header("Ammo Settings")]
    [SerializeField] private AmmoType ammoType = AmmoType.Regular;
    [SerializeField] private int ammoAmount = 30;

    [Header("Lifetime & Drift")]
    [Tooltip("Seconds before uncollected drop disappears.")]
    [SerializeField] private float lifetime = 20f;
    [Tooltip("Gentle float drift in space. Set to 0 for completely stationary.")]
    [SerializeField] private float driftSpeed = 0.2f;

    private Rigidbody2D rb;
    private bool isCollected = false;

    public AmmoType Type => ammoType;
    public int Amount => ammoAmount;

    public void Setup(AmmoType type, int amount)
    {
        ammoType = type;
        ammoAmount = amount;
    }

    private void Awake()
    {
        // Enforce trigger so physics collisions cannot push this drop back
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<CircleCollider2D>();
        }
        col.isTrigger = true;

        // If there is a Rigidbody2D, prevent all collision impulse pushing
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);

        // Apply a very slight space drift if enabled
        if (rb != null && driftSpeed > 0f)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            rb.linearVelocity = randomDir * driftSpeed;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckBulletHit(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckBulletHit(collision.gameObject);
    }

    private void CheckBulletHit(GameObject hitObj)
    {
        if (isCollected) return;

        // Check if hit by a Bullet (by component or tag)
        Bullet bullet = hitObj.GetComponent<Bullet>();
        if (bullet != null || hitObj.CompareTag("Bullet"))
        {
            Collect();

            // Destroy the bullet that collected the drop
            if (bullet != null)
            {
                Destroy(bullet.gameObject);
            }
        }
    }

    /// <summary>
    /// Collects the drop and adds ammunition to the player.
    /// </summary>
    public void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        if (Player_controller.Instance != null)
        {
            if (ammoType == AmmoType.Regular)
                Player_controller.Instance.AddRegularAmmo(ammoAmount);
            else
                Player_controller.Instance.AddRicochetAmmo(ammoAmount);
        }
        else
        {
            Debug.LogWarning("[AmmoDrop] Player_controller instance not found to receive ammo.");
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Spawns an ammo drop. Automatically ensures Collider2D (trigger) and AmmoDrop are present
    /// even if a bare sprite prefab is passed in.
    /// </summary>
    public static GameObject SpawnDrop(Vector3 position, AmmoType type, int amount, GameObject customPrefab = null)
    {
        GameObject instance;

        if (customPrefab != null)
        {
            instance = Object.Instantiate(customPrefab, position, Quaternion.identity);
        }
        else
        {
            // Procedural fallback if no prefab is assigned
            instance = new GameObject(type == AmmoType.Regular ? "Drop_RegularAmmo" : "Drop_RicochetAmmo");
            instance.transform.position = position;
            instance.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            SpriteRenderer sr = instance.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            sr.color = type == AmmoType.Regular ? new Color(0.2f, 0.8f, 1f) : new Color(1f, 0.7f, 0.1f);
            sr.sortingOrder = 3;
        }

        // Guarantee Collider2D exists and is a trigger (so it CANNOT be pushed)
        Collider2D col = instance.GetComponent<Collider2D>();
        if (col == null)
        {
            col = instance.AddComponent<CircleCollider2D>();
        }
        col.isTrigger = true;

        // Guarantee AmmoDrop component exists and is configured
        AmmoDrop drop = instance.GetComponent<AmmoDrop>();
        if (drop == null)
        {
            drop = instance.AddComponent<AmmoDrop>();
        }
        drop.Setup(type, amount);

        return instance;
    }
}
