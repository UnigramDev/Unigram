using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class SearchDialogTextBox : SearchTextBox
    {
        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                GetBindingExpression(TextProperty)?.UpdateSource();
                SearchCommand?.Execute(null);
            }

            base.OnKeyDown(e);
        }
    }
}
