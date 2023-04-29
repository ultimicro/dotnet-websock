namespace WebSock;

public enum Opcode : byte
{
    /// <summary>
    /// Denotes a continuation frame.
    /// </summary>
    Continuation = 0x0,

    /// <summary>
    /// Denotes a text frame.
    /// </summary>
    Text = 0x1,

    /// <summary>
    /// Denotes a binary frame.
    /// </summary>
    Binary = 0x2,

    /// <summary>
    /// Denotes a connection close.
    /// </summary>
    ConnectionClose = 0x8,

    /// <summary>
    /// Denotes a ping.
    /// </summary>
    Ping = 0x9,

    /// <summary>
    /// Denotes a pong.
    /// </summary>
    Pong = 0xa,
}
