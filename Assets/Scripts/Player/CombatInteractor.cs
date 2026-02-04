using UnityEngine;
using Unity.Netcode;

public class CombatInteractor : NetworkBehaviour
{
    PlayerStats stats;
    PlayerClassState classState;

    [Header("Animation")]
    public Animator animator;
    static readonly int HashIsCharging = Animator.StringToHash("IsCharging");
    static readonly int HashCharge = Animator.StringToHash("Charge");
    static readonly int HashShoot = Animator.StringToHash("Shoot");

    [Header("Aim")]
    public Camera aimCamera;         // 비워도 됨(자동으로 Camera.main 씀)
    public LayerMask aimMask = ~0;
    public float aimMaxDistance = 200f;

    [Header("Warrior Slash")]
    public NetworkObject warriorSlashPrefab;
    public float slashSpawnForward = 1.2f;
    public float slashSpawnUp = 1.1f;

    [Header("Archer")]
    public NetworkObject arrowPrefab;     // NetworkObject 프리팹
    public Transform shootOrigin;         // 없으면 자동 계산
    public float minArrowSpeed = 18f;
    public float maxArrowSpeed = 35f;
    public float minArrowLife = 2.0f;
    public float maxArrowLife = 4.0f;
    public float chargeMaxTime = 1.2f;    // 이 시간까지 차지하면 100%

    [Header("Healer Basic Attack")]
    public NetworkObject healerBoltPrefab; // 힐러 평타 투사체(없으면 임시로 null 체크됨)

    float nextActionTime;

