using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class CombatInteractor : NetworkBehaviour
{
    PlayerStats stats;
    PlayerClassState classState;

    [Header("Aim")]
    public Camera aimCamera;                 // 비워도 됨(Owner에서 Camera.main 자동)
    public LayerMask aimMask = ~0;
    public float aimMaxDistance = 200f;

    [Header("Shoot Origin")]
    public Transform shootOrigin;            // 무기/손/활 위치(없으면 임시 계산)

    [Header("Archer - Charge Shot")]
    public NetworkObject arrowPrefab;        // NetworkObject 프리팹
    public float minArrowSpeed = 18f;
    public float maxArrowSpeed = 35f;
    public float minArrowLife = 2.0f;
    public float maxArrowLife = 4.0f;
    public float chargeMaxTime = 1.2f;       // 이 시간까지 차지하면 100%

    [Header("Warrior - Slash Projectile")]
    public NetworkObject slashPrefab;        // 검기 프리팹(NetworkObject)
    public float slashSpeed = 40f;
    public float slashLifeTime = 0.6f;

    [Header("Healer - Basic Attack Projectile")]
    public NetworkObject healerBoltPrefab;   // 힐러 평타 투사체(NetworkObject)
    public float healerBoltSpeed = 28f;
    public float healerBoltLifeTime = 3.0f;

    [Header("Charge Visual (No Animator)")]
    public Transform chargeVisual;           // 활/손/무기 Transform(당기는 연출)
    public Vector3 chargeLocalOffset = new Vector3(0f, 0f, -0.12f);
    public float chargeVisualLerp = 12f;

    [Header("UI - Charge Bar")]
    public ChargeBarUI chargeBarUI;

    [Header("UI - Crosshair")]
    public CrosshairUI crosshairUI;

    bool hudBound;
    Coroutine hudBindRoutine;


    float nextActionTime;
    bool isCharging;
    float chargeStartTime;
    Vector3 chargeVisualBaseLocalPos;


    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        classState = GetComponent<PlayerClassState>();
    }

    void Start()
    {
        if (!IsOwner) return;

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (chargeVisual != null)
            chargeVisualBaseLocalPos = chargeVisual.localPosition;
    }

    void Update()
    {
        if (!IsOwner) return;

        var job = classState != null ? classState.CurrentJob : JobType.Warrior;

        // 좌클릭
        if (job == JobType.Archer)
        {
            if (Input.GetMouseButtonDown(0)) BeginCharge();
            if (Input.GetMouseButtonUp(0)) ReleaseCharge();

            UpdateChargeVisual();
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
                TryPrimaryInstant();
        }

        // 우클릭
        if (Input.GetMouseButtonDown(1))
            TrySecondary();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // 씬 전환/재스폰 대비: 이전 루틴 정리
        if (hudBindRoutine != null)
            StopCoroutine(hudBindRoutine);

        hudBindRoutine = StartCoroutine(CoBindHUDWhenReady());
    }

    IEnumerator CoBindHUDWhenReady()
    {
        hudBound = false;

        // 1) 활성 씬 로드 완료 대기
        yield return new WaitUntil(() =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded);

        float timeout = 5f;
        float elapsed = 0f;

        while (!hudBound && elapsed < timeout)
        {
            // 비활성 포함해서 찾기 (GameScene HUD에만 존재한다고 가정)
            if (chargeBarUI == null)
                chargeBarUI = FindAnyObjectByType<ChargeBarUI>(FindObjectsInactive.Include);

            if (crosshairUI == null)
                crosshairUI = FindAnyObjectByType<CrosshairUI>(FindObjectsInactive.Include);

            // 둘 다 준비되면 바인딩 완료
            if (chargeBarUI != null && crosshairUI != null)
            {
                // 초기 상태 세팅
                chargeBarUI.SetVisible(false);
                crosshairUI.SetCharge01(0f);   // 기본 크기/색으로

                hudBound = true;
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!hudBound)
        {
            Debug.LogWarning($"[CombatInteractor] HUD 바인딩 실패 " +
                             $"chargeBarUI={(chargeBarUI ? "OK" : "NULL")} " +
                             $"crosshairUI={(crosshairUI ? "OK" : "NULL")}");
        }
    }


    // -------------------------
    // Client-side input

    void BeginCharge()
    {
        if (Time.time < nextActionTime) return;

        isCharging = true;
        chargeStartTime = Time.time;

        if (chargeBarUI != null)
            chargeBarUI.SetVisible(true);
    }


    void ReleaseCharge()
    {
        if (!isCharging) return;
        isCharging = false;

        if (chargeBarUI != null)
        {
            chargeBarUI.SetCharge01(0f);
            chargeBarUI.SetVisible(false);
        }

        if (Time.time < nextActionTime) return;

        float charge01 = Mathf.Clamp01((Time.time - chargeStartTime) / chargeMaxTime);

        nextActionTime = Time.time + (stats != null ? stats.AttackCooldown : 0.5f);

        Vector3 aimPoint = GetAimPoint();
        RequestPrimaryRpc(aimPoint, charge01);
    }

    void AutoBindLocalChargeUI()
    {
        // 멀티에서 UI는 "로컬 플레이어"만 잡아야 함
        if (!IsOwner) return;

        if (chargeBarUI == null)
        {
            // 씬에 있는 ChargeBarUI 하나를 찾는다 (Canvas에 1개만 있다고 가정)
            chargeBarUI = FindAnyObjectByType<ChargeBarUI>(FindObjectsInactive.Include);
        }

        // 혹시라도 못 찾았으면 조용히 넘어감
        if (chargeBarUI != null)
            chargeBarUI.SetVisible(false);
    }

    void TryPrimaryInstant()
    {
        if (Time.time < nextActionTime) return;
        nextActionTime = Time.time + (stats != null ? stats.AttackCooldown : 0.5f);

        Vector3 aimPoint = GetAimPoint();
        RequestPrimaryRpc(aimPoint, 0f);
    }

    void TrySecondary()
    {
        if (Time.time < nextActionTime) return;
        nextActionTime = Time.time + 0.8f;

        RequestSecondaryRpc();
    }

    void UpdateChargeVisual()
    {
        float t = isCharging ? Mathf.Clamp01((Time.time - chargeStartTime) / chargeMaxTime) : 0f;
        if (chargeBarUI != null)
            chargeBarUI.SetCharge01(t);
        if (crosshairUI != null)
            crosshairUI.SetCharge01(t);


        // ✅ (옵션) 무기/손 당기는 연출(너가 이미 쓰던 것)
        if (chargeVisual != null)
        {
            Vector3 targetLocal = chargeVisualBaseLocalPos + chargeLocalOffset * t;
            chargeVisual.localPosition = Vector3.Lerp(chargeVisual.localPosition, targetLocal, Time.deltaTime * chargeVisualLerp);
        }
    }


    // -------------------------
    // Aim helpers (client)

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

    // -------------------------
    // Server-side occlusion fix (camera 없는 서버에서도 가능)
    // "클라가 보낸 aimPoint"를 기준으로, 무기 원점에서 레이 쏴서
    // 중간에 벽이 있으면 거기로 aimPoint를 당김
    Vector3 FixAimPointFromOrigin(Vector3 origin, Vector3 desiredAimPoint)
    {
        Vector3 dir = desiredAimPoint - origin;
        float dist = dir.magnitude;
        if (dist < 0.01f) return desiredAimPoint;

        dir /= dist;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, aimMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return desiredAimPoint;
    }

    Vector3 GetOriginPos()
    {
        if (shootOrigin != null) return shootOrigin.position;
        return transform.position + transform.forward * 1.0f + Vector3.up * 1.2f;
    }

    // -------------------------
    // RPCs

    [Rpc(SendTo.Server)]
    void RequestPrimaryRpc(Vector3 aimPoint, float charge01, RpcParams rpcParams = default)
    {
        var job = classState != null ? classState.CurrentJob : JobType.Warrior;

        switch (job)
        {
            case JobType.Warrior:
                ServerWarriorSlash(aimPoint);
                break;

            case JobType.Archer:
                ServerShootArrow(aimPoint, charge01);
                break;

            case JobType.Healer:
                ServerHealerBasicAttack(aimPoint);
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

    // -------------------------
    // Server: Warrior slash (aimPoint로)

    void ServerWarriorSlash(Vector3 aimPoint)
    {
        if (slashPrefab == null) return;

        int dmg = Mathf.Max(1, stats != null ? stats.Atk : 1);

        Vector3 originPos = GetOriginPos();
        Vector3 aimFixed = FixAimPointFromOrigin(originPos, aimPoint);

        Vector3 to = aimFixed - originPos;
        if (to.sqrMagnitude < 0.001f) to = transform.forward;

        var obj = Instantiate(slashPrefab, originPos, Quaternion.LookRotation(to));
        obj.Spawn(true);

        var slash = obj.GetComponent<SlashProjectile>();
        if (slash != null)
            slash.InitToTarget(aimFixed, dmg, slashSpeed, slashLifeTime);
    }

    // -------------------------
    // Server: Archer charge shot (aimPoint + charge01)

    void ServerShootArrow(Vector3 aimPoint, float charge01)
    {
        if (arrowPrefab == null) return;

        int baseDmg = Mathf.Max(1, stats != null ? stats.Atk : 1);

        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, charge01);
        float life = Mathf.Lerp(minArrowLife, maxArrowLife, charge01);
        int dmg = Mathf.RoundToInt(baseDmg * Mathf.Lerp(1.0f, 1.8f, charge01));

        Vector3 originPos = GetOriginPos();
        Vector3 aimFixed = FixAimPointFromOrigin(originPos, aimPoint);

        Vector3 to = aimFixed - originPos;
        if (to.sqrMagnitude < 0.001f) to = transform.forward;

        var obj = Instantiate(arrowPrefab, originPos, Quaternion.LookRotation(to));
        obj.Spawn(true);

        var arrow = obj.GetComponent<ArrowProjectile>();
        if (arrow != null)
            arrow.InitToTarget(aimFixed, dmg, speed, life, OwnerClientId);
    }

    // -------------------------
    // Server: Healer basic attack projectile (aimPoint로)

    void ServerHealerBasicAttack(Vector3 aimPoint)
    {
        if (healerBoltPrefab == null) return;

        int dmg = Mathf.Max(1, stats != null ? stats.Atk : 1);

        Vector3 originPos = GetOriginPos();
        Vector3 aimFixed = FixAimPointFromOrigin(originPos, aimPoint);

        Vector3 to = aimFixed - originPos;
        if (to.sqrMagnitude < 0.001f) to = transform.forward;

        var obj = Instantiate(healerBoltPrefab, originPos, Quaternion.LookRotation(to));
        obj.Spawn(true);

        // 힐러탄도 ArrowProjectile 재사용 가능
        var proj = obj.GetComponent<ArrowProjectile>();
        if (proj != null)
            proj.InitToTarget(aimFixed, dmg, healerBoltSpeed, healerBoltLifeTime, OwnerClientId);
    }

    // -------------------------
    // Server: Healer secondary heal

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
