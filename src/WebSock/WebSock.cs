namespace WebSock;

using System.Diagnostics;

public sealed class WebSock : IAsyncDisposable, IDisposable
{
    private readonly Stream stream;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSock"/> class.
    /// </summary>
    /// <param name="stream">
    /// The TCP/IP connection between the endpoint.
    /// </param>
    /// <remarks>
    /// This class will takeover <paramref name="stream"/>.
    /// </remarks>
    public WebSock(Stream stream)
    {
        this.stream = stream;
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.Dispose(true);
    }

    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        await this.stream.DisposeAsync();

        this.Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        Debug.Assert(!this.disposed, "The object is already disposed.");

        if (disposing)
        {
            this.stream.Dispose();
        }

        this.disposed = true;
    }
}
