using System;
using System.IO;

namespace H.OpenVpn.Utilities;

public struct OpenVpnConfig
{
    public static OpenVpnConfig Create(string filePath)
    {
        CheckFileExtension(filePath);

        return new OpenVpnConfig(filePath, false);
    }

    public static OpenVpnConfig CreateUsingTempFile(string filePath)
    {
        CheckFileExtension(filePath);

        string? config = File.ReadAllText(filePath);
        string tempFilePath = Path.GetTempFileName();
        File.WriteAllText(tempFilePath, config);

        return new OpenVpnConfig(tempFilePath, true);
    }

    private static void CheckFileExtension(string filePath)
    {
        if (!Path.GetExtension(filePath)
                        ?.Equals(".ovpn", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new ArgumentException($"OpenVPN config file have \".ovpn\" extension but received file path has \"{Path.GetExtension(filePath)}\" extension");
        }
    }

    private OpenVpnConfig(string configPath, bool isTempFile)
    {
        ConfigPath = configPath;
        IsTempFile = isTempFile;
    }

    public string ConfigPath { get; }

    private bool IsTempFile { get; }

    public void DeleteTempFile()
    {
        try
        {
            if (IsTempFile && ConfigPath != null && File.Exists(ConfigPath))
            {
                File.Delete(ConfigPath);
            }
        }
        catch
        {
            // ignored
        }
    }
}