using UnityEngine;

public enum JobType
{
    Warrior,
    Archer,
    Healer
}

[CreateAssetMenu(menuName = "RPG/Class Definition")]
public class ClassDefinition : ScriptableObject
{
    public JobType id;

    public string displayName;
    public int baseHp;
    public int baseAtk;
    public float moveSpeed;
}
