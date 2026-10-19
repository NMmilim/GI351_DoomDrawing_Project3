using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders a crackling, jagged chain lightning beam across a sequence of waypoints,
/// shows a glowing electric trail, and fades out over a specified duration before disappearing.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ChainLightningBeam : MonoBehaviour
{
    [Header("Beam Aesthetics")]
    [SerializeField] private float startWidth = 0.35f;
    [SerializeField] private float endWidth = 0.18f;
    [SerializeField] private int segmentsPerLink = 4;
    [SerializeField] private float jitterAmount = 0.18f;
    [SerializeField] private Color coreColor = Color.white;
    [SerializeField] private Color glowColor = new Color(0.2f, 0.85f, 1f, 1f); // Electric Cyan

    private LineRenderer lineRenderer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        ConfigureLineRenderer();
    }

    private void ConfigureLineRenderer()
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;

        // Create vibrant electric cyan to white gradient
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(coreColor, 0.0f),
                new GradientColorKey(glowColor, 0.4f),
                new GradientColorKey(glowColor, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.9f, 0.7f),
                new GradientAlphaKey(0.7f, 1.0f)
            }
        );
        lineRenderer.colorGradient = gradient;

        // Ensure a valid unlit sprite/line material
        if (lineRenderer.material == null || lineRenderer.material.name.Contains("Default"))
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
            }
        }
    }

    /// <summary>
    /// Builds the jagged electric path through all waypoints and begins the fade-out trail routine.
    /// </summary>
    public void Initialize(Vector3[] waypoints, float duration)
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        List<Vector3> points = new List<Vector3>();

        if (waypoints.Length == 1)
        {
            points.Add(waypoints[0]);
        }
        else
        {
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                Vector3 start = waypoints[i];
                Vector3 end = waypoints[i + 1];
                Vector3 dir = end - start;
                float dist = dir.magnitude;
                Vector3 normal = Vector3.Cross(dir.normalized, Vector3.forward);

                points.Add(start);

                // Add jagged lightning sub-segments with lateral displacement
                int segments = Mathf.Max(2, Mathf.RoundToInt(dist * segmentsPerLink * 0.45f));
                for (int s = 1; s < segments; s++)
                {
                    float t = (float)s / segments;
                    Vector3 basePos = Vector3.Lerp(start, end, t);
                    float offset = UnityEngine.Random.Range(-jitterAmount, jitterAmount);
                    points.Add(basePos + normal * offset);
                }
            }
            points.Add(waypoints[waypoints.Length - 1]);
        }

        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());

        StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float elapsed = 0f;
        float initialStart = startWidth;
        float initialEnd = endWidth;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Taper width down to zero smoothly to simulate an electric flash trail disappearing
            lineRenderer.startWidth = Mathf.Lerp(initialStart, 0f, t);
            lineRenderer.endWidth = Mathf.Lerp(initialEnd, 0f, t);

            yield return null;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Static factory method to instantiate a chain lightning beam in world space.
    /// </summary>
    public static ChainLightningBeam Spawn(Vector3[] waypoints, float duration, GameObject customPrefab = null)
    {
        GameObject beamObj;
        ChainLightningBeam beam;

        if (customPrefab != null)
        {
            beamObj = Instantiate(customPrefab);
            beam = beamObj.GetComponent<ChainLightningBeam>();
            if (beam == null) beam = beamObj.AddComponent<ChainLightningBeam>();
        }
        else
        {
            beamObj = new GameObject("ChainLightningBeam");
            beamObj.AddComponent<LineRenderer>();
            beam = beamObj.AddComponent<ChainLightningBeam>();
        }

        beam.Initialize(waypoints, duration);
        return beam;
    }
}
