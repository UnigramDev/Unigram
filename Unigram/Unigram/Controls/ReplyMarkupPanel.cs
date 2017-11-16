using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Core.Services;
using Unigram.Views;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class ReplyMarkupButtonClickEventArgs : EventArgs
    {
        public ReplyMarkupButtonClickEventArgs(TLKeyboardButtonBase button)
        {
            Button = button;
        }

        public TLKeyboardButtonBase Button { get; private set; }
    }

    public class ReplyMarkupPanel : Grid
    {
        private double _keyboardHeight = 260;

        //#region ReplyMarkup

        //public TLReplyMarkupBase ReplyMarkup
        //{
        //    get { return (TLReplyMarkupBase)GetValue(ReplyMarkupProperty); }
        //    set { SetValue(ReplyMarkupProperty, value); }
        //}

        //public static readonly DependencyProperty ReplyMarkupProperty =
        //    DependencyProperty.Register("ReplyMarkup", typeof(TLReplyMarkupBase), typeof(ReplyMarkupPanel), new PropertyMetadata(null, OnReplyMarkupChanged));

        //private static void OnReplyMarkupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    ((ReplyMarkupPanel)d).OnReplyMarkupChanged((TLReplyMarkupBase)e.NewValue, (TLReplyMarkupBase)e.OldValue);
        //}

        //#endregion

        private TLMessage _message;
        public TLMessage Message
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
                OnReplyMarkupChanged(_replyMarkup, _replyMarkup);
            }
        }

        private TLReplyMarkupBase _replyMarkup;
        public TLReplyMarkupBase ReplyMarkup
        {
            get
            {
                return _replyMarkup;
            }
            set
            {
                _replyMarkup = value;
                OnReplyMarkupChanged(value, value);
            }
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            _keyboardHeight = args.OccludedRect.Height;
        }

        private void UpdateSize()
        {
            var inline = Message is TLMessage;
            if (ReplyMarkup is TLReplyKeyboardMarkup && !inline && Parent is ScrollViewer scroll)
            {
                var keyboard = ReplyMarkup as TLReplyKeyboardMarkup;
                if (keyboard.IsResize)
                {
                    scroll.Height = double.NaN;
                    scroll.MaxHeight = _keyboardHeight;
                }
                else
                {
                    scroll.Height = _keyboardHeight;
                    scroll.MaxHeight = _keyboardHeight;
                }
            }
            else if (ReplyMarkup is TLReplyKeyboardHide && !inline && Parent is ScrollViewer scroll2)
            {
                scroll2.Height = 0;
                scroll2.MaxHeight = _keyboardHeight;
            }
        }

        private void OnReplyMarkupChanged(TLReplyMarkupBase newValue, TLReplyMarkupBase oldValue)
        {
            var inline = Message is TLMessage;
            var resize = false;

            TLVector<TLKeyboardButtonRow> rows = null;
            if (newValue is TLReplyKeyboardMarkup keyboardMarkup && !inline)
            {
                rows = keyboardMarkup.Rows;
                resize = keyboardMarkup.IsResize;
            }
            else if (newValue is TLReplyInlineMarkup inlineMarkup && inline)
            {
                rows = inlineMarkup.Rows;

                //if (!double.IsNaN(Height))
                //{
                //    Height = double.NaN;
                //}
            }

            UpdateSize();
            Children.Clear();
            RowDefinitions.Clear();

            var receipt = false;
            if (DataContext is TLMessage message && message.Media is TLMessageMediaInvoice invoiceMedia)
            {
                receipt = invoiceMedia.HasReceiptMsgId;
            }

            if (rows != null && ((inline && newValue is TLReplyInlineMarkup) || (!inline && newValue is TLReplyKeyboardMarkup)))
            {
                for (int j = 0; j < rows.Count; j++)
                {
                    var row = rows[j];

                    var panel = new Grid();
                    panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    panel.VerticalAlignment = VerticalAlignment.Stretch;
                    panel.Margin = new Thickness(-1, 0, -1, 0);

                    for (int i = 0; i < row.Buttons.Count; i++)
                    {
                        var button = new GlyphButton();
                        button.DataContext = row.Buttons[i];
                        button.Content = row.Buttons[i].Text;
                        button.Margin = new Thickness(1, 2, 1, 0);
                        button.HorizontalAlignment = HorizontalAlignment.Stretch;
                        button.VerticalAlignment = VerticalAlignment.Stretch;
                        button.Click += Button_Click;

                        if (inline)
                        {
                            button.Style = App.Current.Resources["ReplyInlineMarkupButtonStyle"] as Style;
                        }
                        else
                        {
                            button.Style = App.Current.Resources["ReplyKeyboardMarkupButtonStyle"] as Style;
                        }

                        if (row.Buttons[i] is TLKeyboardButtonUrl)
                        {
                            button.Glyph = "\uE143";
                        }
                        else if (row.Buttons[i] is TLKeyboardButtonSwitchInline)
                        {
                            button.Glyph = "\uEE35";
                        }
                        else if (row.Buttons[i] is TLKeyboardButton && inline)
                        {
                            button.Glyph = "\uE15F";
                        }
                        else if (row.Buttons[i] is TLKeyboardButtonBuy && receipt)
                        {
                            button.Content = Strings.Android.PaymentReceipt;
                        }

                        SetColumn(button, i);

                        panel.ColumnDefinitions.Add(new ColumnDefinition());
                        panel.Children.Add(button);
                    }

                    SetRow(panel, j);

                    RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, resize ? GridUnitType.Auto : GridUnitType.Star) });
                    Children.Add(panel);
                }

                if (Children.Count > 0 && !inline)
                {
                    var page = this.Ancestors<DialogPage>().FirstOrDefault() as DialogPage;
                    if (page != null)
                    {
                        page.ShowMarkup();
                    }

                    Padding = new Thickness(0, 0, 0, 4);
                }
                else if (!inline)
                {
                    Padding = new Thickness();
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.DataContext is TLKeyboardButtonBase btn)
            {
                ButtonClick?.Invoke(this, new ReplyMarkupButtonClickEventArgs(btn));
            }
        }

        public event EventHandler<ReplyMarkupButtonClickEventArgs> ButtonClick;
    }
}
