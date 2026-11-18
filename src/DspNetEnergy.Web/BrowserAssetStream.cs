using Microsoft.JSInterop;

namespace DspNetEnergy.Web;

// AssetsTools needs synchronous seeking. JavaScript supplies a cached local Blob slice on demand.
internal sealed class BrowserAssetStream(IJSInProcessRuntime js, string name, long length) : Stream
{
    private const int BlockSize = 128 * 1024;
    private long position;
    private long blockStart = -1;
    private byte[] block = [];

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position
    {
        get => position;
        set => position = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = checked(offset + (origin switch
        {
            SeekOrigin.Begin => 0,
            SeekOrigin.Current => Position,
            SeekOrigin.End => Length,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        }));
        return Position;
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> destination)
    {
        int total = 0;
        while (total < destination.Length && position < length)
        {
            int offset = EnsureBlock();
            int count = Math.Min(block.Length - offset, destination.Length - total);
            block.AsSpan(offset, count).CopyTo(destination[total..]);
            position += count;
            total += count;
        }
        return total;
    }

    public override int ReadByte()
    {
        if (position >= length) return -1;
        int offset = EnsureBlock();
        position++;
        return block[offset];
    }

    private int EnsureBlock()
    {
        if (position < blockStart || position >= blockStart + block.Length)
        {
            blockStart = position / BlockSize * BlockSize;
            int size = (int)Math.Min(BlockSize, length - blockStart);
            block = js.Invoke<byte[]>("dsp.assetSlice", name, blockStart, size);
            if (block.Length != size) throw new EndOfStreamException();
        }
        return (int)(position - blockStart);
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
