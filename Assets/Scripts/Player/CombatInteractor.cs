using UnityEngine;
using Unity.Netcode;

public class CombatInteractor : NetworkBehaviour
{
    PlayerStats stats;
    PlayerClassState classState;

    [Header("Archer")]
    public NetworkObject arrowPrefab;     // ✅ NetworkObject 프리팹(네트워크 프리팹 등록 필요)
    public Transform shootOrigin;         // ✅ 화살 시작 위치(없으면 임시로 계산)
    public Camera aimCamera;              // ✅ 로컬 카메라(숄더뷰)
    public LayerMask aimMask = ~0;        // ✅ 맞출 레이어(기본: 전부)
    public float aimMaxDistance = 200f;

    float nextActionTime;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        classState = GetComponent<PlayerClassState>();
    }

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetMouseButtonDown(0))
            TryPrimary();

        if (Input.GetMouseButtonDown(1))
            TrySecondary();
    }

    void TryPrimary()
    {
        if (Time.time < nextActionTime) return;
        nextActionTime = Time.time + (stats != null ? stats.AttackCooldown : 0.5f);

        // ✅ 조준점(월드 좌표)을 클라에서 계산해서 서버로 전달
        Vector3 aimPoint = GetAimPoint();
        RequestPrimaryRpc(aimPoint);
    }

    void TrySecondary()
    {
        if (Time.time < nextActionTime) return;
        nextActionTime = Time.time + 0.8f;

        RequestSecondaryRpc();
    }

    Vector3 GetAimPoint()
    {
        var cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null)
            return transform.position + transform.forward * 20f;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, aimMaxDistance, aimMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return ray.GetPoint(aimMaxDistance);
    }

    // ✅ aimPoint를 서버로 보냄
    [Rpc(SendTo.Server)]
    void RequestPrimaryRpc(Vector3 aimPoint, RpcParams rpcParams = default)
    {
        var job = classState != null ? classState.CurrentJob : JobType.Warrior;

        switch (job)
        {
            case JobType.Warrior:
                ServerMeleeAttack();
                break;

            case JobType.Archer:
                ServerShootArrow(aimPoint);
                break;

            case JobType.Healer:
                ServerHealNearest(6);
                break;
        }
    }

    [Rpc(SendTo.Server)]
    void RequestSecondaryRpc()
    {
        var job = classState != null ? classState.CurrentJob : JobType.Warrior;

        if (job == JobType.Healer)
            ServerHealNearest(12);
    }

    void ServerMeleeAttack()
    {
        float range = stats.AttackRange;
        int dmg = stats.Atk;

        foreach (var enemy in FindObjectsByType<EnemyStats>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, enemy.transform.position);
            if (d <= range)
            {
                enemy.TakeDamage(dmg);
                break;
            }
        }
    }

    void ServerHealNearest(int amount)
    {
        var target = FindNearestPlayerInRange(4f);
        if (target != null)
            target.ServerHeal(amount);
    }

    // ✅ 숄더뷰: aimPoint(월드)로 발사
    void ServerShootArrow(Vector3 aimPoint)
    {
        if (arrowPrefab == null) return;

        int dmg = stats != null ? stats.Atk : 1;

        Vector3 originPos;
        Quaternion rot;

        // 발사 원점
        if (shootOrigin != null)
            originPos = shootOrigin.position;
        else
            originPos = transform.position + transform.forward * 1.0f + Vector3.up * 1.2f;

        Vector3 to = (aimPoint - originPos);
        if (to.sqrMagnitude < 0.001f)
            to = transform.forward;

        rot = Quaternion.LookRotation(to);

        var arrowObj = Instantiate(arrowPrefab, originPos, rot);
        arrowObj.Spawn(true);

        var arrow = arrowObj.GetComponent<ArrowProjectile>();
        if (arrow != null)
        {
            // ✅ 너가 바꾼 ArrowProjectile에 맞춰 호출
            arrow.InitToTarget(aimPoint, dmg);
        }
    }

    HealthNetwork FindNearestPlayerInRange(float range)
    {
        var myPos = transform.position;
        HealthNetwork best = null;
        float bestDist = 999f;

        foreach (var hn in FindObjectsByType<HealthNetwork>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(myPos, hn.transform.position);
            if (d <= range && d < bestDist)
            {
                bestDist = d;
                best = hn;
            }
        }
        return best;
    }
}
