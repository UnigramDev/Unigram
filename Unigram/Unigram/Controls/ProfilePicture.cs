using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class ProfilePicture : HyperlinkButton
    {
        public ProfilePicture()
        {
            DefaultStyleKey = typeof(ProfilePicture);
        }

        #region Source

        public ImageSource Source
        {
            get { return (ImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(ImageSource), typeof(ProfilePicture), new PropertyMetadata(null));

        #endregion
    }
}
