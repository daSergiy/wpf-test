using H.OpenVpn;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Input;

namespace OvpnTest.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            OvpnFilePath = Path.Combine(folder, "response_6.ovpn");

            TryConnectCommand = ReactiveCommand.Create(async () =>
            {
                try
                {
                    DisposeOpenVpn();

                    OpenVpn = new HOpenVpn();
                    OpenVpn.ExceptionOccurred += LogMessage<Exception>;
                    OpenVpn.StateChanged += (obj, newState) => State = newState.ToString();
                    OpenVpn.BytesInCountChanged += (obj, inbytes) => InBytes = inbytes;
                    OpenVpn.BytesOutCountChanged += (obj, outBytes) => OutBytes = outBytes; ;
                    OpenVpn.InternalStateObtained += (obj, state) =>
                        Log += $"> STATE: LocalIp - {state.LocalIp}; RemoteIp - {state.RemoteIp}{Environment.NewLine}";
                    OpenVpn.LogObtained += LogMessage;
                    OpenVpn.ConsoleLineReceived += LogMessage;
                    OpenVpn.ManagementLineReceived += LogMessage;
                    OpenVpn.ConsoleLineSent += LogMessage;
                    OpenVpn.ManagementLineSent += LogMessage;
                    OpenVpn.Start(OvpnFilePath, null, null);
                    await OpenVpn.SubscribeByteCountAsync();
                    await OpenVpn.SubscribeStateAsync();
                    await OpenVpn.SubscribeLogAsync();
                }
                catch (Exception e)
                {
                    DisposeOpenVpn();
                    LogMessage(null, e);
                }
            },
            this.WhenAnyValue(x => x.OvpnFilePath,
                (ovpnFilePath) => !string.IsNullOrEmpty(ovpnFilePath)
                    && Path.GetExtension(ovpnFilePath) == ".ovpn"));

            DisconnectCommand = ReactiveCommand.Create(DisposeOpenVpn/*, this.WhenAnyValue(x => x.OpenVpn, vpn => vpn != null)*/);
        }

        [Reactive]
        public string OvpnFilePath { get; set; }

        [Reactive]
        public string Log { get; set; } = string.Empty;

        [Reactive]
        private HOpenVpn? OpenVpn { get; set; }

        [Reactive]
        public string State { get; set; } = string.Empty;

        [Reactive]
        public long InBytes { get; set; }

        [Reactive]
        public long OutBytes { get; set; }

        public ICommand TryConnectCommand { get; }

        public ICommand DisconnectCommand { get; }

        private void LogMessage<T>(object? sender, T args)
        {
            Log += args?.ToString() + Environment.NewLine;
        }

        private void DisposeOpenVpn()
        {
            Log = string.Empty;
            State = string.Empty;
            InBytes = 0;
            OutBytes = 0;

            if (OpenVpn != null)
            {
                OpenVpn.Stop();
                OpenVpn.Dispose();
                OpenVpn = null;
            }
        }
    }
}