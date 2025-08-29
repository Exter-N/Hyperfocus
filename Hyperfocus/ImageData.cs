using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Dalamud.Interface.Textures;
using Dalamud.Utility;
using Lumina.Data.Files;

namespace Hyperfocus;

public readonly record struct ImageData(int Width, int Height, byte[] Data)
{
    public static readonly ImageData Empty = new(0, 0, []);

    public RawImageSpecification ImageSpecification
        => RawImageSpecification.Rgba32(Width, Height);

    public ImageData Slice(int u, int v, int w, int h)
    {
        if (u < 0 || u > Width)
            throw new ArgumentOutOfRangeException(nameof(u));
        if (v < 0 || v > Height)
            throw new ArgumentOutOfRangeException(nameof(v));
        if (w < 0 || u + w > Width)
            throw new ArgumentOutOfRangeException(nameof(w));
        if (h < 0 || v + h > Height)
            throw new ArgumentOutOfRangeException(nameof(h));

        if (w == 0 || h == 0)
            return Empty;

        // Make sure to round up to the nearest multiple of 4 pixels in the buffer, for V128 processing.
        var partData = new byte[(((w * h) + 3) & ~3) * 4];

        // Iterate over all lines and copy the relevant ones,
        // assuming a 4-byte-per-pixel standard layout.
        for (var y = 0; y < h; ++y)
        {
            var inputSlice = Data.AsSpan((((v + y) * Width) + u) * 4, w * 4);
            var outputSlice = partData.AsSpan(y * w * 4);
            inputSlice.CopyTo(outputSlice);
        }

        return new(w, h, partData);
    }

    /// <summary>Bytewise saturated fused <c>this + tint - bias</c>.</summary>
    public ImageData Tint(uint tint, uint bias)
    {
        var output = new byte[Data.Length];

        var inputSpan = MemoryMarshal.Cast<byte, Vector128<byte>>((ReadOnlySpan<byte>)Data);
        var outputSpan = MemoryMarshal.Cast<byte, Vector128<byte>>((Span<byte>)output);

        var biasVec = Vector128.Create(bias).AsByte();
        var addendVec = Vector128.Create(tint).AsByte();
        var subtrahendVec = Vector128.Max(~addendVec, ~biasVec) - ~biasVec;
        addendVec = Vector128.Max(addendVec, biasVec) - biasVec;

        for (var i = 0; i < outputSpan.Length; ++i)
            outputSpan[i] = Vector128.Max(addendVec + Vector128.Min(inputSpan[i], ~addendVec), subtrahendVec) -
                            subtrahendVec;

        return this with { Data = output };
    }

    public static ImageData FromTexFile(TexFile tex)
        => new(tex.Header.Width, tex.Header.Height, tex.GetRgbaImageData());
}
