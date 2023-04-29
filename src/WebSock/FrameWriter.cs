namespace WebSock;

using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public sealed class FrameWriter : Stream
{
    private readonly Stream connection;
    private readonly IMemoryOwner<byte>? key;
    private readonly long payloadLength;
    private long written;
    private bool disposed;

    internal FrameWriter(Stream connection, IMemoryOwner<byte>? key, long payloadLength)
    {
        Debug.Assert(connection.CanWrite, "The stream must be writable.");
        Debug.Assert(key is null || key.Memory.Length >= 4, "The length of key must be exactly 4.");
        Debug.Assert(payloadLength >= 0, "The value must be non-negative.");

        this.connection = connection;
        this.key = key;
        this.payloadLength = payloadLength;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanTimeout => this.connection.CanTimeout;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int WriteTimeout
    {
        get => this.connection.WriteTimeout;
        set => this.connection.WriteTimeout = value;
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        throw new NotSupportedException();
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        Debug.Assert(this.written <= this.payloadLength, "The written must not exceed the payload length.");

        // Check arguments.
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        else if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        else if (offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        // Check states.
        if (this.disposed)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        }

        if (this.written == this.payloadLength)
        {
            throw new IOException("End of stream has been reached.");
        }

        // Mask the data.
        var remaining = (int)Math.Min(this.payloadLength - this.written, int.MaxValue);
        var source = buffer[offset..(offset + Math.Min(count, remaining))];
        byte[] data;

        if (source.Length == 0 || this.key is null)
        {
            data = source;
        }
        else
        {
            data = new byte[source.Length];

            Buffer.BlockCopy(source, 0, data, 0, data.Length);
            WebSock.ApplyMaskingKey(data, this.key.Memory.Span[..4]);
        }

        // Write the underlying stream.
        var result = this.connection.BeginWrite(data, 0, data.Length, callback, state);

        this.written += data.Length;

        return result;
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        throw new NotSupportedException();
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        throw new InvalidOperationException();
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        this.connection.EndWrite(asyncResult);
    }

    public override void Flush()
    {
        this.connection.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return this.connection.FlushAsync(cancellationToken);
    }

    public override int Read(Span<byte> buffer)
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public override int ReadByte()
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        else if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        else if (offset + count > buffer.Length)
        {
            throw new ArgumentException($"The sum of the value and {nameof(count)} is greater than the length of {nameof(buffer)}.", nameof(offset));
        }

        this.Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Debug.Assert(this.written <= this.payloadLength, "The written must not exceed the payload length.");

        // Check states.
        if (this.disposed)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        }

        // Check if the remaining space is enough.
        var remaining = this.payloadLength - this.written;

        if (buffer.Length > remaining)
        {
            throw new NotSupportedException("Not enough space is available.");
        }

        // Check if we need to mask the data.
        if (this.key is { } key)
        {
            using var masked = MemoryPool<byte>.Shared.Rent(buffer.Length);

            buffer.CopyTo(masked.Memory.Span);
            WebSock.ApplyMaskingKey(masked.Memory.Span[..buffer.Length], key.Memory.Span[..4]);

            this.connection.Write(masked.Memory.Span[..buffer.Length]);
        }
        else
        {
            this.connection.Write(buffer);
        }

        this.written += buffer.Length;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        else if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        else if (offset + count > buffer.Length)
        {
            throw new ArgumentException($"The sum of the value and {nameof(count)} is larger than the length of {nameof(buffer)}.", nameof(offset));
        }

        return this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Debug.Assert(this.written <= this.payloadLength, "The written must not exceed the payload length.");

        // Check states.
        if (this.disposed)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        }

        // Check if the remaining space is enough.
        var remaining = this.payloadLength - this.written;

        if (buffer.Length > remaining)
        {
            throw new NotSupportedException("Not enough space is available.");
        }

        // Check if we need to mask the data.
        if (this.key is { } key)
        {
            using var masked = MemoryPool<byte>.Shared.Rent(buffer.Length);

            buffer.CopyTo(masked.Memory);
            WebSock.ApplyMaskingKey(masked.Memory.Span[..buffer.Length], key.Memory.Span[..4]);

            await this.connection.WriteAsync(masked.Memory[..buffer.Length], cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await this.connection.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        this.written += buffer.Length;
    }

    public override void WriteByte(byte value)
    {
        Debug.Assert(this.written <= this.payloadLength, "The written must not exceed the payload length.");

        // Check states.
        if (this.disposed)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        }
        else if (this.written == this.payloadLength)
        {
            throw new NotSupportedException("Not enough space is available.");
        }

        // Check if we need to mask the data.
        if (this.key is { } key)
        {
            this.connection.WriteByte(WebSock.ApplyMaskingKey(value, key.Memory.Span[..4]));
        }
        else
        {
            this.connection.WriteByte(value);
        }

        this.written++;
    }

    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.key?.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }
}
