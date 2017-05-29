using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Telegram.Api.Native.TL;
using Windows.Security.Cryptography;

// Il modello di elemento Pagina vuota è documentato all'indirizzo https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x410

namespace Telegram.Api.Native.Test
{
    /// <summary>
    /// Pagina vuota che può essere usata autonomamente oppure per l'esplorazione all'interno di un frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            var connectionManager = ConnectionManager.Instance;
            connectionManager.CurrentNetworkTypeChanged += Instance_CurrentNetworkTypeChanged;
            connectionManager.ConnectionStateChanged += ConnectionManager_ConnectionStateChanged;
            connectionManager.UnprocessedMessageReceived += ConnectionManager_UnprocessedMessageReceived;

            //connectionManager.SendRequest(new TLError(0, "Hello world"), null, null, 1, ConnectionType.Generic, false, 0);

            connectionManager.BoomBaby(null, out ITLObject @object);

            GC.Collect();
        }

        private void ConnectionManager_ConnectionStateChanged(ConnectionManager sender, object args)
        {
        }

        private void ConnectionManager_UnprocessedMessageReceived(ConnectionManager sender, TLUnprocessedMessage args)
        {
            var tlConfig = args.Object as TLConfig;
            if (tlConfig != null)
            {
                var tmpSession = tlConfig.TmpSessions;
                var dcOptions = tlConfig.DcOptions.ToArray();
                var disabledFeatures = tlConfig.DisabledFeatures.ToArray();
            }

            //var unconsumedBuffer = new byte[args.Reader.UnconsumedBufferLength];
            //args.Reader.ReadRawBuffer(unconsumedBuffer);

            //var text = System.Text.Encoding.Unicode.GetString(unconsumedBuffer);

            GC.Collect();
        }

        private void Instance_CurrentNetworkTypeChanged(ConnectionManager sender, object e)
        {
        }
    }
}
