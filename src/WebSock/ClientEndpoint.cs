namespace WebSock;

using System.Buffers;

/// <summary>
/// A WebSocket connection that is connected to the client.
/// </summary>
public sealed class ClientEndpoint : WebSock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientEndpoint"/> class from a completed handshake connection.
    /// </summary>
    /// <param name="connection">
    /// The TCP/IP connection to the client that already completed a WebSocket handshake.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="connection"/> is not readable or writable.
    /// </exception>
    /// <remarks>
    /// This class will takeover <paramref name="connection"/>.
    /// </remarks>
    public ClientEndpoint(Stream connection)
        : base(connection)
    {
    }

    protected override IMemoryOwner<byte>? GenerateMaskingKey()
    {
        // A server MUST NOT mask any frames that it sends to the client.
        return null;
    }
}
