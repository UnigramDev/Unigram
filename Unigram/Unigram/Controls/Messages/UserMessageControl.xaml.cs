using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Messages
{
    public sealed partial class UserMessageControl : MessageControlBase
    {
        public UserMessageControl()
        {
            InitializeComponent();

            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null)
                {
                    Bindings.Update();
                }
            };
        }

        private void LayoutRoot_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var context = sender.ContextFlyout as MenuFlyout;
            if (context != null)
            {
                // TODO: totally WRONG way to do this, find a better solution
                var editItem = context.Items[2];
                if (editItem != null)
                {
                    //var channel = this.ViewModel.With as TLChannel;
                    var message = DataContext as TLMessage;
                    if (message != null && message.FwdFrom == null && message.ViaBotId == null && (message.IsOut /*|| (channel != null && channel.Creator && channel.IsEditor)*/) && (message.Media is ITLMediaCaption || message.Media is TLMessageMediaWebPage || message.Media is TLMessageMediaEmpty))
                    {
                        if (message.IsVoice())
                        {
                            return;
                        }

                        InMemoryCacheService.Current.GetConfigAsync(config =>
                        {
                            var now = TLUtils.DateToUniversalTimeTLInt(MTProtoService.Current.ClientTicksDelta, DateTime.Now);
                            if (config != null && message.Date + config.EditTimeLimit < now)
                            {
                                editItem.Visibility = Visibility.Visible;
                            }
                        });
                    }
                }
            }
        }
    }
}
