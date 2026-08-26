#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Confere e ajusta os ícones das relíquias e poções.
///
/// Tools → Guild of Legends → Conferir Ícones de Item
///
/// <b>Um arquivo por item, e não uma folha fatiada.</b> O <i>Raven Fantasy
/// Icons</i> traz 6.580 ícones numa grade de 2.192 quadros — o projeto precisa
/// de dez. Importar a folha inteira significaria 2.192 sprites no AssetDatabase
/// e uma tabela de índices que ninguém consegue reler depois; recortar os dez em
/// <c>Resources/ItemIcons</c> deixa cada ícone com o nome do item que ele
/// representa, e o carregamento vira <c>Resources.Load</c> pelo id.
///
/// Esta ferramenta não cria nada: ela corrige o import (pixel art precisa de
/// Point e nada de compressão) e <b>reclama do que falta</b>. Item sem ícone
/// aparece como texto puro na ficha, que é o estado anterior — funciona, mas o
/// silêncio esconderia um id escrito errado.
/// </summary>
public static class ItemArt
{
    public const string PastaEmResources = "ItemIcons";
    const string Pasta = "Assets/Resources/" + PastaEmResources;

    [MenuItem("Tools/Guild of Legends/Conferir Ícones de Item")]
    public static void Conferir()
    {
        var faltando = new List<string>();
        var ajustados = new List<string>();

        foreach (var relic in ItemCatalog.Reliquias) Verificar(relic.id, relic.nome, faltando, ajustados);
        foreach (var potion in ItemCatalog.Pocoes) Verificar(potion.id, potion.nome, faltando, ajustados);

        if (ajustados.Count > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"ItemArt: {ajustados.Count} ícone(s) reimportado(s) como sprite de pixel art —\n  "
                    + string.Join("\n  ", ajustados));
        }
        else
        {
            Debug.Log($"ItemArt: os {ItemCatalog.Reliquias.Count + ItemCatalog.Pocoes.Count} ícones estão no lugar.");
        }

        if (faltando.Count > 0)
            Debug.LogWarning($"ItemArt: sem ícone em {Pasta} — {string.Join(", ", faltando)}. "
                           + "Esses itens aparecem só como texto.");
    }

    static void Verificar(string id, string nome, List<string> faltando, List<string> ajustados)
    {
        string caminho = $"{Pasta}/{id}.png";
        var importer = AssetImporter.GetAtPath(caminho) as TextureImporter;

        if (importer == null)
        {
            faltando.Add($"{nome} ({id})");
            return;
        }

        bool mexeu = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            mexeu = true;
        }

        // Pixel art de 32px: interpolar borra o traço, e comprimir num ícone
        // deste tamanho não economiza nada e suja as bordas.
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            mexeu = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            mexeu = true;
        }

        if (importer.spritePixelsPerUnit != 32f)
        {
            importer.spritePixelsPerUnit = 32f;
            mexeu = true;
        }

        if (!mexeu) return;

        importer.SaveAndReimport();
        ajustados.Add($"{nome} → {id}.png");
    }
}
#endif
