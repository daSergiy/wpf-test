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

                    //var openVpn = new LinuxOpenVpn();
                    var openVpn = new WindowsOpenVpn();
                    OpenVpn = openVpn;
                    openVpn.Start(OvpnFilePath, null, null);
                    //openVpn.ConsoleListener!.ExceptionOccurred += LogMessage;
                    //openVpn.ConsoleListener!.LineReceived += LogMessage;
                    //openVpn.ConsoleListener!.RemoteAddressRead += LogMessage;

                    openVpn.ManagmentInterface!.LineReceived += LogMessage;
                    openVpn.ManagmentInterface!.LineSent += LogMessage;
                    openVpn.ManagmentInterface!.InternalStateObtained += (obj, state) =>
                        Log += $"> STATE: LocalIp - {state.LocalIp}; RemoteIp - {state.RemoteIp}{Environment.NewLine}";
                    openVpn.ManagmentInterface!.StateChanged += (obj, newState) => State = newState.ToString();
                    openVpn.ManagmentInterface!.BytesInCountChanged += (obj, inbytes) => InBytes = inbytes;
                    openVpn.ManagmentInterface!.BytesOutCountChanged += (obj, outBytes) => OutBytes = outBytes; ;
                    openVpn.ManagmentInterface!.LogObtained += LogMessage;
                    openVpn.ManagmentInterface!.ExceptionOccurred += LogMessage;

                    await openVpn.ManagmentInterface!.SubscribeByteCountAsync();
                    await openVpn.ManagmentInterface!.SubscribeStateAsync();
                    await openVpn.ManagmentInterface!.SubscribeLogAsync();
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
        private OpenVpnBase? OpenVpn { get; set; }

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