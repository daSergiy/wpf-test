namespace H.OpenVpn;

public struct RemoteIpAddress
{
    public RemoteIpAddress(string? ipAddress, int? port)
    {
        IpAddress = ipAddress;
        Port = port;
    }

    public string? IpAddress { get; }

    public int? Port { get; }
}