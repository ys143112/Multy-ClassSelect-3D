using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;

public class ClassSelectUIController : MonoBehaviour
{
    [Header("Data")]
    public ClassDatabase classDatabase;

    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text statText;

    private JobType current = JobType.Warrior;

    private bool pendingSend;   // 아직 못 보낸 선택이 있다
    private bool sending;       // 코루틴 중복 방지

    void Awake()
    {
        Debug.Log("[UI] ClassSelectUIController Awake");
    }

    void Start()
    {
        Debug.Log("[UI] ClassSelectUIController Start");
        Preview(current);
        QueueSend(current);
    }

    public void ClickArcher()
    {
        Debug.Log("[UI] ClickArcher pressed");
        Select(JobType.Archer);
    }


    public void ClickWarrior() => Select(JobType.Warrior);
    //public void ClickArcher() => Select(JobType.Archer);
    public void ClickHealer() => Select(JobType.Healer);

    void Select(JobType job)
    {
        current = job;
        SelectedJobCache.Selected = job;   // ✅ 저장
        Preview(job);
        // TrySendSelection(job);  // ❌ 여기서 보내지 말기
    }


    void Preview(JobType job)
    {
        var def = classDatabase.Get(job);
        if (def == null) return;

        if (nameText) nameText.text = def.displayName;
        if (statText)
        {
            statText.text =
                $"HP: {def.baseHp}\n" +
                $"ATK: {def.baseAtk}\n" +
                $"SPD: {def.moveSpeed}";
        }
    }

    void QueueSend(JobType job)
    {
        pendingSend = true;

        if (!sending)
            StartCoroutine(CoTrySendWhenReady());
    }

    IEnumerator CoTrySendWhenReady()
    {
        sending = true;

        // 네트워크 준비 대기
        while (!NetworkManager.Singleton || !NetworkManager.Singleton.IsClient)
            yield return null;

        // PlayerObject 스폰 대기
        while (NetworkManager.Singleton.LocalClient == null ||
               NetworkManager.Singleton.LocalClient.PlayerObject == null)
            yield return null;

        // 여기까지 오면 이제 보낼 수 있음
        if (pendingSend)
        {
            pendingSend = false;

            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            var state = playerObj.GetComponent<PlayerClassState>();

            Debug.Log($"[UI] Sending job={(int)current} to PlayerClassState. " +
                      $"playerObj={playerObj.name}, state={(state ? "OK" : "NULL")}");

            if (state != null && state.NetworkObject != null && state.NetworkObject.IsSpawned)
            {
                state.RequestSetJobRpc((int)current);
            }
            else
            {
                Debug.LogWarning("[UI] PlayerClassState not spawned yet. Will retry.");
                pendingSend = true;
                yield return null;
                StartCoroutine(CoTrySendWhenReady());
            }
        }

        sending = false;
    }
}
