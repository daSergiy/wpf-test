using H.OpenVpn.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace H.OpenVpn;

public class OpenVpnManagementInterface : IDisposable
{
    private VpnState _vpnState = VpnState.Inactive;
    private long _bytesInCount;
    private long _bytesOutCount;
    private bool disposedValue;

    public static OpenVpnManagementInterface Create(int tcpPort)
    {
        return new OpenVpnManagementInterface(tcpPort, null, null);
    }

    public static OpenVpnManagementInterface Create(int tcpPort, string username, string password)
    {
        return new OpenVpnManagementInterface(tcpPort, username, password);
    }

    private OpenVpnManagementInterface(int tcpPort, string? username, string? password)
    {
        TcpClientWrapper = new TcpClientWrapper(new TimeSpan(0, 0, 5));
        TcpClientWrapper.Connect(IPAddress.Loopback, tcpPort);

        var stream = TcpClientWrapper.TcpClient.GetStream();
        StreamReader = new StreamReader(stream);
        StreamWriter = new StreamWriter(stream);

        var _ = ListenManagementAsync(username, password);
    }

    private TcpClientWrapper? TcpClientWrapper { get; set; }

    private StreamReader? StreamReader { get; set; }

    private StreamWriter? StreamWriter { get; set; }

    private Channel<string> ManagementOutput { get; } = Channel.CreateUnbounded<string>();

    private CancellationTokenSource? Cancellation { get; set; } = new CancellationTokenSource();

    private TaskCompletionSource<bool> AuthenticationCompletion { get; } = new TaskCompletionSource<bool>();

    public VpnState VpnState
    {
        get => _vpnState;

        protected set
        {
            _vpnState = value;
            OnStateChanged(value);
        }
    }

    public long BytesInCount
    {
        get => _bytesInCount;

        protected set
        {
            _bytesInCount = value;
            OnBytesInCountChanged(value);
        }
    }

    public long BytesOutCount
    {
        get => _bytesOutCount;

        protected set
        {
            _bytesOutCount = value;
            OnBytesOutCountChanged(value);
        }
    }

    public string LocalInterfaceAddress { get; set; } = string.Empty;

    public string RemoteIpAddress { get; set; } = string.Empty;

    public event EventHandler<string?>? LineReceived;

    private void OnLineReceived(string? value)
    {
        LineReceived?.Invoke(this, value);
    }

    public event EventHandler<string>? LineSent;

    private void OnLineSent(string value)
    {
        LineSent?.Invoke(this, value);
    }

    public event EventHandler<State>? InternalStateObtained;

    private void OnInternalStateObtained(State value)
    {
        InternalStateObtained?.Invoke(this, value);
    }

    public event EventHandler<VpnState>? StateChanged;

    private void OnStateChanged(VpnState value)
    {
        StateChanged?.Invoke(this, value);
    }

    public event EventHandler<long>? BytesInCountChanged;

    private void OnBytesInCountChanged(long value)
    {
        BytesInCountChanged?.Invoke(this, value);
    }

    public event EventHandler<long>? BytesOutCountChanged;

    private void OnBytesOutCountChanged(long value)
    {
        BytesOutCountChanged?.Invoke(this, value);
    }

    public event EventHandler<string>? LogObtained;

    private void OnLogObtained(string value)
    {
        LogObtained?.Invoke(this, value);
    }

    public event EventHandler<Exception>? ExceptionOccurred;

    private void OnExceptionOccurred(Exception value)
    {
        ExceptionOccurred?.Invoke(this, value);
    }

    private async Task ListenManagementAsync(string? username, string? password)
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

                if (line.StartsWith(">PASSWORD:Need 'Auth' username/password"))
                {
                    await WriteLineAsync($"username \"Auth\" {username}").ConfigureAwait(false);
                    await ReadLineAsync().ConfigureAwait(false);

                    await WriteLineAsync($"password \"Auth\" {password}").ConfigureAwait(false);
                    await ReadLineAsync().ConfigureAwait(false);

                    AuthenticationCompletion.TrySetResult(true);
                    continue;
                }

