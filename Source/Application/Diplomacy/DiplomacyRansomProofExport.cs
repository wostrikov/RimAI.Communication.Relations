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
using Ustas.RimAI.Core.Storage;

namespace Ustas.RimAI.Communication.Relations.UI;

internal sealed class DiplomacyRansomProofExport : DiplomacyDialogueCollaborator
{
    internal DiplomacyRansomProofExport(Dialog_DiplomacyDialogue owner) : base(owner) { }


internal static bool TryExportRansomProofPortrait(Pawn pawn, out string imagePath)
{
    imagePath = string.Empty;
    if (pawn == null)
    {
        return false;
    }

    Texture portrait;
    try
    {
        portrait = PortraitsCache.Get(
            pawn,
            new Vector2(DiplomacyRansomProofWorkflow.RansomProofPortraitSize, DiplomacyRansomProofWorkflow.RansomProofPortraitSize),
            Rot4.South,
            Vector3.zero,
            1f);
    }
    catch (Exception ex)
    {
        Log.Warning($"[RimAI.Relations] Failed to capture ransom portrait: {ex.Message}");
        return false;
    }

    if (!TryConvertPortraitToPngBytes(portrait, out byte[] pngBytes) || pngBytes == null || pngBytes.Length == 0)
    {
        return false;
    }

    try
    {
        string folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "Ustas.RimAI.Communication.Relations", "Temp", "RansomProof");
        LocalStorage.Current.CreateDirectory(folder);
        int tick = Find.TickManager?.TicksGame ?? 0;
        imagePath = Path.Combine(folder, $"ransom_proof_{pawn.thingIDNumber}_{tick}.png");
        LocalStorage.Current.WriteAllBytes(imagePath, pngBytes);
        return true;
    }
    catch (Exception ex)
    {
        Log.Warning($"[RimAI.Relations] Failed to persist ransom portrait: {ex.Message}");
        imagePath = string.Empty;
        return false;
    }
}



internal static bool TryConvertPortraitToPngBytes(Texture portrait, out byte[] pngBytes)
{
    pngBytes = null;
    if (portrait == null)
    {
        return false;
    }

    if (portrait is Texture2D texture2D)
    {
        try
        {
            pngBytes = texture2D.EncodeToPNG();
            return pngBytes != null && pngBytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    if (!(portrait is RenderTexture renderTexture))
    {
        return false;
    }

    RenderTexture previous = RenderTexture.active;
    Texture2D readable = null;
    try
    {
        RenderTexture.active = renderTexture;
        readable = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        readable.Apply();
        pngBytes = readable.EncodeToPNG();
        return pngBytes != null && pngBytes.Length > 0;
    }
    catch
    {
        return false;
    }
    finally
    {
        RenderTexture.active = previous;
        if (readable != null)
        {
            UnityEngine.Object.Destroy(readable);
        }
    }
}
}
