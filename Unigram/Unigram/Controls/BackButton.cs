using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Common;

namespace Unigram.Controls
{
    public class BackButton : GlyphButton
    {
        public BackButton()
        {
            DefaultStyleKey = typeof(BackButton);
            Click += OnClick;
        }

        private void OnClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            BootStrapper.Current.RaiseBackRequested();
        }
    }
}
