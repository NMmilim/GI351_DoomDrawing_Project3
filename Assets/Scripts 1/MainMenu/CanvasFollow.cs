using UnityEngine;
// 1. Import the New Input System namespace
using UnityEngine.InputSystem;

public class CanvasFollow : MonoBehaviour
{
    [Header("Subtle Settings")]
    [Tooltip("Keep this low (10 to 20 pixels) for a high-end, subtle feel.")]
    public float maxMovementAmount = 15f;

    [Tooltip("Lower values (1 to 3) make the motion feel heavy and organic.")]
    public float smoothSpeed = 2f;

    private RectTransform rectTransform;
    private Vector2 startPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        startPosition = rectTransform.anchoredPosition;
    }

    void Update()
    {
        // 2. Check if a mouse is connected to prevent errors
        if (Mouse.current == null) return;

        // 3. Get mouse position using the New Input System API
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // 4. Get normalized mouse coordinates (-1 to 1) relative to screen center
        float normalizedX = (mousePos.x / Screen.width) * 2f - 1f;
        float normalizedY = (mousePos.y / Screen.height) * 2f - 1f;

        // 5. Inverted directional offset (standard luxury menu behavior):
        // When mouse goes right, background slips left slightly to create depth.
        float targetX = startPosition.x - (normalizedX * maxMovementAmount);
        float targetY = startPosition.y - (normalizedY * maxMovementAmount);
        Vector2 targetPosition = new Vector2(targetX, targetY);

        // 6. Ultra-smooth framerate-independent glide
        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            targetPosition,
            Time.deltaTime * smoothSpeed
        );
    }
}
