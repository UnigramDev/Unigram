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
using Windows.ApplicationModel.DataTransfer;
using System.Diagnostics;
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

namespace Unigram.Views
{
    public sealed partial class ShareTargetPage : Page
    {
        public MainViewModel ViewModel => DataContext as MainViewModel;
        private object _selectedDialog;
        private bool answerDirty = false;

        // Create the ShareOperator and it's friends
        ShareOperation shareOperation;
        private string sharedTitle;
        private string sharedDescription;
        private string sharedText;
        private Uri sharedWebLink;


        public ShareTargetPage()
        {
            this.InitializeComponent();
            DataContext = UnigramContainer.Instance.ResolverType<MainViewModel>();
        }


        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            this.shareOperation = (ShareOperation)e.Parameter;

            await Task.Factory.StartNew(async () =>
            {
                // Get the properties of the shared package
                this.sharedTitle = this.shareOperation.Data.Properties.Title;
                this.sharedDescription = this.shareOperation.Data.Properties.Description;

                // Now let's get the content! :)
                // Text
                if (this.shareOperation.Data.Contains(StandardDataFormats.Text))
                {
                    try
                    {
                        this.sharedText = await this.shareOperation.Data.GetTextAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("ERROR - ShareTargetHelper - GetText - " + ex);
                    }
                }

                // Web-link
                //if (this.shareOperation.Data.Contains(StandardDataFormats.WebLink))
                //{
                //    try
                //    {
                //        this.sharedWebLink = await this.shareOperation.Data.GetWebLinkAsync();
                //    }
                //    catch (Exception ex)
                //    {
                //        Debug.WriteLine("ERROR - ShareTargetHelper - GetLink - " + ex);
                //    }
                //}
            });
        }



        // Button
        private async void lvMasterChats_ItemClick(object sender, ItemClickEventArgs e)
        {
            //TODO: Put the handling of the message in an background task to the user can quickly return to his/her work
            await this.showQuestionDialog();
            if (answerDirty == true)
            {
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

                protoService.Initialized += (s, args) =>
                {
                    var replyToMsgId = 0;

                    // First, get the dialogId to which we have to send the message
                    var toId = default(TLPeerBase);
                    toId = new TLPeerUser { Id = dialog.WithId };


                    // Now, prepare the message with the correct date and message itself.
                    var date = TLUtils.DateToUniversalTimeTLInt(protoService.ClientTicksDelta, DateTime.Now);
                    var message = TLUtils.GetMessage(SettingsHelper.UserId, toId, TLMessageState.Sending, true, true, date, sharedText, new TLMessageMediaEmpty(), TLLong.Random(), replyToMsgId);
                    var history = cacheService.GetHistory(SettingsHelper.UserId, toId, 1);

                    // Now send the darn thing
                    cacheService.SyncSendingMessage(message, null, toId, async (m) =>
                    {
                        await protoService.SendMessageAsync(message);
                        manualResetEvent.Set();
                    });
                };
                protoService.InitializationFailed += (s, args) =>
                {
                    manualResetEvent.Set();
                };
                cacheService.Initialize();
                protoService.Initialize();
                manualResetEvent.WaitOne(4000);

                this.showErrorDialog();

                // Now close the shareoperation
                //this.shareOperation.ReportCompleted();
            }

        }


        public async Task<bool> showQuestionDialog()
        {
            bool answer = false;
            answerDirty = false;

            // Show Error dialog
            var updatedDialog = new MessageDialog("Do you want to send a message to this person?", "Share text");
            updatedDialog.Commands.Add(new UICommand("Yes", new UICommandInvokedHandler(this.CommandInvokedHandlerAnswerYes)));
            updatedDialog.Commands.Add(new UICommand("No", new UICommandInvokedHandler(this.CommandInvokedHandlerAnswerNo)));
            // Extra code to select the Close-option when an user presses on the Escape-button
            updatedDialog.CancelCommandIndex = 1;
            // Show Dialog
            await updatedDialog.ShowAsync();
            answer = answerDirty;
            return answer;
        }

        public async void showErrorDialog()
        {
            // Show Error dialog
            MessageDialog errorDialog = new MessageDialog("Temp content");
            errorDialog.Content = "The message has been send!";

            errorDialog.Commands.Add(new UICommand("Close", new UICommandInvokedHandler(this.CommandInvokedHandlerClose)));
            // Extra code to select the Close-option when an user presses on the Escape-button
            errorDialog.CancelCommandIndex = 0;
            // Show Dialog
            await errorDialog.ShowAsync();
        }
        private void CommandInvokedHandlerClose(IUICommand command)
        {
            this.shareOperation.ReportCompleted();
        }
        private void CommandInvokedHandlerAnswerYes(IUICommand command)
        {
            answerDirty = true;
        }
        private void CommandInvokedHandlerAnswerNo(IUICommand command)
        {
            answerDirty = false;
        }
    }
}
