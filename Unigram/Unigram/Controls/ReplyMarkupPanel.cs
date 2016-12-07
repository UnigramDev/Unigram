using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
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

    public class ReplyMarkupPanel : StackPanel
    {
        #region IsInline

        public bool IsInline
        {
            get { return (bool)GetValue(IsInlineProperty); }
            set { SetValue(IsInlineProperty, value); }
        }

        public static readonly DependencyProperty IsInlineProperty =
            DependencyProperty.Register("IsInline", typeof(bool), typeof(ReplyMarkupPanel), new PropertyMetadata(true));

        #endregion


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

        private void OnReplyMarkupChanged(TLReplyMarkupBase newValue, TLReplyMarkupBase oldValue)
        {
            TLVector<TLKeyboardButtonRow> rows = null;
            if (newValue is TLReplyKeyboardMarkup && !IsInline)
            {
                rows = ((TLReplyKeyboardMarkup)newValue).Rows;

                //if (!double.IsNaN(Height) && ((TLReplyKeyboardMarkup)newValue).IsResize)
                //{
                //    Height = double.NaN;
                //}
                //else if (double.IsNaN(Height) && !((TLReplyKeyboardMarkup)newValue).IsResize && rows.Count > 0)
                //{
                //    Height = 320; // TODO: last known keyboard height
                //}
            }

            if (newValue is TLReplyInlineMarkup && IsInline)
            {
                rows = ((TLReplyInlineMarkup)newValue).Rows;
                
                //if (!double.IsNaN(Height))
                //{
                //    Height = double.NaN;
                //}
            }

            Children.Clear();

            if (rows != null && ((IsInline && newValue is TLReplyInlineMarkup) || (!IsInline && newValue is TLReplyKeyboardMarkup)))
            {
                foreach (var row in rows)
                {
                    var panel = new Grid();
                    panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    panel.VerticalAlignment = VerticalAlignment.Stretch;
                    panel.Margin = new Thickness(-4, 0, -4, 0);

                    for (int i = 0; i < row.Buttons.Count; i++)
                    {
                        var button = new GlyphButton();
                        button.DataContext = row.Buttons[i];
                        button.Content = row.Buttons[i].Text;
                        button.Margin = new Thickness(4);
                        button.HorizontalAlignment = HorizontalAlignment.Stretch;
                        button.VerticalAlignment = VerticalAlignment.Stretch;
                        button.Click += Button_Click;

                        //if (IsInline)
                        {
                            button.Style = App.Current.Resources["ReplyInlineMarkupButtonStyle"] as Style;
                        }

                        if (row.Buttons[i] is TLKeyboardButtonUrl)
                        {
                            button.Glyph = "\uE12B";
                        }
                        else if (row.Buttons[i] is TLKeyboardButtonSwitchInline)
                        {
                            button.Glyph = "\uE248";
                        }

                        Grid.SetColumn(button, i);

                        panel.ColumnDefinitions.Add(new ColumnDefinition());
                        panel.Children.Add(button);
                    }

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
