using UnityEngine;
using Unity.Netcode;

public class CombatInteractor : NetworkBehaviour
{
    PlayerStats stats;
    float nextAttackTime;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    void Update()
    {
        if (!IsOwner) return;

        // 좌클릭: 공격(또는 역할별 기본 액션)
        if (Input.GetMouseButtonDown(0))
        {
            TryAttack();
        }

        // 우클릭: 힐러면 힐(임시)
        if (Input.GetMouseButtonDown(1))
        {
            TryHeal();
        }
    }

    void TryAttack()
    {
        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + (stats != null ? stats.AttackCooldown : 0.5f);

        RequestAttackRpc();
    }

    void TryHeal()
    {
        RequestHealRpc();
    }

    [Rpc(SendTo.Server)]
    void RequestAttackRpc()
    {
        // 서버에서 "내 앞"에 있는 타겟 찾기 (임시: 근거리)
        var myPos = transform.position;

        float range = stats != null ? stats.AttackRange : 1.8f;
        int dmg = stats != null ? stats.Atk : 1;

        // 가장 가까운 다른 플레이어 1명 타격 (테스트용)
        HealthNetwork best = null;
        float bestDist = 999f;

        foreach (var hn in FindObjectsByType<HealthNetwork>(FindObjectsSortMode.None))
        {
            if (hn.gameObject == gameObject) continue;
            float d = Vector3.Distance(myPos, hn.transform.position);
            if (d <= range && d < bestDist)
            {
                bestDist = d;
                best = hn;
            }
        }

        if (best != null)
            best.ServerTakeDamage(dmg);
    }

    [Rpc(SendTo.Server)]
    void RequestHealRpc()
    {
        // 임시: 가장 가까운 플레이어 힐 (힐러만 되게 나중에 직업 체크 넣을 것)
        var myPos = transform.position;

        HealthNetwork best = null;
        float bestDist = 999f;

        foreach (var hn in FindObjectsByType<HealthNetwork>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(myPos, hn.transform.position);
            if (d <= 4f && d < bestDist)
            {
                bestDist = d;
                best = hn;
            }
        }

        if (best != null)
            best.ServerHeal(5);
    }
}
