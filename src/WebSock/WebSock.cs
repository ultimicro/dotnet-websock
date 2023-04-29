namespace WebSock;

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;

/// <summary>
/// Represents a connected WebSocket.
/// </summary>
public abstract class WebSock : IAsyncDisposable, IDisposable
{
    private readonly Stream connection;
    private bool disposed;

    protected WebSock(Stream connection)
    {
        if (!connection.CanRead)
        {
            throw new ArgumentException("The stream is not readable.", nameof(connection));
        }

        if (!connection.CanWrite)
        {
            throw new ArgumentException("The stream is not writable.", nameof(connection));
        }

        this.connection = connection;
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsyncCore().ConfigureAwait(false);
        this.Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Send a frame header and return a <see cref="FrameWriter"/> to write the payload.
    /// </summary>
    /// <param name="header">
    /// The header to send.
    /// </param>
    /// <param name="payloadLength">
    /// Length of payload, in bytes.
    /// </param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous header send operation.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="payloadLength"/> is negative.
    /// </exception>
    public async Task<FrameWriter> SendHeaderAsync(FrameHeader header, long payloadLength, CancellationToken cancellationToken = default)
    {
        if (payloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadLength));
        }

        // Send frame header.
        var key = this.GenerateMaskingKey();

        try
        {
            using var data = MemoryPool<byte>.Shared.Rent(14);
            var length = SerializeFrameHeader(header, payloadLength, key is null ? Array.Empty<byte>() : key.Memory.Span[..4], data.Memory.Span);

            await this.connection.WriteAsync(data.Memory[..length], cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            key?.Dispose();
            throw;
        }

        // Construct the writer.
        try
        {
            return new(this.connection, key, payloadLength);
        }
        catch
        {
            key?.Dispose();
            throw;
        }
    }

    internal static void ApplyMaskingKey(Span<byte> data, ReadOnlySpan<byte> key)
    {
        Debug.Assert(key.Length == 4, "Length of the value must be exactly 4 bytes.");

        // Octet i of the transformed data ("transformed-octet-i") is the XOR of octet i of the original data ("original-octet-i") with octet at index i modulo
        // 4 of the masking key ("masking-key-octet-j")
        for (var i = 0; i < data.Length; i++)
        {
            data[i] ^= key[i % 4];
        }
    }

    internal static byte ApplyMaskingKey(byte data, ReadOnlySpan<byte> key)
    {
        Debug.Assert(key.Length == 4, "Length of the value must be exactly 4 bytes.");

        return (byte)(data ^ key[0]);
    }

    protected abstract IMemoryOwner<byte>? GenerateMaskingKey();

    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            this.connection.Dispose();
        }

        this.disposed = true;
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (this.disposed)
        {
            return;
        }

        await this.connection.DisposeAsync().ConfigureAwait(false);
    }

    private static int SerializeFrameHeader(FrameHeader header, long payloadLength, ReadOnlySpan<byte> key, Span<byte> output)
    {
        var i = 0;

        Debug.Assert(payloadLength >= 0, "The value should be non-negative.");
        Debug.Assert(key.Length == 0 || key.Length == 4, "The length of key should be 0 or 4.");
        Debug.Assert(output.Length >= 14, "The length of value should be 14 or greater.");

        output[i++] = header.Value;

        // The length of the "Payload data", in bytes: if 0-125, that is the payload length. If 126, the following 2 bytes interpreted as a 16-bit unsigned
        // integer are the payload length. If 127, the following 8 bytes interpreted as a 64-bit unsigned integer (the most significant bit MUST be 0) are the
        // payload length. Multibyte length quantities are expressed in network byte order. Note that in all cases, the minimal number of bytes MUST be used to
        // encode the length, for example, the length of a 124-byte-long string can't be encoded as the sequence 126, 0, 124.
        byte len;

        if (payloadLength <= 125)
        {
            len = (byte)payloadLength;
            i++;
        }
        else if (payloadLength <= 0xffff)
        {
            len = 126;
            BinaryPrimitives.WriteUInt16BigEndian(output[++i..], (ushort)payloadLength);
            i += 2;
        }
        else
        {
            len = 127;
            BinaryPrimitives.WriteInt64BigEndian(output[++i..], payloadLength);
            i += 8;
        }

        // All frames sent from the client to the server are masked by a 32-bit value that is contained within the frame. This field is present if the mask bit
        // is set to 1 and is absent if the mask bit is set to 0.
        byte mask;

        if (key.Length != 0)
        {
            mask = 0x01;
            key.CopyTo(output[i..(i + 4)]);
            i += 4;
        }
        else
        {
            mask = 0x00;
        }

        output[1] = (byte)((len << 1) | mask);

        return i;
    }
}
