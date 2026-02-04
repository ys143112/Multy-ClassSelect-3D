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
}
