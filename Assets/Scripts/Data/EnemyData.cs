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

    /// <summary>
    /// Os quadros com que a criatura se mexe no campo de batalha.
    ///
    /// O <see cref="portrait"/> continua sendo o primeiro quadro do Idle e serve
    /// à jornada, onde o inimigo aparece como card pequeno. Aqui é onde o combate
    /// busca o resto: os spritesheets dos pacotes já trazem Idle, Attack, Take
    /// Hit e Death fatiados, e até agora só o primeiro quadro do primeiro deles
    /// era usado.
    ///
    /// Preenchido pela ferramenta <c>Tools → Guild of Legends → Aplicar Arte nos
    /// Inimigos</c>, que é também quem escolhe qual criatura representa quem.
    /// Vazio é estado válido: sem quadros a figura fica parada no retrato, e o
    /// combate roda igual.
    ///
    /// Campo no fim da classe, como manda a regra do projeto.
    /// </summary>
    public EnemyAnimation animation;
}

/// <summary>
/// Um conjunto de animações de criatura, em quadros soltos.
///
/// Quadros, e não <c>AnimationClip</c>: os pacotes vêm como PNG fatiado, sem
/// controlador nem prefab, e montar um Animator por criatura significaria um
/// asset novo por inimigo para trocar quatro sprites. Um array por estado é o
/// que o material já é.
/// </summary>
[System.Serializable]
public class EnemyAnimation
{
    public Sprite[] idle;
    public Sprite[] attack;
    public Sprite[] hit;
    public Sprite[] death;

    /// <summary>Quadros por segundo. 10 é o passo dos pacotes de pixel art.</summary>
    public float frameRate = 10f;

    /// <summary>
    /// A criatura foi desenhada olhando para a direita?
    ///
    /// No campo de batalha os inimigos ficam à direita e encaram a party, que
    /// está à esquerda — quem foi desenhado olhando para a direita precisa ser
    /// espelhado, ou luta de costas para quem veio matá-la.
    /// </summary>
    public bool desenhadaOlhandoParaDireita = true;

    /// <summary>
    /// Onde o desenho começa dentro do quadro, do pé para cima, em fração da
    /// altura do quadro.
    ///
    /// <b>O quadro não é a criatura.</b> Os spritesheets são grades de tamanho
    /// fixo com o bicho solto em algum lugar do meio — o esqueleto ocupa a metade
    /// de cima de um quadro de 150px, com transparência embaixo. Assentar o
    /// quadro no chão deixa a criatura flutuando meia altura acima dele, e é
    /// exatamente assim que ela apareceu na primeira captura do campo de
    /// batalha: de pé no ar, sobre o próprio nome.
    ///
    /// Medido uma vez pela ferramenta de arte, lendo os pixels: em execução não
    /// há como perguntar isso a uma textura que não é legível.
    /// </summary>
    public float baseVisivel;

    /// <summary>
    /// Quanto da altura do quadro o desenho de fato ocupa, de 0 a 1.
    ///
    /// É esta altura, e não a do quadro, que a criatura precisa preencher na
    /// tela — senão duas criaturas do mesmo tamanho aparente saem com tamanhos
    /// diferentes só porque uma foi desenhada com mais folga em volta.
    /// </summary>
    public float alturaVisivel = 1f;

    public bool TemQuadros => idle != null && idle.Length > 0;

    /// <summary>A medição existe? Sem ela, o quadro inteiro é o que há.</summary>
    public bool TemMedida => alturaVisivel > 0.01f && alturaVisivel <= 1f;
}

public enum EnemyIntent
{
    Attack,
    AttackAll,
    Defend,
    Stress
}
