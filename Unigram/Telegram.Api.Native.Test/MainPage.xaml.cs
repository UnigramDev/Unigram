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
using System.Diagnostics;
using Telegram.Api.TL.Methods.Contacts;
using System.ComponentModel;
using Telegram.Api.TL.Methods.Upload;
using Telegram.Api.TL.Methods.Messages;

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

            TLTestObject.Register();
        }

        private void ConnectionManager_ConnectionStateChanged(ConnectionManager sender, object args)
        {
        }

        private void ConnectionManager_UnprocessedMessageReceived(ConnectionManager sender, MessageResponse args)
        {
            switch (args.Object)
            {
                case TLConfig tlConfig:
                    var tmpSession = tlConfig.TmpSessions;
                    var dcOptions = tlConfig.DCOptions.ToArray();
                    var disabledFeatures = tlConfig.DisabledFeatures.ToArray();
                    break;
                case TLRPCError tlRPCError:
                    break;
            }
        }

        private void Instance_CurrentNetworkTypeChanged(ConnectionManager sender, object e)
        {
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ConnectionManager.Instance.SendRequest(new TLHelpInviteText(), (message5, ex5) =>
            {
                Debugger.Break();
            },
            // Should run on Download connection
            null, ConnectionManager.DefaultDatacenterId, ConnectionType.Generic, RequestFlag.WithoutLogin | RequestFlag.EnableUnauthorized);

            GC.Collect();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var connectionManager = ConnectionManager.Instance;
            var authSendCode = new TLAuthSendCode { ApiHash = Constants.ApiHash, ApiId = Constants.ApiId, PhoneNumber = Constants.PhoneNumber };
            var messageToken = connectionManager.SendRequest(authSendCode, (message, ex) =>
            {
                var sentCode = message.Object as TLAuthSentCode;
                if (sentCode == null)
                {
                    Debugger.Break();
                    return;
                }

                var authSignIn = new TLAuthSignIn { PhoneNumber = Constants.PhoneNumber, PhoneCode = Constants.PhoneCode, PhoneCodeHash = sentCode.PhoneCodeHash };
                connectionManager.SendRequest(authSignIn, (message2, ex2) =>
                {
                    var authorization = message2.Object as TLAuthAuthorization;
                    if (authorization == null)
                    {
                        Debugger.Break();
                        return;
                    }

                    connectionManager.UserId = authorization.User.Id;
                    Debugger.Break();
                },
                null, ConnectionManager.DefaultDatacenterId, ConnectionType.Generic, RequestFlag.WithoutLogin);
            },
            null, ConnectionManager.DefaultDatacenterId, ConnectionType.Generic, RequestFlag.WithoutLogin);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            ConnectionManager.Instance.SendRequest(new TLHelpGetConfig(), (message5, ex5) =>
            {
                Debugger.Break();
            },
          // Should run on Download connection
          null, ConnectionManager.DefaultDatacenterId, ConnectionType.Generic, RequestFlag.WithoutLogin | RequestFlag.EnableUnauthorized);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            ConnectionManager.Instance.BoomBaby(null, out ITLObject xxx);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            var connectionManager = ConnectionManager.Instance;
            var resolve = new TLContactsResolveUsername { Username = Constants.Resolve };
            connectionManager.SendRequest(resolve, (message3, ex3) =>
            {
                var resolvedPeer = message3.Object as TLContactsResolvedPeer;
                if (resolvedPeer == null)
                {
                    Debugger.Break();
                    return;
                }

                var user = resolvedPeer.Users.FirstOrDefault() as TLUser;
                if (user == null)
                {
                    Debugger.Break();
                    return;
                }

                var photo = user.Photo as TLUserProfilePhoto;
                var big = photo.PhotoBig as TLFileLocation;

                var getFile = new TLUploadGetFile { Offset = 0, Limit = 32 * 1024, Location = new TLInputFileLocation { VolumeId = big.VolumeId, LocalId = big.LocalId, Secret = big.Secret } };
                connectionManager.SendRequest(getFile, (message5, ex5) =>
                {
                    Debugger.Break();
                },
                // Should run on Download connection
                null, big.DCId, ConnectionType.Generic, RequestFlag.TryDifferentDc);
            },
            null, ConnectionManager.DefaultDatacenterId, ConnectionType.Generic);
        }
    }
}

namespace Telegram.Api.TL
{
    public class TLObject : ITLObject
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual TLType TypeId => TLType.None;

        public virtual void Read(TLBinaryReader reader)
        {
            throw new NotImplementedException();
        }

        public virtual void Write(TLBinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public uint Constructor => (uint)TypeId;
    }
}
