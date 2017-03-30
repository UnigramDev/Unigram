using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public class PhoneCallMessageBubbleBase : StackPanel
    {
        public TLMessageService ViewModel => DataContext as TLMessageService;

        public BindConvert Convert => BindConvert.Current;

        private DialogViewModel _context;
        public DialogViewModel Context
        {
            get
            {
                if (_context == null)
                {
                    var parent = VisualTreeHelper.GetParent(this);
                    while (parent as BubbleListViewItem == null)
                    {
                        parent = VisualTreeHelper.GetParent(parent);
                    }

                    var item = parent as BubbleListViewItem;
                    if (item != null)
                    {
                        _context = item.Owner.DataContext as DialogViewModel;
                    }
                }

                return _context;
            }
        }

        protected TLMessageService _oldValue;
        protected UserControl _layoutRoot;

        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;

        #region Binding

        protected string ConvertReason(TLMessageService message)
        {
            if (message.Action is TLMessageActionPhoneCall phoneCallAction)
            {
                var outgoing = message.IsOut;
                var missed = phoneCallAction.Reason is TLPhoneCallDiscardReasonMissed || phoneCallAction.Reason is TLPhoneCallDiscardReasonBusy;

                VisualStateManager.GoToState(_layoutRoot, missed ? "Missed" : "Default", false);

                return outgoing ? "\uE60B\u00A0" : "\uE60C\u00A0";
            }

            return string.Empty;
        }

        #endregion

        protected void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var tooltip = sender as ToolTip;
            if (tooltip != null && ViewModel != null)
            {
                var date = Convert.DateTime(ViewModel.Date);
                var text = $"{Convert.LongDate.Format(date)} {Convert.LongTime.Format(date)}";

                tooltip.Content = text;
            }
        }
    }
}
