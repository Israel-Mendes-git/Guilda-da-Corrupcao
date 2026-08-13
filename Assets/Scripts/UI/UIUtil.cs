using UnityEngine;

/// <summary>
/// Utilidades de UI compartilhadas.
/// </summary>
public static class UIUtil
{
    /// <summary>
    /// Esvazia um container agora, e não no fim do frame.
    ///
    /// `Destroy` apenas agenda a remoção: o objeto continua sendo filho e sendo
    /// desenhado até o frame terminar. Todo código que faz "limpa e reconstrói"
    /// duas vezes no mesmo frame — o que acontece o tempo todo quando uma ação
    /// dispara outra em cadeia — acabava com as duas gerações de elementos na
    /// tela ao mesmo tempo: cartas sobre cartas, botões sobre botões.
    ///
    /// Desanexar antes de destruir tira o objeto da hierarquia imediatamente.
    /// </summary>
    public static void ClearChildrenNow(Transform container)
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (child == null) continue;

            child.SetParent(null, false);
            Object.Destroy(child.gameObject);
        }
    }
}
