using UnityEngine;

public class player_controller : MonoBehaviour
{
    private Transform mouse_transform;
    private void MouseAim()
    {
        Vector2 mouseposition = Camera.main.ScreenToWorldPoint(Input.mousePosition) - mouse_transform.position;
        float angle = Mathf.Atan2(mouseposition.y, mouseposition.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        mouse_transform.rotation = rotation;
    }

    void Update()
    {
    MouseAim();
    }
}
