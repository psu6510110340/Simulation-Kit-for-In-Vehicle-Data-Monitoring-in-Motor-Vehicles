using UnityEngine;

public class RainFollowCamera : MonoBehaviour
{
    public Transform cam;

    void LateUpdate()
    {
        if (cam == null) return;
        transform.position = new Vector3(
            cam.position.x,
            cam.position.y + 20f,
            cam.position.z
        );
    }
}
