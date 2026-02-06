using UnityEngine;
using Unity.Netcode;

public class EnemyStats : NetworkBehaviour
{
    public NetworkVariable<int> Hp =
        new(30, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        Hp.Value = 30;
    }

    // =========================
    // 데미지 처리
    // =========================
    public void TakeDamage(int dmg)
    {
        if (!IsServer) return;

        Hp.Value = Mathf.Max(0, Hp.Value - dmg);
        if (Hp.Value == 0)
            Die();
    }

    void Die()
    {
        if (!IsServer) return;
        NetworkObject.Despawn();
    }

    // =========================
    // 🔥 히트 피드백 (맞춘 사람만)
    // =========================

    // ⭕ ClientRpc (특정 클라이언트 지정 가능)
    [ClientRpc]
    void HitFeedbackClientRpc(float intensity01, ClientRpcParams clientRpcParams = default)
    {
        if (HitFeedbackHub.Instance != null)
            HitFeedbackHub.Instance.PlayHitFeedback(intensity01);
    }

    /// <summary>
    /// 서버에서 호출:
    /// 이 적을 공격한 플레이어(clientId)에게만 히트 피드백 전송
    /// </summary>
    public void ServerNotifyHitFeedback(ulong attackerClientId, float intensity01 = 1f)
    {
        if (!IsServer) return;

        var targets = new ulong[] { attackerClientId };

        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = targets
            }
        };

        HitFeedbackClientRpc(intensity01, clientRpcParams);
    }
}
