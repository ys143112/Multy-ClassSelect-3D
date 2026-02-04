using UnityEngine;
using TMPro;
using Unity.Netcode;

public class ClassSelectUIController : MonoBehaviour
{
    [Header("Data")]
    public ClassDatabase classDatabase;

    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text statText;

    private JobType current = JobType.Warrior;

    void Start()
    {
        Preview(current);
        TrySendSelection(current);
    }

    public void ClickWarrior() => Select(JobType.Warrior);
    public void ClickArcher() => Select(JobType.Archer);
    public void ClickHealer() => Select(JobType.Healer);

    void Select(JobType job)
    {
        current = job;
        Preview(job);
        TrySendSelection(job);
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

    void TrySendSelection(JobType job)
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsClient) return;

        var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (!playerObj) return; // 아직 스폰 전일 수 있음

        var state = playerObj.GetComponent<PlayerClassState>();
        if (!state) return;

        state.RequestSetJobRpc((int)job);
    }
}
