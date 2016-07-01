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
using Unigram.ViewModels;
using Unigram.Core.Dependency;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Core.Services;
using Telegram.Api.Aggregator;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Updates;
using Telegram.Api.Transport;
using Telegram.Api.Services.Connection;
using System.Threading;
using Telegram.Api.Services;
using Telegram.Api.Helpers;
using Windows.UI.Popups;
using Unigram.Helpers;

namespace Unigram.Views
{
    public sealed partial class ShareTargetPage : Page
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;
        private object _selectedDialog;
        private bool answerDirty = false;
        private ShareTargetHelper sth;

        public ShareTargetPage()
        {
            this.InitializeComponent();
            DataContext = UnigramContainer.Instance.ResolverType<MainViewModel>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            sth = new ShareTargetHelper((ShareOperation)e.Parameter);
        }

        // Button
        private async void lvMasterChats_ItemClick(object sender, ItemClickEventArgs e)
        {
            prgSendStatus.Value = 0;
            //TODO: Put the handling of the message in an background task to the user can quickly return to his/her work
            await this.showQuestionDialog();
            if (answerDirty == true)
            {
                prgSendStatus.Value = 10;
                // Set the selected Dialog with the clicked item
                _selectedDialog = e.ClickedItem;

                var dialog = e.ClickedItem as TLDialog;

                var deviceInfoService = new DeviceInfoService();
                var eventAggregator = new TelegramEventAggregator();
                var cacheService = new InMemoryCacheService(eventAggregator);
                var updatesService = new UpdatesService(cacheService, eventAggregator);
                var transportService = new TransportService();
                var connectionService = new ConnectionService(deviceInfoService);
                var manualResetEvent = new ManualResetEvent(false);
                var protoService = new MTProtoService(deviceInfoService, updatesService, cacheService, transportService, connectionService);
                prgSendStatus.Value = 20;

                protoService.Initialized += (s, args) =>
                {
                    var replyToMsgId = 0;

                    // First, get the dialogId to which we have to send the message
                    var toId = default(TLPeerBase);
                    toId = new TLPeerUser { Id = dialog.WithId };
                    prgSendStatus.Value = 30;

                    // Now, prepare the message with the correct date and message itself.
                    var date = TLUtils.DateToUniversalTimeTLInt(protoService.ClientTicksDelta, DateTime.Now);
                    prgSendStatus.Value = 40;

                    // Send the correct message according to the send content
                    if (sth.sharedWebLink != null)
                    {
                        // If the user shares a weblink then...
                        var message = TLUtils.GetMessage(SettingsHelper.UserId, toId, TLMessageState.Sending, true, true, date, sth.sharedWebLink.AbsoluteUri, new TLMessageMediaEmpty(), TLLong.Random(), replyToMsgId);
                        prgSendStatus.Value = 50;
                        cacheService.SyncSendingMessage(message, null, toId, async (m) =>
                        {
                            await protoService.SendMessageAsync(message);
                            manualResetEvent.Set();
                            prgSendStatus.Value = 60;
                        });
                    }
                    else
                    {
                        // In all other cases (mostly text only)
                        var message = TLUtils.GetMessage(SettingsHelper.UserId, toId, TLMessageState.Sending, true, true, date, sth.sharedText, new TLMessageMediaEmpty(), TLLong.Random(), replyToMsgId);
                        prgSendStatus.Value = 50;
                        cacheService.SyncSendingMessage(message, null, toId, async (m) =>
                        {
                            await protoService.SendMessageAsync(message);
                            manualResetEvent.Set();
                            prgSendStatus.Value = 60;
                        });
                    }

                    var history = cacheService.GetHistory(SettingsHelper.UserId, toId, 1);
                    prgSendStatus.Value = 70;
                };
                protoService.InitializationFailed += (s, args) =>
                {
                    manualResetEvent.Set();
                };
                cacheService.Initialize();
                prgSendStatus.Value = 80;
                protoService.Initialize();
                prgSendStatus.Value = 90;
                manualResetEvent.WaitOne(4000);
                prgSendStatus.Value = 100;

                // Now close the shareoperation
                sth.CloseShareTarget();
            }

        }


        public async Task<bool> showQuestionDialog()
        {
            bool answer = false;
            answerDirty = false;

            // Show Error dialog
            var updatedDialog = new MessageDialog("Share content with this chat?", "Share");
            updatedDialog.Commands.Add(new UICommand("Yes", new UICommandInvokedHandler(this.CommandInvokedHandlerAnswerYes)));
            updatedDialog.Commands.Add(new UICommand("No", new UICommandInvokedHandler(this.CommandInvokedHandlerAnswerNo)));

            // Extra code to select the Close-option when an user presses on the Escape-button
            updatedDialog.CancelCommandIndex = 1;

            // Show Dialog
            await updatedDialog.ShowAsync();
            answer = answerDirty;
            return answer;
        }

        private void CommandInvokedHandlerAnswerYes(IUICommand command)
        {
            answerDirty = true;
        }
        private void CommandInvokedHandlerAnswerNo(IUICommand command)
        {
            answerDirty = false;
        }

        //private void btnNo_Click(object sender, RoutedEventArgs e)
        //{

        //}

        //private void btnYes_Click(object sender, RoutedEventArgs e)
        //{

        //}
    }
}