    bool isCharging;
    float chargeStartTime;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        classState = GetComponent<PlayerClassState>();
    }

    void Start()
    {
        if (!IsOwner) return;
        if (aimCamera == null) aimCamera = Camera.main;
    }

    void Update()
    {
        if (!IsOwner) return;

        var job = classState != null ? classState.CurrentJob : JobType.Warrior;

        if (job == JobType.Archer)
        {
            if (isCharging && animator)
            {
                float t = Mathf.Clamp01((Time.time - chargeStartTime) / chargeMaxTime);
                animator.SetFloat(HashCharge, t);
            }

            if (Input.GetMouseButtonDown(0)) BeginCharge();
            if (Input.GetMouseButtonUp(0)) ReleaseCharge();
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
                TryPrimaryInstant(); // 전사/힐러
        }

        if (Input.GetMouseButtonDown(1))
            TrySecondary();
    }

    void BeginCharge()
    {
        if (Time.time < nextActionTime) return;

        isCharging = true;
        chargeStartTime = Time.time;

        if (animator)
        {
            animator.SetBool(HashIsCharging, true);
            animator.SetFloat(HashCharge, 0f);
        }
    }

    void ReleaseCharge()
    {
        if (!isCharging) return;
        isCharging = false;

        if (animator)
        {
            animator.SetBool(HashIsCharging, false);
            animator.SetTrigger(HashShoot);
        }

        if (Time.time < nextActionTime) return;

        float chargeTime = Time.time - chargeStartTime;
        float charge01 = Mathf.Clamp01(chargeTime / chargeMaxTime);

        nextActionTime = Time.time + (stats != null ? stats.AttackCooldown : 0.5f);

        Vector3 aimPoint = GetAimPoint();
        RequestPrimaryRpc(aimPoint, charge01);
    }

    void TryPrimaryInstant()
    {
        if (Time.time < nextActionTime) return;
        nextActionTime = Time.time + (stats != null ? stats.AttackCooldown : 0.5f);

        Vector3 aimPoint = GetAimPoint();
        RequestPrimaryRpc(aimPoint, 0f); // 전사/힐러는 차지 없음
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

    // ✅ aimPoint + charge01 서버로 전달
    [Rpc(SendTo.Server)]
    void RequestPrimaryRpc(Vector3 aimPoint, float charge01, RpcParams rpcParams = default)
    {
        var job = classState != null ? classState.CurrentJob : JobType.Warrior;

        switch (job)
        {
            case JobType.Warrior:
                ServerMeleeAttack();                 // 전사 평타(근접)
                ServerWarriorSlash(aimPoint);
                break;

            case JobType.Archer:
                ServerShootArrow(aimPoint, charge01); // 아처 차지샷
                break;

            case JobType.Healer:
                ServerHealerBasicAttack(aimPoint);    // 힐러 평타(공격 투사체)
                break;
        }
    }

    [Rpc(SendTo.Server)]
    void RequestSecondaryRpc()
    {
        var job = classState != null ? classState.CurrentJob : JobType.Warrior;

        // 우클릭: 힐러 힐(기존 컨셉 유지)
        if (job == JobType.Healer)
            ServerHealNearest(12);
    }

    // -------------------------
    // Warrior: 근접 평타(기존)
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

    void ServerWarriorSlash(Vector3 aimPoint)
    {
        if (warriorSlashPrefab == null) return;

        int dmg = Mathf.Max(1, stats != null ? stats.Atk : 1);

        Vector3 originPos = transform.position + transform.forward * slashSpawnForward + Vector3.up * slashSpawnUp;

        Vector3 to = (aimPoint - originPos);
        to.y = 0f; // 검기는 보통 수평으로
        if (to.sqrMagnitude < 0.001f) to = transform.forward;

        var obj = Instantiate(warriorSlashPrefab, originPos, Quaternion.LookRotation(to));
        obj.Spawn(true);

        var slash = obj.GetComponent<WarriorSlashProjectile>();
        if (slash != null)
            slash.Init(to, dmg);
    }


    // -------------------------
    // Healer: 공격 평타(투사체)
    void ServerHealerBasicAttack(Vector3 aimPoint)
    {
        if (healerBoltPrefab == null) return;

        int dmg = Mathf.Max(1, stats != null ? stats.Atk : 1);

        Vector3 originPos = (shootOrigin != null)
            ? shootOrigin.position
            : transform.position + transform.forward * 1.0f + Vector3.up * 1.2f;

        Vector3 to = aimPoint - originPos;
        if (to.sqrMagnitude < 0.001f) to = transform.forward;

        var boltObj = Instantiate(healerBoltPrefab, originPos, Quaternion.LookRotation(to));
        boltObj.Spawn(true);

        // healerBoltPrefab에 ArrowProjectile을 재사용해도 됨(“타겟으로 날아가는 마법탄”)
        var proj = boltObj.GetComponent<ArrowProjectile>();
        if (proj != null)
        {
            proj.InitToTarget(aimPoint, dmg, 28f, 3.0f);
        }
    }

    // -------------------------
    // Archer: 차지샷(속도/수명/데미지 스케일)
    void ServerShootArrow(Vector3 aimPoint, float charge01)
    {
        if (arrowPrefab == null) return;

        int baseDmg = Mathf.Max(1, stats != null ? stats.Atk : 1);

        // 차지 스케일(원하면 여기만 바꿔도 손맛 크게 바뀜)
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, charge01);
        float life = Mathf.Lerp(minArrowLife, maxArrowLife, charge01);

        // 데미지도 살짝 증가(예: 1.0x ~ 1.8x)
        int dmg = Mathf.RoundToInt(baseDmg * Mathf.Lerp(1.0f, 1.8f, charge01));

        Vector3 originPos = (shootOrigin != null)
            ? shootOrigin.position
            : transform.position + transform.forward * 1.0f + Vector3.up * 1.2f;

        Vector3 to = aimPoint - originPos;
        if (to.sqrMagnitude < 0.001f) to = transform.forward;

        var arrowObj = Instantiate(arrowPrefab, originPos, Quaternion.LookRotation(to));
        arrowObj.Spawn(true);

        var arrow = arrowObj.GetComponent<ArrowProjectile>();
        if (arrow != null)
        {
            arrow.InitToTarget(aimPoint, dmg, speed, life);
        }
    }

    void ServerHealNearest(int amount)
    {
        var target = FindNearestPlayerInRange(4f);
        if (target != null)
            target.ServerHeal(amount);
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
