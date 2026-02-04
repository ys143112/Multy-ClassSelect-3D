using UnityEngine;
using Unity.Netcode;

public class CameraFollow : NetworkBehaviour
{
    public Vector3 offset = new Vector3(0, 8, -6);

    Transform target;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        target = transform;
    }

    void LateUpdate()
    {
        if (!target) return;

        Camera.main.transform.position = target.position + offset;
        Camera.main.transform.LookAt(target.position);
    }
}
