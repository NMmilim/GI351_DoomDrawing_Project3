using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 3f;

    [Tooltip("Bullet is destroyed when it hits an object with this tag.")]
    [SerializeField] private string enemy_tag = "Enemy";
    [Tooltip("Bullet is also destroyed when it hits an object on any of these layers.")]
    [SerializeField] private LayerMask enemy_layers;
    public int damage = 1;
    private void Start()
    {
        GetComponent<Rigidbody2D>().linearVelocity = transform.right * speed;
        Destroy(gameObject, lifetime);   // clean up if it never hits anything
    }

    //// Called when the bullet's collider is set to "Is Trigger" (passes through, no bounce)
    //private void OnTriggerEnter2D(Collider2D other)
    //{
    //    TryHit(other.gameObject);
    //}

    // Called when the bullet's collider is NOT a trigger (solid, physical hit)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHit(collision.gameObject);
        Rock rock = collision.gameObject.GetComponent<Rock>();
        if (rock != null)
        {
            rock.TakeDamage(damage);
        }
    }

    private void TryHit(GameObject target)
    {
        bool tag_match = target.CompareTag(enemy_tag);
        bool layer_match = (enemy_layers.value & (1 << target.layer)) != 0;

        if (tag_match || layer_match)
        {
            Destroy(gameObject);
        }
    }
}
