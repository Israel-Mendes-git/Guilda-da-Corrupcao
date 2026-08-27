/// <summary>
/// Para que a carta serve dentro de um baralho.
///
/// Nasceu de um defeito de montagem: o <see cref="DeckGenerator"/> escolhia por
/// raridade e sorteava o resto entre as cartas comuns da classe. Com duas comuns
/// por classe, o baralho saía como oito cópias de duas cartas — e o do Curandeiro
/// saía sem uma única carta que ferisse alguém, porque nenhuma das dele causa
/// dano. Papel é o eixo que faltava: o baralho passa a ser montado por função, e
/// só então por raridade.
///
/// Rejeitado: classificar por <see cref="CardRarity"/> ou por custo. Raridade diz
/// quanto a carta é rara, não o que ela resolve no turno; custo separa cartas que
/// fazem a mesma coisa.
/// </summary>
public enum CardRole
{
    /// <summary>Fere o inimigo — inclui veneno e enfraquecimento, que também cobram vida.</summary>
    Ataque,

    /// <summary>Segura o golpe: bloqueio e esquiva.</summary>
    Defesa,

    /// <summary>Repõe o que já se perdeu: cura e limpeza de aflição.</summary>
    Suporte,

    /// <summary>Não muda o placar sozinha; muda o turno seguinte.</summary>
    Utilidade
}

public static class CardRoleUtil
{
    /// <summary>
    /// O papel vem do efeito de combate, nunca dos números da carta: a Fúria
    /// carrega <c>combatDamage</c> e não fere ninguém, e ler o número a chamaria
    /// de ataque. Foi esse mesmo engano que já fez o simulador desperdiçar turnos.
    ///
    /// Quem fere é <see cref="CombatManager.DealsDamage"/>, e não uma segunda
    /// lista aqui — duas listas divergem no primeiro efeito novo.
    /// </summary>
    public static CardRole Of(CardData card)
    {
        if (card == null) return CardRole.Utilidade;

        if (CombatManager.DealsDamage(card.combatEffect))
            return CardRole.Ataque;

        switch (card.combatEffect)
        {
            case CombatEffectType.Block:
            case CombatEffectType.BlockAll:
            case CombatEffectType.Evade:
                return CardRole.Defesa;

            case CombatEffectType.Heal:
            case CombatEffectType.HealAll:
            case CombatEffectType.Cleanse:
                return CardRole.Suporte;

            default:
                // Compra, energia, ímpeto, mira — e também a carta sem efeito de
                // combate, que na estrada ainda vale alguma coisa.
                return CardRole.Utilidade;
        }
    }

    /// <summary>Nome do papel para a tela de baralho e para os avisos do console.</summary>
    public static string Label(CardRole role)
    {
        switch (role)
        {
            case CardRole.Ataque: return "ataque";
            case CardRole.Defesa: return "defesa";
            case CardRole.Suporte: return "suporte";
            default: return "utilidade";
        }
    }
}
