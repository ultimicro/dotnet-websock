namespace WebSock.Abstractions;

public class WebSockException : Exception
{
    public WebSockException()
    {
    }

    public WebSockException(string? message)
        : base(message)
    {
    }

    public WebSockException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