                if (line.StartsWith(">PASSWORD:Auth-Token"))
                {
                    continue;
                }

                if (line.StartsWith(">INFO:"))
                {
                    // ignore all INFO response
                    continue;
                }

                if (line.StartsWith(">BYTECOUNT:"))
                {
                    // split the string without prefix ">BYTECOUNT:"
                    string[] nums = line.Substring(11).Split(',');

                    if (nums.Length > 1)
                    {
                        BytesInCount = long.Parse(nums[0]);
                        BytesOutCount = long.Parse(nums[1]);
                    }
                    continue;
                }

                if (line.StartsWith(">STATE:"))
                {
                    var state = State.Parse(line.Substring(7));
                    switch (state.Name)
                    {
                        case "WAIT":
                            VpnState = VpnState.Connecting;
                            break;

                        case "RESOLVE":
                        case "AUTH":
                        case "GET_CONFIG":
                            break;

                        case "ASSIGN_IP":
                            LocalInterfaceAddress = state.LocalIp;
                            break;

                        case "CONNECTED":
                            if (!string.IsNullOrWhiteSpace(state.RemoteIp))
                            {
                                RemoteIpAddress = state.RemoteIp;
                            }
                            VpnState = VpnState.Connected;
                            break;

                        case "EXITING":
                            VpnState = VpnState.Exiting;
                            break;
                    }

                    OnInternalStateObtained(state);
                    continue;
                }

                if (line.StartsWith(">LOG:"))
                {
                    // string without prefix ">LOG:"
                    OnLogObtained(line.Substring(5));
                    continue;
                }

                await ManagementOutput.Writer.WriteAsync(line).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                OnExceptionOccurred(exception);
            }
        }
    }

    public async Task<long> GetPidAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineAsync("pid", cancellationToken).ConfigureAwait(false);

        while (await ManagementOutput.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (ManagementOutput.Reader.TryRead(out string message))
            {
                var regExpPattern = new Regex(@".*pid=(\d+)", RegexOptions.Compiled);

                var match = regExpPattern.Match(message);
                if (match.Success)
                {
                    return long.Parse(match.Groups[1].Value);
                }

                return 0;
            }
        }

        return 0;
    }

    public async Task<ICollection<State>> GetStatesAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineAsync("state all", cancellationToken).ConfigureAwait(false);

        var states = new List<State>();

        while (await ManagementOutput.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (ManagementOutput.Reader.TryRead(out string message))
            {
                if (message == "END")
                {
                    return states;
                }

                states.Add(State.Parse(message));
            }
        }

        return states;
    }

    public async Task<string> GetLogsAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineAsync("log all", cancellationToken).ConfigureAwait(false);

        string logs = "";

        while (await ManagementOutput.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (ManagementOutput.Reader.TryRead(out string message))
            {
                if (message == "END")
                {
                    return logs;
                }

                logs += message + Environment.NewLine;
            }
        }

        return logs;
    }

    public async Task SubscribeStateAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineAsync("state on", cancellationToken).ConfigureAwait(false);
    }

    public async Task SubscribeByteCountAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineAsync("bytecount 5", cancellationToken).ConfigureAwait(false);
    }

    public async Task SubscribeLogAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineAsync("log on", cancellationToken).ConfigureAwait(false);
    }

    public async Task WaitAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        await AuthenticationCompletion.Task
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SendSignalAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        await WriteLineAsync($"signal {signal:G}", cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineAsync("exit", cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
    {
        StreamWriter = StreamWriter ?? throw new InvalidOperationException("StreamWriter is null");

        await StreamWriter
            .WriteLineAsync(line)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);
        await StreamWriter
            .FlushAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        OnLineSent(line);
    }

    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        StreamReader = StreamReader ?? throw new InvalidOperationException("StreamReader is null");

        string? line = await StreamReader.ReadLineAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        string? message = line?.Trim('\r', '\n');
        OnLineReceived(message);

        return message;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Cancellation?.Cancel();
                Cancellation?.Dispose();
                Cancellation = null;
                TcpClientWrapper?.Dispose();
                TcpClientWrapper = null;
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}