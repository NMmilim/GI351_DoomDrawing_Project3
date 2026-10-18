using UnityEngine;

public class Rock : MonoBehaviour
{
    [SerializeField] string enemy_tag = "Enemy";
    [SerializeField] LayerMask enemy_layers;
    public int damage = 1;
    public int hp;
    public int maxhp;

    void Start()
    {
        hp = maxhp;   // start every rock at full health
    }

    // Public so OTHER scripts (other rocks, bullets, ...) can damage this rock
    public void TakeDamage(int amount)
    {
        hp -= amount;
        Debug.Log($"{name} took {amount} damage, hp now {hp}");

        if (hp <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHit(collision.gameObject);
    }

    private void TryHit(GameObject target)
    {
        bool tag_match = target.CompareTag(enemy_tag);
        bool layer_match = (enemy_layers.value & (1 << target.layer)) != 0;

        if (tag_match || layer_match)
        {
            // Deal MY damage to the rock I bumped into
            Rock other = target.GetComponent<Rock>();
            if (other != null) other.TakeDamage(damage);
        }
    }
}
