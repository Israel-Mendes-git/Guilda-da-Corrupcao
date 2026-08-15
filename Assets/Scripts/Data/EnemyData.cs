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

    /// <summary>
    /// Tingimento do retrato no combate.
    ///
    /// Existe porque há 11 inimigos e 7 criaturas desenhadas: a mesma arte serve
    /// a mais de um inimigo, e a cor é o que os separa à primeira vista — o
    /// esqueleto cinza-pedra da Estátua Desperta não se confunde com o esqueleto
    /// pálido do Carniçal. Branco = a arte como ela é.
    ///
    /// Campo no fim da classe de propósito: os assets guardam a ordem.
    /// </summary>
    public Color portraitTint = Color.white;

    /// <summary>
    /// Quanto o retrato ocupa da moldura, em relação ao tamanho padrão.
    ///
    /// Chefe grande é linguagem de Darkest Dungeon: o tamanho na tela é o aviso
    /// que se lê antes de qualquer número.
    /// </summary>
    public float portraitScale = 1f;
}

public enum EnemyIntent
{
    Attack,
    AttackAll,
    Defend,
    Stress
}
