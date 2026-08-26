using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A estrada dentro da tela da jornada: a janela por onde se vê o grupo andando.
///
/// Só cola o palco (<see cref="TrailStage"/>) na interface e traduz o estado da
/// jornada em "andando" ou "parado". O palco não sabe o que é uma jornada, e o
/// <see cref="JourneyManager"/> não sabe o que é uma RenderTexture.
///
/// <b>Onde a faixa fica na pilha:</b> logo acima do fundo do bioma e abaixo de
/// todo o resto. A estrada é cenário — o texto do evento, as escolhas e a mão
/// continuam legíveis por cima dela, e é o cenário que se apaga sob a caixa de
/// texto, nunca o contrário.
/// </summary>
public class TrailRoadUI : MonoBehaviour
{
    /// <summary>A janela. Recebe a filmagem do palco.</summary>
    public RawImage janela;

    TrailStage palco;

    /// <summary>Para o teste: o palco existe e tem quantos corpos.</summary>
    public int CorposEmCena => palco != null ? palco.Corpos : 0;

    /// <summary>Para o teste: o tamanho do grupo em pixels da faixa.</summary>
    public float AlturaDoGrupoEmPixels => palco != null ? palco.AlturaEmPixels : 0f;

    /// <summary>
    /// Põe o grupo na estrada, na ordem em que ele marcha.
    ///
    /// Chamado quando a jornada começa. Pode rodar com este objeto desativado —
    /// o painel da jornada nasce fechado, e esperar o <c>OnEnable</c> deixaria a
    /// estrada vazia no primeiro frame em que ela aparece.
    /// </summary>
    public void Preparar(IList<HeroData> ordemDaMarcha)
    {
        if (palco == null)
        {
            // A filmagem nasce do tamanho exato da janela. Textura menor que a
            // faixa seria ampliada por interpolação e borraria o pixel art;
            // maior, seria trabalho jogado fora todo frame.
            var rt = janela != null ? janela.rectTransform : (RectTransform)transform;
            int largura = Mathf.RoundToInt(rt.rect.width);
            int altura = Mathf.RoundToInt(rt.rect.height);

            palco = largura > 0 && altura > 0
                ? TrailStage.Criar(largura, altura)
                : TrailStage.Criar();
        }

        palco.Elenco(ordemDaMarcha);

        if (janela != null)
        {
            janela.texture = palco.Textura;
            janela.color = Color.white;
            janela.raycastTarget = false;   // cenário não recebe clique
            janela.enabled = true;
        }
    }

    /// <summary>O grupo está em movimento?</summary>
    public void Andar(bool andando)
    {
        if (palco != null) palco.Andando = andando;
    }

    /// <summary>Vida e estresse mudaram — repinta as barras sobre as cabeças.</summary>
    public void AtualizarEstado()
    {
        if (palco != null) palco.AtualizarBarras();
    }

    /// <summary>
    /// Fim da jornada: desmonta o palco.
    ///
    /// Sem isto, cada jornada deixa uma câmera e uma RenderTexture vivas na
    /// cena. Uma partida longa acumularia uma por expedição, e nenhuma delas
    /// apareceria em tela para denunciar o vazamento.
    /// </summary>
    public void Desmontar()
    {
        if (janela != null)
        {
            janela.texture = null;
            janela.enabled = false;
        }

        if (palco != null)
        {
            Destroy(palco.gameObject);
            palco = null;
        }
    }

    void OnDestroy()
    {
        if (palco != null) Destroy(palco.gameObject);
    }
}
