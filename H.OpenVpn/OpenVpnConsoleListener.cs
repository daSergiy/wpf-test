using H.OpenVpn.Utilities;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace H.OpenVpn;

public class OpenVpnConsoleListener : IDisposable
{
    private bool _disposedValue;

    private OpenVpnConsoleListener(Process process)
    {
        Process = process;
        Output = Channel.CreateUnbounded<string>();
        var _ = ListenAsync();
    }

    public static OpenVpnConsoleListener Create(Process proceess)
    {
        return new OpenVpnConsoleListener(proceess);
    }

    private Process Process { get; }

    private Channel<string> Output { get; }

    private CancellationTokenSource? Cancellation { get; set; } = new CancellationTokenSource();

    public event EventHandler<Exception>? ExceptionOccurred;

    private void OnExceptionOccurred(Exception value)
    {
        ExceptionOccurred?.Invoke(this, value);
    }

    public event EventHandler<string?>? LineReceived;

    private void OnLineReceived(string? value)
    {
        LineReceived?.Invoke(this, value);
    }

    public event EventHandler<RemoteIpAddress>? RemoteAddressRead;

    private void OnRemoteAddressRead(string ipAddress, int? port)
    {
        RemoteAddressRead?.Invoke(this, new RemoteIpAddress(ipAddress, port));
    }

    private async Task ListenAsync()
    {
        while (Cancellation != null && !Cancellation.IsCancellationRequested)
        {
            try
            {
                string? line;
                try
                {
                    line = await ReadLineAsync(Cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Cancellation?.Dispose();
                    break;
                }

                if (line == null)
                {
                    break;
                }

                if (line.Contains("link remote: [AF_INET]"))
                {
                    int ipIndex = line.LastIndexOf(']') + 1;
                    int portIndex = line.LastIndexOf(':') + 1;
                    string? remoteIpAddress = line.Substring(ipIndex, portIndex - ipIndex - 1);
                    string? remoteIpPort = line.Substring(portIndex);

                    if (int.TryParse(remoteIpPort, out int ipPort))
                    {
                        OnRemoteAddressRead(remoteIpAddress, ipPort);
                    }
                    else
                    {
                        OnRemoteAddressRead(remoteIpAddress, null);
                    }

                    continue;
                }

                await Output.Writer.WriteAsync(line).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                OnExceptionOccurred(exception);
            }
        }
    }

    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        string? line = await Process.StandardOutput
            .ReadLineAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        string? message = line?.Trim('\r', '\n');
        OnLineReceived(message);

        return message;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                Cancellation?.Cancel();
                Cancellation?.Dispose();
                Cancellation = null;
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}