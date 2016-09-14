using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public class MessageControlBase : UserControl
    {
        public TLMessage ViewModel => DataContext as TLMessage;

        public MessageControlBase()
        {
            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null)
                {
                    Loading(s, null);
                }
            };
        }

        #region Convert methods
        protected string ConvertDate(int value)
        {
            var clientDelta = MTProtoService.Instance.ClientTicksDelta;
            var utc0SecsLong = value * 4294967296 - clientDelta;
            var utc0SecsInt = utc0SecsLong / 4294967296.0;
            var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

            var cultureInfo = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
            var shortTimePattern = Utils.GetShortTimePattern(ref cultureInfo);

            return dateTime.ToString(string.Format("{0}", shortTimePattern), cultureInfo);
        }

        protected string ConvertState(TLMessageState value)
        {
            switch (value)
            {
                case TLMessageState.Sending:
                    return "\uE600";
                case TLMessageState.Confirmed:
                    return "\uE601";
                case TLMessageState.Read:
                    return "\uE602";
                default:
                    return "\uFFFD";
            }
        }

        protected SolidColorBrush ConvertBubble(int? value)
        {
            switch (Utils.GetColorIndex(value ?? 0))
            {
                case 0:
                    return Application.Current.Resources["RedBrush"] as SolidColorBrush;
                case 1:
                    return Application.Current.Resources["GreenBrush"] as SolidColorBrush;
                case 2:
                    return Application.Current.Resources["YellowBrush"] as SolidColorBrush;
                case 3:
                    return Application.Current.Resources["BlueBrush"] as SolidColorBrush;
                case 4:
                    return Application.Current.Resources["PurpleBrush"] as SolidColorBrush;
                case 5:
                    return Application.Current.Resources["PinkBrush"] as SolidColorBrush;
                case 6:
                    return Application.Current.Resources["CyanBrush"] as SolidColorBrush;
                case 7:
                    return Application.Current.Resources["OrangeBrush"] as SolidColorBrush;
                default:
                    return Application.Current.Resources["ListViewItemPlaceholderBackgroundThemeBrush"] as SolidColorBrush;
            }
        }
        #endregion

        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;

        #region Static
        protected static SolidColorBrush StatusDarkBackgroundBrush = new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00));
        protected static SolidColorBrush StatusDarkForegroundBrush = new SolidColorBrush(Colors.White);
        protected static SolidColorBrush StatusLightForegroundBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0xbc, 0x54));

        protected static Dictionary<Type, Func<TLObject, bool>> FullMedia = new Dictionary<Type, Func<TLObject, bool>>();
        protected static Dictionary<Type, Func<TLObject, bool>> InlineMedia = new Dictionary<Type, Func<TLObject, bool>>();

        static MessageControlBase()
        {
            #region FullMedia
            FullMedia.Add(typeof(TLMessageMediaGeo), (media) => true);
            FullMedia.Add(typeof(TLMessageMediaPhoto), (media) => string.IsNullOrEmpty((media as TLMessageMediaPhoto).Caption));
            //FullMedia.Add(typeof(TLMessageMediaVideo), (media) => string.IsNullOrEmpty((media as TLMessageMediaVideo).Caption));
            #endregion

            #region InlineMedia
            InlineMedia.Add(typeof(TLMessageMediaDocument), (media) => true);
            //InlineMedia.Add(typeof(TLMessageMediaAudio), (media) => true);
            InlineMedia.Add(typeof(TLMessageMediaContact), (media) => true);
            InlineMedia.Add(typeof(TLMessageMediaVenue), (media) => true);
            InlineMedia.Add(typeof(TLMessageMediaPhoto), (media) => !string.IsNullOrEmpty((media as TLMessageMediaPhoto).Caption));
            //InlineMedia.Add(typeof(TLMessageMediaVideo), (media) => !string.IsNullOrEmpty((media as TLMessageMediaVideo).Caption));
            //InlineMedia.Add(typeof(TLMessageMediaWebPage), (media) =>
            //{
            //    var webpageMedia = media as TLMessageMediaWebPage;
            //    return (webpageMedia.Photo == null && webpageMedia.WebPage.DescriptionVisibility == Windows.UI.Xaml.Visibility.Visible);
            //});
            #endregion
        }
        #endregion
    }
}
