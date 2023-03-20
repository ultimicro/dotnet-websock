namespace WebSock.AspNetCore;

public interface IWebSockFeature
{
    Task<Stream> AcceptAsync();
}
