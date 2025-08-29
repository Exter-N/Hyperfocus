using System;
using System.IO;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;

namespace Hyperfocus;

public class CursorProvider
{
    private const string UldPath = "ui/uld/TargetCursor.uld";
    private const string TexPath = "ui/uld/TargetCursor.tex";
    private const string TexHrPath = "ui/uld/TargetCursor_hr1.tex";

    public const int TargetEdgePart = 0;
    public const int TargetFillPart = 1;
    public const int FocusEdgePart = 2;
    public const int FocusFillPart = 3;

    private readonly ITextureProvider textureProvider;
    private readonly int pixelRatio;
    private readonly PartEntry[] parts;

    public CursorProvider(
        ITextureProvider textureProvider, IUiBuilder uiBuilder, ITextureSubstitutionProvider substitution,
        IDataManager data)
    {
        this.textureProvider = textureProvider;

        var uld = uiBuilder.LoadUld(UldPath);
        if (!uld.Valid)
            throw new Exception($"Failed to load {UldPath}");

        var id = GetTextureId(uld.Uld!, TexPath);
        if (id == uint.MaxValue)
            throw new Exception($"Could not find {TexPath} in ULD");

        pixelRatio = 2;
        var tex = GetTexture(substitution.GetSubstitutedPath(TexHrPath), data);
        if (tex == null)
        {
            pixelRatio = 1;
            tex = GetTexture(substitution.GetSubstitutedPath(TexPath), data);

            // Neither texture could be loaded.
            if (tex == null)
                throw new Exception($"Failed to load {TexPath}");
        }

        var texData = ImageData.FromTexFile(tex);
        parts = uld.Uld!.Parts
                   .SelectMany(p => p.Parts)
                   .Where(p => p.TextureId == id)
                   .Skip(2)
                   .Take(4)
                   .Select(p =>
                   {
                       var u = p.U * pixelRatio;
                       var v = p.V * pixelRatio;
                       var w = Math.Max(0, Math.Min(p.W * pixelRatio, texData.Width - u));
                       var h = Math.Max(0, Math.Min(p.H * pixelRatio, texData.Height - v));

                       return new PartEntry(texData.Slice(u, v, w, h));
                   })
                   .ToArray();
    }

    public IDalamudTextureWrap GetTintedCursorPart(int index, uint tint)
    {
        if (index < 0 || index >= parts.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        tint &= 0xFFFFFFu;

        var part = parts[index];
        if (part.CurrentTint == tint && part.CurrentTexture is not null)
            return part.CurrentTexture;

        var tinted = part.ImageData.Tint(tint, index switch
        {
            TargetEdgePart or FocusEdgePart => 0u,
            TargetFillPart or FocusFillPart => 0xA0A0A0u,
            _ => throw new Exception("Undefined part color bias"),
        });

        part.CurrentTexture?.Dispose();
        part.CurrentTexture = textureProvider.CreateFromRaw(tinted.ImageSpecification, tinted.Data,
                                                            $"{nameof(CursorProvider)} part {index}, tinted 0x{tint:X6}");
        part.CurrentTint = tint;

        return part.CurrentTexture;
    }

    private static uint GetTextureId(UldFile uld, ReadOnlySpan<char> texPath)
    {
        var id = uint.MaxValue;
        foreach (var part in uld.AssetData)
        {
            var maxLength = Math.Min(part.Path.Length, texPath.Length);
            if (part.Path.AsSpan()[..maxLength].SequenceEqual(texPath[..maxLength]))
            {
                id = part.Id;
                break;
            }
        }

        return id;
    }

    private static TexFile? GetTexture(string path, IDataManager data)
        => Path.IsPathRooted(path)
               ? data.GameData.GetFileFromDisk<TexFile>(path)
               : data.GetFile<TexFile>(path);

    private sealed class PartEntry(ImageData imageData)
    {
        public readonly ImageData ImageData = imageData;
        public uint CurrentTint;
        public IDalamudTextureWrap? CurrentTexture;
    }
}
