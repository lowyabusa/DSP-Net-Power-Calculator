using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace DspNetEnergy.Desktop;

internal readonly record struct GameTexture(string Name, bool Shared);

internal static class DspGameIcons
{
    internal static Dictionary<GameTexture, BitmapSource> Load(string gameFolder, IReadOnlySet<GameTexture> wanted)
    {
        string data = Path.Combine(gameFolder, "DSPGAME_Data");
        var textures = new Dictionary<GameTexture, BitmapSource>();
        ReadFile(Path.Combine(data, "resources.assets"), shared: false, wanted, textures);
        ReadFile(Path.Combine(data, "sharedassets0.assets"), shared: true, wanted, textures);
        return textures;
    }

    private static void ReadFile(string path, bool shared, IReadOnlySet<GameTexture> wanted,
        Dictionary<GameTexture, BitmapSource> textures)
    {
        try
        {
            using var reader = new AssetsFileReader(path);
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
                if (ReadIcon(reader, end) is BitmapSource icon)
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

    private static BitmapSource? ReadIcon(AssetsFileReader reader, long end)
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

        byte[] bgra = new byte[byteCount];
        for (int y = 0; y < height; y++)
        {
            int source = (height - y - 1) * stride;
            int target = y * stride;
            for (int x = 0; x < stride; x += 4)
            {
                bgra[target + x] = rgba[source + x + 2];
                bgra[target + x + 1] = rgba[source + x + 1];
                bgra[target + x + 2] = rgba[source + x];
                bgra[target + x + 3] = rgba[source + x + 3];
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgra, stride);
        bitmap.Freeze();
        return bitmap;
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
