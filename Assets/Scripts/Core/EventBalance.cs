#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Faz a carta da estrada valer o que ela custa.
///
/// Tools → Guild of Legends → Ajustar Eventos (carta ⨯ escolha)
///
/// <b>O problema.</b> Todo evento tem uma opção que exige carta, e o jogador não
/// tinha por que tomá-la: as opções livres resolviam igual, e a carta gasta
/// aqui falta na luta seguinte — o baralho é o mesmo. Pior: os 25 desfechos
/// reforçados estavam vazios, e o código trocava o desfecho bom por eles.
///
/// <b>As duas regras, decididas pelo autor em 21/08:</b>
///
/// 1. <b>A carta dá o melhor desfecho.</b> O reforçado é a mesma escolha, só que
///    dando certo: o dano não acontece, a cura rende mais, o ouro vem maior, o
///    ferimento não pega. Não é um desfecho inventado — é o que a opção já
///    prometia, sem o que podia dar errado.
///
/// 2. <b>A opção com carta é a única saída sem custo.</b> Das 64 opções livres,
///    56 já cobravam alguma coisa; as 8 restantes passam a cobrar moral. Sem
///    isso, "não gastar carta" continuaria sendo de graça.
///
/// <b>Escolha do autor no Inspector tem precedência:</b> reforço já escrito à
/// mão não é tocado, a menos que se peça para sobrescrever. É a armadilha do
/// Card Creator, que regravava valores antigos sem avisar.
/// </summary>
public static class EventBalance
{
    /// <summary>Moral que uma opção livre sem custo passa a cobrar do grupo.</summary>
    const int CustoDeMoral = -6;

    /// <summary>Ganho mínimo do reforçado, quando a opção não prometia nada.</summary>
    const int GanhoMinimoDeMoral = 8;

    [MenuItem("Tools/Guild of Legends/Ajustar Eventos (carta ⨯ escolha)")]
    public static void Ajustar() => Ajustar(false);

    /// <param name="sobrescrever">true para regravar reforço escrito à mão.</param>
    public static void Ajustar(bool sobrescrever)
    {
        EventData[] eventos = Resources.LoadAll<EventData>("Events");
        if (eventos.Length == 0)
        {
            Debug.LogError("EventBalance: nenhum evento em Resources/Events.");
            return;
        }

        int reforcados = 0, cobrados = 0;
        var relatorio = new List<string>();

        foreach (EventData evento in eventos)
        {
            if (evento.outcomes == null) continue;

            bool mexeu = false;

            foreach (EventOutcome opcao in evento.outcomes)
            {
                if (opcao == null) continue;

                if (opcao.RequiresCard)
                {
                    if (opcao.empoweredConsequences != null
                        && !Vazio(opcao.empoweredConsequences) && !sobrescrever) continue;

                    opcao.empoweredConsequences = Melhorar(opcao.consequences);

                    if (string.IsNullOrEmpty(opcao.empoweredText))
                        opcao.empoweredText = "A carta abre o caminho — e o grupo sai inteiro.";

                    reforcados++;
                    mexeu = true;
                    relatorio.Add($"  {evento.name}: reforço escrito para \"{Curto(opcao.optionText)}\"");
                    continue;
                }

                // Opção livre: se não cobra nada, passa a cobrar.
                if (TemCusto(opcao)) continue;

                opcao.consequences = ComCustoDeMoral(opcao.consequences);
                cobrados++;
                mexeu = true;
                relatorio.Add($"  {evento.name}: custo de moral em \"{Curto(opcao.optionText)}\"");
            }

            if (mexeu) EditorUtility.SetDirty(evento);
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"EventBalance: {reforcados} desfecho(s) reforçado(s) e {cobrados} opção(ões) livre(s) "
                + $"com custo novo, em {eventos.Length} eventos.\n" + string.Join("\n", relatorio));
    }

