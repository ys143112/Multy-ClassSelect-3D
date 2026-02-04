using UnityEngine;

public enum JobType { Warrior = 0, Archer = 1, Healer = 2 }


[CreateAssetMenu(menuName = "RPG/Class Definition")]
public class ClassDefinition : ScriptableObject
{
    public JobType id;

    public string displayName;
    public int baseHp;
    public int baseAtk;
    public float moveSpeed;
}
