using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnemySpawner : NetworkBehaviour
{
    public EnemyAI enemyPrefab;

    [Header("Spawn Settings")]
    public int initialCount = 3;
    public float respawnDelay = 3f;

    // 스폰 위치 저장(죽으면 여기로 다시 스폰)
    readonly List<Vector3> spawnPoints = new();
    readonly Dictionary<ulong, int> enemyToSpawnIndex = new(); // enemyNetworkId -> spawnIndex

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        spawnPoints.Clear();
        enemyToSpawnIndex.Clear();

        for (int i = 0; i < initialCount; i++)
        {
            Vector3 pos = new Vector3(i * 3, 0, 6);
            spawnPoints.Add(pos);
            SpawnEnemyAtIndex(i);
        }
    }

    void SpawnEnemyAtIndex(int index)
    {
        if (!IsServer) return;
        if (index < 0 || index >= spawnPoints.Count) return;

        Vector3 pos = spawnPoints[index];

        var enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);
        var netObj = enemy.GetComponent<NetworkObject>();
        netObj.Spawn(true);

        // EnemyStats에 스포너 정보 주입
        var stats = enemy.GetComponent<EnemyStats>();
        if (stats != null)
            stats.ServerInitSpawner(this, index);

        // 추적용(선택)
        enemyToSpawnIndex[netObj.NetworkObjectId] = index;
    }

    // EnemyStats가 서버에서 호출하는 콜백
    public void ServerOnEnemyDied(int spawnIndex)
    {
        if (!IsServer) return;
        StartCoroutine(CoRespawn(spawnIndex));
    }

    IEnumerator CoRespawn(int spawnIndex)
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnEnemyAtIndex(spawnIndex);
    }
}
