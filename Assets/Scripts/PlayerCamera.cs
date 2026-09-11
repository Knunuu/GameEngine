using UnityEngine;

public struct CameraInput
{
    public Vector2 LookAxis;
}

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    private float _pitch;
    private float _yaw;

    public void Initialize(Transform target)
    {
        transform.position = target.position;

        _yaw = target.eulerAngles.y;
        _pitch = Mathf.Clamp(target.eulerAngles.x > 180f ? target.eulerAngles.x - 360f : target.eulerAngles.x, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    public void UpdatePosition(Transform target)
    {
        transform.position = target.position;
    }

    public void UpdateRotation(CameraInput input)
    {
        _pitch = Mathf.Clamp(_pitch - input.LookAxis.y * sensitivity, minPitch, maxPitch);
        _yaw += input.LookAxis.x * sensitivity;

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }
}
