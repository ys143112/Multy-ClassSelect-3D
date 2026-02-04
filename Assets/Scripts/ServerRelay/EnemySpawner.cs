using UnityEngine;
using Unity.Netcode;

public class EnemySpawner : NetworkBehaviour
{
    public EnemyAI enemyPrefab;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        for (int i = 0; i < 3; i++)
            SpawnEnemy(new Vector3(i * 3, 0, 6));
    }

    void SpawnEnemy(Vector3 pos)
    {
        var enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);
        enemy.GetComponent<NetworkObject>().Spawn(true);
    }
}
