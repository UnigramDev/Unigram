using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    internal class IdentityTextBlock : Control
    {
        private TextBlock NameLabel;
        private ContentPresenter IconPresenter;

        public IdentityTextBlock()
        {
            DefaultStyleKey = typeof(IdentityTextBlock);
        }

        protected override void OnApplyTemplate()
        {
            NameLabel = GetTemplateChild(nameof(NameLabel)) as TextBlock;
            IconPresenter = GetTemplateChild(nameof(IconPresenter)) as ContentPresenter;
        }

        public void SetUser(IProtoService protoService, User user)
        {
            var verified = user.IsVerified;
            var premium = user.IsPremium && protoService.IsPremiumAvailable;

            NameLabel.Text = user.GetFullName();

            if (premium || verified)
            {
                SetGlyph(premium ? Icons.Premium16 : Icons.Verified16);
                IconPresenter.Visibility = Visibility.Visible;
            }
            else
            {
                IconPresenter.Visibility = Visibility.Collapsed;
            }
        }

        public void SetMessageSender(IProtoService protoService, User user)
        {
            var verified = user.IsVerified;
            var premium = user.IsPremium && protoService.IsPremiumAvailable;

            NameLabel.Text = user.GetFullName();

            if (premium || verified)
            {
                SetGlyph(premium ? Icons.Premium16 : Icons.Verified16);
                IconPresenter.Visibility = Visibility.Visible;
            }
            else
            {
                IconPresenter.Visibility = Visibility.Collapsed;
            }
        }

        private void SetGlyph(string value)
        {
            if (IconPresenter.Content is FontIcon fontIcon)
            {
                fontIcon.Glyph = value;
            }
            else
            {
                IconPresenter.Content = new FontIcon
                {
                    Glyph = value,
                    //FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily,
                    //FontSize = 16,
                    //Margin = new Thickness(4, 0, 0, 2),
                    //VerticalAlignment = VerticalAlignment.Bottom
                };
            }
        }
    }
}
