namespace WebSock;

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
        await this.DisposeAsyncCore();
        this.Dispose(false);
        GC.SuppressFinalize(this);
    }

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

        await this.connection.DisposeAsync();
    }
}
