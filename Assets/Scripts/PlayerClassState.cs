using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class PlayerClassState : NetworkBehaviour
{
    public NetworkVariable<int> JobId =
        new((int)JobType.Warrior,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        JobId.OnValueChanged += OnJobChanged;

        // 씬 로드될 때마다 재적용(특히 ClassSelect -> GameScene 전환 시 필요)
        if (IsClient)
            NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;

        // 스폰 직후도 1회 적용
        ApplyNow();
    }

    public override void OnNetworkDespawn()
    {
        JobId.OnValueChanged -= OnJobChanged;

        if (NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
    }

    void OnJobChanged(int prev, int cur)
    {
        ApplyNow();
    }

    void OnSceneLoadCompleted(string sceneName, LoadSceneMode mode,
        System.Collections.Generic.List<ulong> clientsCompleted,
        System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        // GameScene 로드가 끝난 타이밍에 DB 홀더가 생기므로 여기서 다시 적용
        ApplyNow();
    }

    void ApplyNow()
    {
        // Unity 6에서 더 안정적으로 찾기
        var holder = Object.FindAnyObjectByType<ClassDatabaseHolder>();
        if (holder == null || holder.Database == null)
        {
            Debug.LogWarning("[PlayerClassState] ClassDatabaseHolder/Database를 아직 못 찾음");
            return;
        }

        var def = holder.Database.Get((JobType)JobId.Value);
        var job = GetComponent<PlayerJob>();
        if (job == null)
        {
            Debug.LogError("[PlayerClassState] PlayerJob이 Player에 없음");
            return;
        }

        job.Apply(def);
    }

    [Rpc(SendTo.Server)]
    public void RequestSetJobRpc(int jobId)
    {
        JobId.Value = jobId;
    }
}
