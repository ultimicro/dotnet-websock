namespace WebSock.Abstractions;

public class MalformedHandshakeException : WebSockException
{
    public MalformedHandshakeException()
    {
    }

    public MalformedHandshakeException(string? message)
        : base(message)
    {
    }

    public MalformedHandshakeException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
