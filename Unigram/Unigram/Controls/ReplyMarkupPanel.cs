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

        #region IsInline

        public bool IsInline
        {
            get { return (bool)GetValue(IsInlineProperty); }
            set { SetValue(IsInlineProperty, value); }
        }

        public static readonly DependencyProperty IsInlineProperty =
            DependencyProperty.Register("IsInline", typeof(bool), typeof(ReplyMarkupPanel), new PropertyMetadata(true, OnIsInlineChanged));

        private static void OnIsInlineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ReplyMarkupPanel)d).OnIsInlineChanged((bool)e.NewValue, (bool)e.OldValue);
        }

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

        private void OnIsInlineChanged(bool newValue, bool oldValue)
        {
            //if (newValue)
            //{
            //    InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            //}
            //else
            //{
            //    InputPane.GetForCurrentView().Showing += InputPane_Showing;
            //}
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            _keyboardHeight = args.OccludedRect.Height;
        }

        private void UpdateSize()
        {
            if (ReplyMarkup is TLReplyKeyboardMarkup && !IsInline)
            {
                var keyboard = ReplyMarkup as TLReplyKeyboardMarkup;
                if (keyboard.IsResize && double.IsNaN(Height))
                {
                    Height = double.NaN;
                    ((ScrollViewer)Parent).MaxHeight = _keyboardHeight;
                }
                else if (keyboard.IsResize == false && double.IsNaN(Height))
                {
                    Height = _keyboardHeight;
                    ((ScrollViewer)Parent).MaxHeight = double.PositiveInfinity;
                }
            }
        }

        private void OnReplyMarkupChanged(TLReplyMarkupBase newValue, TLReplyMarkupBase oldValue)
        {
            bool resize = false;
            TLVector<TLKeyboardButtonRow> rows = null;
            if (newValue is TLReplyKeyboardMarkup && !IsInline)
            {
                rows = ((TLReplyKeyboardMarkup)newValue).Rows;
                resize = ((TLReplyKeyboardMarkup)newValue).IsResize;
            }

            if (newValue is TLReplyInlineMarkup && IsInline)
            {
                rows = ((TLReplyInlineMarkup)newValue).Rows;
                
                //if (!double.IsNaN(Height))
                //{
                //    Height = double.NaN;
                //}
            }

            UpdateSize();
            Children.Clear();
            RowDefinitions.Clear();

            if (rows != null && ((IsInline && newValue is TLReplyInlineMarkup) || (!IsInline && newValue is TLReplyKeyboardMarkup)))
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
                        button.Margin = new Thickness(2);
                        button.HorizontalAlignment = HorizontalAlignment.Stretch;
                        button.VerticalAlignment = VerticalAlignment.Stretch;
                        button.Click += Button_Click;

                        if (IsInline)
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
                            button.Glyph = "\uE248";
                        }
                        else if (row.Buttons[i] is TLKeyboardButton && IsInline)
                        {
                            button.Glyph = "\uE15F";
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
