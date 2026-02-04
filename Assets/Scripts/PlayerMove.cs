using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : NetworkBehaviour
{
    public float speed = 5f;

    CharacterController cc;
    Camera cam;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // ❗ 로컬 플레이어만 입력 처리
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        cam = Camera.main;
    }

    void Update()
    {
        if (!IsOwner) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = new Vector3(h, 0, v);
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        cc.Move(move * speed * Time.deltaTime);
    }

    // 전직 스탯 반영용
    public void SetSpeed(float moveSpeed)
    {
        speed = moveSpeed;
    }
}
