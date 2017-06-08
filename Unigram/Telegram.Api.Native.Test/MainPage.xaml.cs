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
using Telegram.Api.TL.Methods.Auth;
using Telegram.Api.TL;

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

            TLObjectSerializer.RegisterObjectConstructor(0x5E002502, () => new TLAuthSentCode());

            connectionManager.BoomBaby(null, out ITLObject @object);

            var datacenter = connectionManager.CurrentDatacenter;

            GC.Collect();
        }

        private void ConnectionManager_ConnectionStateChanged(ConnectionManager sender, object args)
        {
        }

        private void ConnectionManager_UnprocessedMessageReceived(ConnectionManager sender, TLUnprocessedMessage args)
        {
            switch (args.Object)
            {
                case TLConfig tlConfig:
                    var tmpSession = tlConfig.TmpSessions;
                    var dcOptions = tlConfig.DCOptions.ToArray();
                    var disabledFeatures = tlConfig.DisabledFeatures.ToArray();
                    break;
                case TLError tlError:
                    break;
            }

            var messageToken = sender.SendRequest(new TLAuthSendCode()
            {
                ApiHash = Constants.ApiHash,
                ApiId = Constants.ApiId,
                PhoneNumber = Constants.PhoneNumber
            },
            (message, ex) =>
            {
                System.Diagnostics.Debugger.Break();
            },
            null, ConnectionManager.DefaultDatacenterId, ConnectionType.Generic, RequestFlag.WithoutLogin);
        }

        private void Instance_CurrentNetworkTypeChanged(ConnectionManager sender, object e)
        {
            var xxx = new TLObject();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
        }
    }
}

namespace Telegram.Api.TL
{
    public class TLObject : ITLObject
    {
        public virtual TLType TypeId => TLType.None;

        public virtual void Read(TLBinaryReader reader)
        {
            throw new NotImplementedException();
        }

        public virtual void Write(TLBinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        public uint Constructor => (uint)TypeId;
    }
}
