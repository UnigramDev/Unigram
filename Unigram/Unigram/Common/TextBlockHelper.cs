using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Unigram.Common
{
    public static class TextBlockHelper
    {
        #region SentCodeType

        public static TLAuthSentCodeTypeBase GetSentCodeType(DependencyObject obj)
        {
            return (TLAuthSentCodeTypeBase)obj.GetValue(SentCodeTypeProperty);
        }

        public static void SetSentCodeType(DependencyObject obj, TLAuthSentCodeTypeBase value)
        {
            obj.SetValue(SentCodeTypeProperty, value);
        }

        public static readonly DependencyProperty SentCodeTypeProperty =
            DependencyProperty.RegisterAttached("SentCodeType", typeof(TLAuthSentCodeTypeBase), typeof(TextBlockHelper), new PropertyMetadata(null, OnSentCodeTypeChanged));

        private static void OnSentCodeTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as TextBlock;
            var type = e.NewValue as TLAuthSentCodeTypeBase;

            sender.Inlines.Clear();

            switch (type)
            {
                case TLAuthSentCodeTypeApp appType:
                    sender.Inlines.Add(new Run { Text = "We've sent the code the the " });
                    sender.Inlines.Add(new Run { Text = "Telegram", FontWeight = FontWeights.SemiBold });
                    sender.Inlines.Add(new Run { Text = " app on your other device." });
                    break;
                case TLAuthSentCodeTypeSms smsType:
                    sender.Inlines.Add(new Run { Text = "We've sent you an SMS with the code." });
                    break;
            }
        }

        #endregion
    }
}
