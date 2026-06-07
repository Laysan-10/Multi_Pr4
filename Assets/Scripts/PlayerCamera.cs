using FishNet.Object;
using UnityEngine;

public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private Vector3 playerOffset = new(0f, 8f, -6f);
    [SerializeField] private Vector3 vehicleOffset = new(0f, 4f, -8f);

    private Camera _cam;
    private Transform _followTarget;
    private bool _followingVehicle;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (!Owner.IsLocalClient)
        {
            enabled = false;
            return;
        }

        _cam = Camera.main;
        _followTarget = transform;
    }

    public void SetFollowTarget(Transform target, bool isVehicle)
    {
        if (!Owner.IsLocalClient)
            return;

        _followTarget = target ? target : transform;
        _followingVehicle = isVehicle;
    }

    private void LateUpdate()
    {
        if (!_cam)
            return;

        Transform target = _followTarget ? _followTarget : transform;
        Vector3 offset = _followingVehicle ? vehicleOffset : playerOffset;

        _cam.transform.position = target.position + offset;
        _cam.transform.LookAt(target.position + Vector3.up * (_followingVehicle ? 1f : 0f));
    }
}
