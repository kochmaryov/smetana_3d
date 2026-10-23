namespace SmetaNA.Formats.Tests;

/// <summary>Временный каталог тестов: удаляется рекурсивно при Dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "smetana-pf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string FilePath(string name) => Path.Combine(Root, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// Поток-«обрыв записи»: пропускает заданное число байт, затем бросает IOException —
/// имитация сбоя (обрыв питания, переполнение диска) в середине атомарной записи.
/// </summary>
internal sealed class ThrowingStream : Stream
{
    private readonly Stream _inner;
    private readonly long _failAfterBytes;
    private long _written;

    public ThrowingStream(Stream inner, long failAfterBytes)
    {
        _inner = inner;
        _failAfterBytes = failAfterBytes;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_written + count > _failAfterBytes)
        {
            int allowed = (int)Math.Max(0, _failAfterBytes - _written);
            _inner.Write(buffer, offset, allowed);
            _written = _failAfterBytes;
            throw new IOException("Имитация сбоя записи (обрыв питания).");
        }

        _inner.Write(buffer, offset, count);
        _written += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (_written + buffer.Length > _failAfterBytes)
        {
            int allowed = (int)Math.Max(0, _failAfterBytes - _written);
            _inner.Write(buffer[..allowed]);
            _written = _failAfterBytes;
            throw new IOException("Имитация сбоя записи (обрыв питания).");
        }

        _inner.Write(buffer);
        _written += buffer.Length;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
