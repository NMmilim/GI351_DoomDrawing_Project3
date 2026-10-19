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

    //// Called when the bullet's collider is set to "Is Trigger" (passes through, no bounce)
    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other.gameObject);
    }

    // Called when the bullet's collider is NOT a trigger (solid, physical hit)
    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        TryHit(collision.gameObject);
        Rock rock = collision.gameObject.GetComponent<Rock>();
        if (rock != null)
        {
            rock.TakeDamage(damage);
        }
    }

    protected bool IsEnemy(GameObject target)
    {
        bool tag_match = target.CompareTag(enemy_tag);
        bool layer_match = (enemy_layers.value & (1 << target.layer)) != 0;
        return tag_match || layer_match;
    }

    protected void TryHit(GameObject target)
    {
        if (IsEnemy(target))
        {
            Destroy(gameObject);
        }
    }
}
