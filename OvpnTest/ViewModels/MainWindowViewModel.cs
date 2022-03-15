using H.OpenVpn;
using ReactiveUI;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Input;

namespace OvpnTest.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private HOpenVpn? openVpn = null;
        private string _ovpnFilePath = string.Empty;
        private string _log = string.Empty;

        public MainWindowViewModel()
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            OvpnFilePath = Path.Combine(folder, "response_6.ovpn");

            TryConnectCommand = ReactiveCommand.Create(() =>
            {
                try
                {
                    if (openVpn != null)
                    {
                        openVpn.Stop();
                        openVpn.Dispose();
                    }
                    //string? config = File.ReadAllText(OvpnFilePath);
                    openVpn = new HOpenVpn();

                    openVpn.ExceptionOccurred += LogMessage;
                    openVpn.StateChanged += LogMessage;
                    openVpn.InternalStateObtained += LogMessage;
                    openVpn.BytesInCountChanged += LogMessage;
                    openVpn.BytesOutCountChanged += LogMessage;
                    openVpn.LogObtained += LogMessage;
                    openVpn.ConsoleLineReceived += LogMessage;
                    openVpn.ManagementLineReceived += LogMessage;
                    openVpn.ConsoleLineSent += LogMessage;
                    openVpn.ManagementLineSent += LogMessage;

                    openVpn.Start(OvpnFilePath, null, null);
                }
                catch (Exception e)
                {
                    LogMessage(null, e);
                }
            },
            this.WhenAnyValue(x => x.OvpnFilePath,
                (ovpnFilePath) => !string.IsNullOrEmpty(ovpnFilePath)
                    && Path.GetExtension(ovpnFilePath) == ".ovpn"));
        }

        public string OvpnFilePath
        {
            get => _ovpnFilePath;
            set => this.RaiseAndSetIfChanged(ref _ovpnFilePath, value);
        }

        public string Log
        {
            get => _log;
            set => this.RaiseAndSetIfChanged(ref _log, value);
        }

        public ICommand TryConnectCommand { get; }

        private void LogMessage<T>(object? sender, T args)
        {
            Log += args?.ToString() + Environment.NewLine;
        }
    }
}