using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : NetworkBehaviour
{
    [Header("Move")]
    public float walkSpeed = 5f;
    public float runMultiplier = 1.6f;
    public float airControl = 0.6f;

    [Header("Jump/Gravity")]
    public float gravity = -20f;
    public float jumpHeight = 1.25f;

    [Header("Double Jump")]
    public int maxJumps = 2;              // 2면 더블점프(총 2번)
    public float doubleJumpHeight = 1.1f; // 2번째 점프 높이(조금 낮게 추천)

    CharacterController cc;
    float yVel;
    int jumpsUsed;

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

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 inputMove = (transform.right * h + transform.forward * v);
        if (inputMove.sqrMagnitude > 1f) inputMove.Normalize();

        bool run = Input.GetKey(KeyCode.LeftShift);
        float targetSpeed = walkSpeed * (run ? runMultiplier : 1f);

        // 지면 처리
        if (cc.isGrounded)
        {
            // 바닥에 닿으면 점프 횟수 리셋
            jumpsUsed = 0;

            if (yVel < 0f) yVel = -2f;
        }

        // 점프 입력
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (jumpsUsed < maxJumps)
            {
                bool isFirstJump = (jumpsUsed == 0);
                float hgt = isFirstJump ? jumpHeight : doubleJumpHeight;

                // 점프 시작 시 하강 중이면 yVel 리셋(공중에서 2단 점프할 때 답답함 방지)
                if (yVel < 0f) yVel = 0f;

                yVel = Mathf.Sqrt(hgt * -2f * gravity);
                jumpsUsed++;
            }
        }

        // 중력
        yVel += gravity * Time.deltaTime;

        // 공중 제어
        float control = cc.isGrounded ? 1f : airControl;
        Vector3 planarVel = inputMove * (targetSpeed * control);

        Vector3 vel = planarVel + Vector3.up * yVel;
        cc.Move(vel * Time.deltaTime);
    }

    public void SetSpeed(float s) => walkSpeed = s;
}
