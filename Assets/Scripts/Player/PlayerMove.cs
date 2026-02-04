using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : NetworkBehaviour
{
    public float speed = 5f;
    public float gravity = -20f;

    CharacterController cc;
    float yVel;

    void Awake() => cc = GetComponent<CharacterController>();

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = (transform.right * h + transform.forward * v);
        if (move.sqrMagnitude > 1f) move.Normalize();

        // 중력 (CharacterController용)
        if (cc.isGrounded && yVel < 0f) yVel = -2f;
        yVel += gravity * Time.deltaTime;

        Vector3 vel = move * speed + Vector3.up * yVel;
        cc.Move(vel * Time.deltaTime);
    }

    public void SetSpeed(float s) => speed = s;
}
