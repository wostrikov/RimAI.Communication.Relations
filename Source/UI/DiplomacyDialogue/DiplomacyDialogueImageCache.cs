using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using Ustas.RimAI.Communication.Relations.AI;
using Ustas.RimAI.Communication.Relations.Config;
using Ustas.RimAI.Communication.Relations.Context;
using Ustas.RimAI.Communication.Relations.Dialogue;
using Ustas.RimAI.Communication.Relations.DiplomacySystem;
using Ustas.RimAI.Communication.Relations.Guards;
using Ustas.RimAI.Communication.Relations.Memory;
using Ustas.RimAI.Communication.Relations.Module;
using Ustas.RimAI.Communication.Relations.PawnRpgPush;
using Ustas.RimAI.Communication.Relations.Persistence;
using Ustas.RimAI.Communication.Relations.Prompting;
using Ustas.RimAI.Communication.Relations.UI;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class DiplomacyDialogueImageCache : DiplomacyDialogueCollaborator
{
    internal DiplomacyDialogueImageCache(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal const int InlineImageCacheSoftLimit = 48;


internal static readonly Dictionary<string, Texture2D> InlineImageTextureCache =
    new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);


internal static readonly LinkedList<string> InlineImageTextureCacheOrder =
    new LinkedList<string>();



internal static bool TryGetInlineImageTexture(string path, out Texture2D texture)
{
    texture = null;
    if (string.IsNullOrWhiteSpace(path))
    {
        return false;
    }

    if (InlineImageTextureCache.TryGetValue(path, out Texture2D cached) && cached != null)
    {
        TouchInlineImageCacheKey(path);
        texture = cached;
        return true;
    }

    if (!File.Exists(path))
    {
        return false;
    }

    try
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes == null || bytes.Length == 0)
        {
            return false;
        }

        Texture2D loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(loaded, bytes))
        {
            UnityEngine.Object.Destroy(loaded);
            return false;
        }

        loaded.wrapMode = TextureWrapMode.Clamp;
        loaded.filterMode = FilterMode.Bilinear;
        InlineImageTextureCache[path] = loaded;
        TouchInlineImageCacheKey(path);
        TrimInlineImageTextureCache();
        texture = loaded;
        return true;
    }
    catch
    {
        return false;
    }
}



internal static void TouchInlineImageCacheKey(string path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return;
    }

    LinkedListNode<string> node = InlineImageTextureCacheOrder.First;
    while (node != null)
    {
        LinkedListNode<string> next = node.Next;
        if (string.Equals(node.Value, path, StringComparison.OrdinalIgnoreCase))
        {
            InlineImageTextureCacheOrder.Remove(node);
            break;
        }

        node = next;
    }

    InlineImageTextureCacheOrder.AddLast(path);
}



internal static void TrimInlineImageTextureCache()
{
    while (InlineImageTextureCache.Count > InlineImageCacheSoftLimit && InlineImageTextureCacheOrder.First != null)
    {
        string evictPath = InlineImageTextureCacheOrder.First.Value;
        InlineImageTextureCacheOrder.RemoveFirst();
        if (!InlineImageTextureCache.TryGetValue(evictPath, out Texture2D evicted))
        {
            continue;
        }

        InlineImageTextureCache.Remove(evictPath);
        if (evicted != null)
        {
            UnityEngine.Object.Destroy(evicted);
        }
    }
}



internal static void ClearInlineImageTextureCache()
{
    foreach (KeyValuePair<string, Texture2D> pair in InlineImageTextureCache)
    {
        if (pair.Value != null)
        {
            UnityEngine.Object.Destroy(pair.Value);
        }
    }

    InlineImageTextureCache.Clear();
    InlineImageTextureCacheOrder.Clear();
}
}
