using System;

namespace H.OpenVpn;

public abstract class OpenVpnBase : IDisposable
{
    private bool disposedValue;

    public abstract void Start(string configFilePath, string? username, string? password);

    public abstract void Stop();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                DisposeCore();
            }

            disposedValue = true;
        }
    }

    protected abstract void DisposeCore();
}