    /// <summary>
    /// A mesma escolha, dando certo.
    ///
    /// Zera o que podia dar errado e aumenta o que já era ganho. Nada aqui
    /// inventa consequência nova: o desfecho reforçado precisa ser reconhecível
    /// como <i>aquela</i> opção, ou o jogador não entende o que a carta comprou.
    /// </summary>
    static EventConsequences Melhorar(EventConsequences original)
    {
        var novo = new EventConsequences
        {
            goldChange = original == null ? 0
                       : original.goldChange > 0 ? Mathf.RoundToInt(original.goldChange * 1.4f)
                       : 0,

            reputationChange = original == null ? 0
                             : original.reputationChange > 0 ? Mathf.RoundToInt(original.reputationChange * 1.5f)
                             : 0,
        };

        var herois = new List<HeroEffect>();
        if (original?.heroEffects != null)
        {
            foreach (var e in original.heroEffects)
            {
                if (e == null) continue;

                herois.Add(new HeroEffect
                {
                    heroName = e.heroName,

                    // Dano vira nada; cura rende mais.
                    hpChange = e.hpChange > 0 ? Mathf.RoundToInt(e.hpChange * 1.5f) : 0,

                    // O que a carta compra é justamente não voltar quebrado.
                    addInjury = false,
                    addTrait = false,
                });
            }
        }

        var morais = new List<MoraleChange>();
        if (original?.moraleChanges != null)
        {
            foreach (var m in original.moraleChanges)
            {
                if (m == null) continue;

                morais.Add(new MoraleChange
                {
                    heroName = m.heroName,
                    moraleChange = m.moraleChange > 0 ? Mathf.RoundToInt(m.moraleChange * 1.5f) : 0,
                });
            }
        }

        novo.heroEffects = herois.ToArray();
        novo.moraleChanges = morais.ToArray();

        // Opção que não prometia nada de bom sairia com reforço vazio — e a
        // carta voltaria a não pagar, que é o defeito que isto veio consertar.
        if (Vazio(novo))
        {
            novo.moraleChanges = new[]
            {
                new MoraleChange { heroName = "All", moraleChange = GanhoMinimoDeMoral }
            };
        }

        return novo;
    }

    /// <summary>Esta opção cobra alguma coisa de quem a escolher?</summary>
    static bool TemCusto(EventOutcome opcao)
    {
        if (opcao == null) return true;
        if (opcao.extraDays > 0 || opcao.triggersCorruption) return true;

        EventConsequences c = opcao.consequences;
        if (c == null) return false;

        if (c.goldChange < 0 || c.reputationChange < 0) return true;

        if (c.heroEffects != null)
            foreach (var e in c.heroEffects)
                if (e != null && (e.hpChange < 0 || e.addInjury || e.addTrait)) return true;

        if (c.moraleChanges != null)
            foreach (var m in c.moraleChanges)
                if (m != null && m.moraleChange < 0) return true;

        return false;
    }

    /// <summary>
    /// Acrescenta o custo à opção livre, preservando o que ela já fazia.
    ///
    /// Moral, e não vida: 8 opções ganhando dano mexeriam na letalidade, que é
    /// medida em mortes por jornada e está calibrada. Moral é o recurso que
    /// pressiona sem matar.
    /// </summary>
    static EventConsequences ComCustoDeMoral(EventConsequences original)
    {
        var novo = original ?? new EventConsequences();
        var morais = new List<MoraleChange>();

        if (novo.moraleChanges != null) morais.AddRange(novo.moraleChanges);
        morais.Add(new MoraleChange { heroName = "All", moraleChange = CustoDeMoral });

        novo.moraleChanges = morais.ToArray();
        return novo;
    }

    static bool Vazio(EventConsequences c)
    {
        if (c == null) return true;
        if (c.goldChange != 0 || c.reputationChange != 0) return false;

        if (c.heroEffects != null)
            foreach (var e in c.heroEffects)
                if (e != null && (e.hpChange != 0 || e.addInjury || e.addTrait)) return false;

        if (c.moraleChanges != null)
            foreach (var m in c.moraleChanges)
                if (m != null && m.moraleChange != 0) return false;

        return true;
    }

    static string Curto(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "(sem texto)";
        return texto.Length <= 34 ? texto : texto.Substring(0, 32) + "…";
    }
}
#endif
