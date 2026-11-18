using System.IO;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace DspNetEnergy.Web;

internal readonly record struct GameTexture(string Name, bool Shared);

internal static class DspGameIcons
{
    internal static Dictionary<GameTexture, byte[]> Load(Stream? resources, Stream? shared, IReadOnlySet<GameTexture> wanted)
    {
        var textures = new Dictionary<GameTexture, byte[]>();
        if (resources is not null) ReadFile(resources, shared: false, wanted, textures);
        if (shared is not null) ReadFile(shared, shared: true, wanted, textures);
        return textures;
    }

    private static void ReadFile(Stream stream, bool shared, IReadOnlySet<GameTexture> wanted,
        Dictionary<GameTexture, byte[]> textures)
    {
        try
        {
            using var reader = new AssetsFileReader(stream);
            if (!AssetsFile.IsAssetsFile(reader, 0, reader.BaseStream.Length))
            {
                return;
            }

            reader.Position = 0;
            var file = new AssetsFile();
            file.Read(reader);
            // These player builds omit type trees. Use the verified Unity 2022.3.62 Texture2D layout.
            if (reader.BigEndian || !file.Metadata.UnityVersion.StartsWith("2022.3.62f", StringComparison.Ordinal))
            {
                return;
            }

            foreach (AssetFileInfo asset in file.GetAssetsOfType(AssetClassID.Texture2D))
            {
                long start = asset.GetAbsoluteByteOffset(file);
                long end = start + asset.ByteSize;
                if (start < 0 || end > reader.BaseStream.Length || asset.ByteSize < 4)
                {
                    continue;
                }

                reader.Position = start;
                int nameLength = reader.ReadInt32();
                if (nameLength is < 1 or > 128 || end - reader.Position < nameLength)
                {
                    continue;
                }

                var key = new GameTexture(Encoding.UTF8.GetString(reader.ReadBytes(nameLength)), shared);
                if (textures.ContainsKey(key) || (!wanted.Contains(key) && !wanted.Contains(key with { Shared = !shared })))
                {
                    continue;
                }

                reader.Align();
                if (ReadIcon(reader, end) is byte[] icon)
                {
                    textures.Add(key, icon);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidDataException or InvalidOperationException or IndexOutOfRangeException or OverflowException)
        {
            // A missing or unsupported resource file leaves its entries in text mode.
        }
    }

    private static byte[]? ReadIcon(AssetsFileReader reader, long end)
    {
        if (end - reader.Position < 96)
        {
            return null;
        }

        reader.Position += 8; // Fallback format and aligned flags
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        reader.Position += 8; // Complete image size and stripped mips
        const int rgba32 = 4;
        const int iconSide = 80;
        if (width != iconSide || height != iconSide || reader.ReadInt32() != rgba32)
        {
            return null;
        }

        reader.Position += 8; // Mip count and aligned readability flags
        if (!SkipAlignedBytes(reader, end)) // Mipmap limit group name
        {
            return null;
        }

        reader.Position += 48; // Streaming flags, dimensions, sampler settings, lightmap and color space
        if (!SkipAlignedBytes(reader, end) || end - reader.Position < 4) // Platform data
        {
            return null;
        }

        int imageLength = reader.ReadInt32();
        int stride = width * 4;
        int byteCount = stride * height;
        if (imageLength < byteCount || imageLength > end - reader.Position)
        {
            return null;
        }

        byte[] rgba = reader.ReadBytes(byteCount);
        if (rgba.Length != byteCount)
        {
            return null;
        }

        return IconPng.Encode(rgba, width, height);
    }

    private static bool SkipAlignedBytes(AssetsFileReader reader, long end)
    {
        if (end - reader.Position < 4)
        {
            return false;
        }

        int count = reader.ReadInt32();
        if (count < 0 || end - reader.Position < count)
        {
            return false;
        }

        reader.Position += count;
        reader.Align();
        return reader.Position <= end;
    }
}
