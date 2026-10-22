using UnityEngine;

public class Bullet : MonoBehaviour
{
    // protected = child classes (e.g. Bullet_Ricochet) can use these too
    [SerializeField] protected float speed = 15f;
    [SerializeField] protected float lifetime = 3f;

    [Tooltip("Bullet is destroyed when it hits an object with this tag.")]
    [SerializeField] protected string enemy_tag = "Enemy";
    [Tooltip("Bullet is also destroyed when it hits an object on any of these layers.")]
    [SerializeField] protected LayerMask enemy_layers;
    public int damage = 1;

    protected Rigidbody2D rb;
    protected float life_timer;   // timer instead of Destroy(gameObject, lifetime) so children can reset it
    protected bool hasHit;        // Pierce 0: Destroy() is deferred to end of frame, so block a 2nd hit in the same physics step

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = transform.right * speed;
        life_timer = lifetime;
    }

    protected virtual void Update()
    {
        life_timer -= Time.deltaTime;
        if (life_timer <= 0f) Destroy(gameObject);   // clean up if it never hits anything
    }

    // Called when the bullet's collider is set to "Is Trigger" (passes through, no bounce)
    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        HandleImpact(other.gameObject);
    }

    // Called when the bullet's collider is NOT a trigger (solid, physical hit)
    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        HandleImpact(collision.gameObject);
    }

    private void HandleImpact(GameObject target)
    {
        if (hasHit) return;
        if (CheckAmmoDrop(target)) { hasHit = true; return; }
        if (CheckDisposableMine(target)) { hasHit = true; return; }

        Rock rock = target.GetComponent<Rock>();
        if (rock == null && !IsEnemy(target)) return;
        hasHit = true;
        Destroy(gameObject);

        // Combo/kill bookkeeping is handled inside Rock.TakeDamage
        if (rock != null)
        {
            rock.TakeDamage(damage, isExplosionOrPiercing: false);
        }
    }

    protected virtual bool CheckDisposableMine(GameObject target)
    {
        DisposableMine mine = target.GetComponent<DisposableMine>();
        if (mine != null && !mine.IsDetonated)
        {
            mine.Detonate();
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    protected bool CheckAmmoDrop(GameObject target)
    {
        AmmoDrop drop = target.GetComponent<AmmoDrop>();
        if (drop != null)
        {
            drop.Collect();
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    protected bool IsEnemy(GameObject target)
    {
        bool tag_match = target.CompareTag(enemy_tag);
        bool layer_match = (enemy_layers.value & (1 << target.layer)) != 0;
        return tag_match || layer_match;
    }
}
