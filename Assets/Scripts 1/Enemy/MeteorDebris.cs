using UnityEngine;

/// <summary>
/// Meteor shard (debris) shed when a meteor is destroyed - the building block of the domino chain reaction.
///
/// Flies in a straight line, destroys the first living Rock it touches (which then sheds its own shards),
/// and fizzles out after a short lifetime.
///
/// Hit detection uses a swept circle query instead of a physics collider, so shards:
/// - never shove rocks around (a collider + rigidbody would add unwanted impulses),
/// - never tunnel through a rock at low frame rates (the whole step is swept),
/// - need no layer / tag / collision-matrix setup.
///
/// Spawned by Rock.SpawnDebris. Either assign a prefab on the Rock, or let CreateProcedural build a small
/// shard from the meteor's own sprite (with a glowing trail so the chain is easy to follow).
/// </summary>
public class MeteorDebris : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    [SerializeField] private float lifetime = 1.1f;
    [SerializeField] private int damage = 1;
    [Tooltip("Radius of the swept hit circle (the target's own collider radius is added on top).")]
    [SerializeField] private float hitRadius = 0.2f;
    [Tooltip("Max random tumble speed in degrees per second (visual only).")]
    [SerializeField] private float spinSpeed = 540f;

    private Vector2 direction = Vector2.right;
    private float timeLeft;
    private float spin;
    private bool consumed;

    // Shared across all shards: no per-shard allocations during a big chain reaction
    private static readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[8];
    private static ContactFilter2D filter;
    private static bool filterReady;
    private static Material trailMaterial;
    private static Sprite fallbackSprite;

    private static ContactFilter2D Filter
    {
        get
        {
            if (!filterReady)
            {
                filter = new ContactFilter2D();
                filter.useTriggers = true;     // also hit rocks whose collider is a trigger
                filter.useLayerMask = false;   // any layer; non-Rock hits are ignored below
                filterReady = true;
            }
            return filter;
        }
    }

    private void Awake()
    {
        timeLeft = lifetime;
        spin = Random.Range(-spinSpeed, spinSpeed);
    }

    /// <summary>Fires the shard. Call right after instantiating.</summary>
    public void Launch(Vector2 dir, float newSpeed, float newLifetime, int newDamage)
    {
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        speed = newSpeed;
        lifetime = newLifetime;
        timeLeft = newLifetime;
        damage = newDamage;
    }

    private void Update()
    {
        if (consumed) return;

        float dt = Time.deltaTime;
        timeLeft -= dt;
        if (timeLeft <= 0f)
        {
            consumed = true;
            Destroy(gameObject);
            return;
        }

        Vector2 pos = transform.position;
        float step = speed * dt;

        // Sweep this frame's whole movement and take the nearest living Rock along the path
        int count = Physics2D.CircleCast(pos, hitRadius, direction, Filter, hitBuffer, step);
        Rock best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider2D col = hitBuffer[i].collider;
            if (col == null) continue;

            // IsDead skips the meteor that just shed this shard (it is still alive for the rest of the frame)
            Rock rock = col.GetComponentInParent<Rock>();
            if (rock == null || rock.IsDead) continue;

            if (hitBuffer[i].distance < bestDistance)
            {
                bestDistance = hitBuffer[i].distance;
                best = rock;
            }
        }

        if (best != null)
        {
            consumed = true;
            transform.position = pos + direction * bestDistance;
            Destroy(gameObject);

            // Lethal damage -> Rock.Die -> sheds the next generation of shards (the domino step).
            // isExplosionOrPiercing lets shards hurt armored types if they are ever re-enabled.
            best.TakeDamage(damage, isExplosionOrPiercing: true);
            return;
        }

        transform.position = pos + direction * step;
        transform.Rotate(0f, 0f, spin * dt);
    }

    /// <summary>
    /// Builds a small orange shard with a trail, using the source meteor's sprite when available.
    /// Used when the Rock has no debris prefab assigned.
    /// </summary>
    public static GameObject CreateProcedural(Vector2 position, SpriteRenderer source)
    {
        GameObject go = new GameObject("MeteorDebris");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.3f;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (source != null && source.sprite != null)
        {
            sr.sprite = source.sprite;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder + 1;
        }
        else
        {
            sr.sprite = FallbackSprite;
            sr.sortingOrder = 3;
        }
        sr.color = new Color(1f, 0.62f, 0.25f, 1f);

        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.material = TrailMaterial;
        trail.time = 0.35f;
        trail.startWidth = 0.18f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.startColor = new Color(1f, 0.7f, 0.3f, 0.9f);
        trail.endColor = new Color(1f, 0.25f, 0.05f, 0f);
        trail.sortingLayerID = sr.sortingLayerID;
        trail.sortingOrder = sr.sortingOrder - 1;

        go.AddComponent<MeteorDebris>();
        return go;
    }

    private static Material TrailMaterial
    {
        get
        {
            if (trailMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default");
                if (s == null) s = Shader.Find("Unlit/Color");
                trailMaterial = new Material(s);
            }
            return trailMaterial;
        }
    }

    // 4x4 white pixel texture at 4 pixels-per-unit = a 1x1 unit square
    private static Sprite FallbackSprite
    {
        get
        {
            if (fallbackSprite == null)
            {
                fallbackSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            }
            return fallbackSprite;
        }
    }
}
