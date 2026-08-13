using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy", menuName = "Game/Enemy")]
public class EnemyData : ScriptableObject
{
    [Header("Identidade")]
    public string enemyName;
    [TextArea(2, 4)] public string description;
    public Sprite portrait;
    public BiomeType biome = BiomeType.Any;
    public bool isBoss;

    [Header("Atributos")]
    public int maxHp = 30;
    public int attackDamage = 6;
    public int blockAmount = 5;
    public int stressDamage = 8;

    [Header("Comportamento")]
    [Range(0, 100)] public int attackWeight = 60;
    [Range(0, 100)] public int defendWeight = 15;
    [Range(0, 100)] public int stressWeight = 15;
    [Range(0, 100)] public int attackAllWeight = 10;

    [Header("Recompensa")]
    public int goldReward = 25;
}

public enum EnemyIntent
{
    Attack,
    AttackAll,
    Defend,
    Stress
}
