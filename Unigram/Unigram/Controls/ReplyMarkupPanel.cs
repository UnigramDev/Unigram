using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Core.Services;
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
        private double _keyboardHeight = 300;

        #region ReplyMarkup

        public TLReplyMarkupBase ReplyMarkup
        {
            get { return (TLReplyMarkupBase)GetValue(ReplyMarkupProperty); }
            set { SetValue(ReplyMarkupProperty, value); }
        }

        public static readonly DependencyProperty ReplyMarkupProperty =
            DependencyProperty.Register("ReplyMarkup", typeof(TLReplyMarkupBase), typeof(ReplyMarkupPanel), new PropertyMetadata(null, OnReplyMarkupChanged));

        private static void OnReplyMarkupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ReplyMarkupPanel)d).OnReplyMarkupChanged((TLReplyMarkupBase)e.NewValue, (TLReplyMarkupBase)e.OldValue);
        }

        #endregion

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            _keyboardHeight = args.OccludedRect.Height;
        }

        private void UpdateSize()
        {
            var inline = DataContext is TLMessage;
            if (ReplyMarkup is TLReplyKeyboardMarkup && !inline && Parent is ScrollViewer scroll)
            {
                var keyboard = ReplyMarkup as TLReplyKeyboardMarkup;
                if (keyboard.IsResize && double.IsNaN(Height))
                {
                    Height = double.NaN;
                    scroll.MaxHeight = _keyboardHeight;
                }
                else if (keyboard.IsResize == false && double.IsNaN(Height) && Parent is ScrollViewer scroll1)
                {
                    Height = _keyboardHeight;
                    scroll1.MaxHeight = double.PositiveInfinity;
                }
            }
            else if (ReplyMarkup is TLReplyKeyboardHide && !inline && Parent is ScrollViewer scroll2)
            {
                Height = double.NaN;
                scroll2.MaxHeight = double.PositiveInfinity;
            }
        }

        private void OnReplyMarkupChanged(TLReplyMarkupBase newValue, TLReplyMarkupBase oldValue)
        {
            var inline = DataContext is TLMessage;
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

            //if (receipt)
            //{
            //    var panel = new Grid();
            //    panel.HorizontalAlignment = HorizontalAlignment.Stretch;
            //    panel.VerticalAlignment = VerticalAlignment.Stretch;
            //    panel.Margin = new Thickness(-2, 0, -2, 0);

            //    var button = new GlyphButton();
            //    button.DataContext = new TLKeyboardButtonBuy();
            //    button.Content = "Receipt";
            //    button.Margin = new Thickness(2, 2, 2, 0);
            //    button.HorizontalAlignment = HorizontalAlignment.Stretch;
            //    button.VerticalAlignment = VerticalAlignment.Stretch;
            //    button.Click += Button_Click;
            //    button.Style = App.Current.Resources["ReplyInlineMarkupButtonStyle"] as Style;

            //    panel.Children.Add(button);
            //    Children.Add(panel);

            //    return;
            //}

            if (rows != null && ((inline && newValue is TLReplyInlineMarkup) || (!inline && newValue is TLReplyKeyboardMarkup)))
            {
                for (int j = 0; j < rows.Count; j++)
                {
                    var row = rows[j];

                    var panel = new Grid();
                    panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    panel.VerticalAlignment = VerticalAlignment.Stretch;
                    panel.Margin = new Thickness(-2, 0, -2, 0);

                    for (int i = 0; i < row.Buttons.Count; i++)
                    {
                        var button = new GlyphButton();
                        button.DataContext = row.Buttons[i];
                        button.Content = row.Buttons[i].Text;
                        button.Margin = new Thickness(2, 2, 2, j == rows.Count - 1 ? 0 : 2);
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
                            button.Content = "Receipt";
                        }

                        Grid.SetColumn(button, i);

                        panel.ColumnDefinitions.Add(new ColumnDefinition());
                        panel.Children.Add(button);
                    }

                    Grid.SetRow(panel, j);

                    RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, resize ? GridUnitType.Auto : GridUnitType.Star) });
                    Children.Add(panel);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var btn = button.DataContext as TLKeyboardButtonBase;
            if (btn != null)
            {
                ButtonClick?.Invoke(this, new ReplyMarkupButtonClickEventArgs(btn));
            }
        }

        public event EventHandler<ReplyMarkupButtonClickEventArgs> ButtonClick;
    }
}
