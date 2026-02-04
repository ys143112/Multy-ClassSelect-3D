using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class EnemyAI : NetworkBehaviour
{
    public float moveSpeed = 3f;
    public float chaseRange = 10f;

    CharacterController cc;

    void Awake() => cc = GetComponent<CharacterController>();

    public float gravity = -20f;
    private float yVel;

    void Update()
    {
        if (!IsServer) return;

        Transform target = FindNearestPlayer();
        if (!target) return;

        Vector3 to = target.position - transform.position;
        if (to.magnitude > chaseRange) return;

        // 회전은 수평만
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        if (flat.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.LookRotation(flat);

        // 수평 이동
        Vector3 move = flat.normalized * moveSpeed;

        // 중력(지면 붙이기)
        if (cc.isGrounded && yVel < 0f) yVel = -2f;
        yVel += gravity * Time.deltaTime;
        move.y = yVel;

        cc.Move(move * Time.deltaTime);
    }


    Transform FindNearestPlayer()
    {
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (var p in FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = p.transform;
            }
        }
        return best;
    }
}
