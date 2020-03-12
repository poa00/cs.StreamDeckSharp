using OpenMacroBoard.SDK;
using System;
using System.Text;

namespace StreamDeckSharp.Internals
{
    internal class BasicHidClient : IStreamDeckBoard
    {
        private readonly bool[] keyStateData;
        private readonly object disposeLock = new object();

        public BasicHidClient(IStreamDeckHid deckHid, IHardwareInternalInfos hardwareInformation)
        {
            DeckHid = deckHid;
            Keys = hardwareInformation.Keys;

            deckHid.ConnectionStateChanged += (s, e) => ConnectionStateChanged?.Invoke(this, e);
            deckHid.ReportReceived += DeckHid_ReportReceived;

            HardwareInfo = hardwareInformation;
            Buffer = new byte[deckHid.OutputReportLength];
            keyStateData = new bool[Keys.Count];
            KeyStates = new KeyStateCollection(keyStateData);
        }

        public event EventHandler<KeyEventArgs> KeyStateChanged;
        public event EventHandler<ConnectionEventArgs> ConnectionStateChanged;

        public GridKeyPositionCollection Keys { get; }
        IKeyPositionCollection IMacroKeyArea.Keys => Keys;
        public bool IsDisposed { get; private set; }
        public bool IsConnected => DeckHid.IsConnected;
        public IKeyStateCollection KeyStates { get; }

        protected IStreamDeckHid DeckHid { get; }
        protected IHardwareInternalInfos HardwareInfo { get; }
        protected byte[] Buffer { get; private set; }

        public void Dispose()
        {
            lock (disposeLock)
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
            }

            Shutdown();
            ShowLogoWithoutDisposeVerification();

            DeckHid.Dispose();

            Dispose(true);
        }

        public string GetFirmwareVersion()
        {
            if (!DeckHid.ReadFeatureData(HardwareInfo.FirmwareVersionFeatureId, out var featureData))
            {
                return null;
            }

            return Encoding.UTF8.GetString(featureData, HardwareInfo.FirmwareReportSkip, featureData.Length - HardwareInfo.FirmwareReportSkip).Trim('\0');
        }

        public string GetSerialNumber()
        {
            if (!DeckHid.ReadFeatureData(HardwareInfo.SerialNumberFeatureId, out var featureData))
            {
                return null;
            }

            return Encoding.UTF8.GetString(featureData, HardwareInfo.SerialNumberReportSkip, featureData.Length - HardwareInfo.SerialNumberReportSkip).Trim('\0');
        }

        public void SetBrightness(byte percent)
        {
            VerifyNotDisposed();
            DeckHid.WriteFeature(HardwareInfo.GetBrightnessMessage(percent));
        }

        public virtual void SetKeyBitmap(int keyId, KeyBitmap bitmapData)
        {
            keyId = HardwareInfo.ExtKeyIdToHardwareKeyId(keyId);

            var payload = HardwareInfo.GeneratePayload(bitmapData);

            foreach (var report in OutputReportSplitter.Split(payload, Buffer, HardwareInfo.ReportSize, HardwareInfo.HeaderSize, keyId, HardwareInfo.PrepareDataForTransmittion))
            {
                DeckHid.WriteReport(report);
            }
        }

        public void ShowLogo()
        {
            VerifyNotDisposed();
            ShowLogoWithoutDisposeVerification();
        }

        protected virtual void Shutdown()
        {
        }

        protected virtual void Dispose(bool managed)
        {
        }

        protected void VerifyNotDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(BasicHidClient));
            }
        }

        private void DeckHid_ReportReceived(object sender, ReportReceivedEventArgs e)
        {
            ProcessKeys(e.ReportData);
        }

        private void ProcessKeys(byte[] newStates)
        {
            for (var i = 0; i < keyStateData.Length; i++)
            {
                var stateInReportIndex = i + HardwareInfo.KeyReportOffset;
                var currentKeyState = newStates[stateInReportIndex] != 0;

                if (keyStateData[i] != currentKeyState)
                {
                    var externalKeyId = HardwareInfo.HardwareKeyIdToExtKeyId(i);
                    keyStateData[i] = currentKeyState;
                    KeyStateChanged?.Invoke(this, new KeyEventArgs(externalKeyId, currentKeyState));
                }
            }
        }

        private void ShowLogoWithoutDisposeVerification()
        {
            DeckHid.WriteFeature(HardwareInfo.GetLogoMessage());
        }
    }
}
