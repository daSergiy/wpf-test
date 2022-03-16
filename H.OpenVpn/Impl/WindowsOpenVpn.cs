using H.OpenVpn.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace H.OpenVpn;

public class WindowsOpenVpn : OpenVpnBase
{
    private OpenVpnConfig? Config { get; set; }

    private Process? Process { get; set; }

    public OpenVpnManagementInterface? ManagmentInterface { get; private set; }

    public OpenVpnConsoleListener? ConsoleListener { get; private set; }

    public override void Start(string configFilePath, string? username, string? password)
    {
        Config = OpenVpnConfig.Create(configFilePath);

        string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
        string path = Path.Combine(folder, "OpenVPN", "openvpn.exe");
        int port = 8888;    //NetworkUtilities.GetFreeTcpPort();

        Process = Process.Start(new ProcessStartInfo(path,
            $"--config \"{Config}\" " +
            $"--management 127.0.0.1 {port} " +
            "--verb 3 "/* +
            "--management-query-passwords " +
            "--remap-usr1 SIGTERM"*/)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        });

        ManagmentInterface = OpenVpnManagementInterface.Create(port);
        ConsoleListener = OpenVpnConsoleListener.Create(Process!);
    }

    public override void Stop()
    {
        Dispose();
    }

    protected override void DisposeCore()
    {
        ManagmentInterface?.Dispose();
        ManagmentInterface = null;
        ConsoleListener?.Dispose();
        ConsoleListener = null;
        Config?.DeleteTempFile();
        Config = null;

        try
        {
            Process?.Kill();
        }
        catch (InvalidOperationException)
        {
            // ignored
        }

        Process?.Dispose();
        Process = null;
    }

    //public void WaitForExit(TimeSpan timeout)
    //{
    //    Process?.WaitForExit((int)timeout.TotalMilliseconds);
    //}
}