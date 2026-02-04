using UnityEngine;
using Unity.Netcode;

public class HealthNetwork : NetworkBehaviour
{
    public NetworkVariable<int> CurrentHp =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> MaxHpNet =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    PlayerStats stats;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            MaxHpNet.Value = stats != null ? stats.MaxHp : 10;
            CurrentHp.Value = MaxHpNet.Value;
        }
    }

    public void ServerTakeDamage(int dmg)
    {
        if (!IsServer) return;   // 👈 서버만 실행
        if (dmg <= 0) return;

        CurrentHp.Value = Mathf.Max(0, CurrentHp.Value - dmg);
    }

    public void ServerHeal(int amount)
    {
        if (!IsServer) return;   // 👈 서버만 실행
        if (amount <= 0) return;

        CurrentHp.Value =
            Mathf.Min(MaxHpNet.Value, CurrentHp.Value + amount);
    }

}
