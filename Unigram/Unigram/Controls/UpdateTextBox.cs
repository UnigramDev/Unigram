using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class UpdateTextBox : TextBox
    {
        public UpdateTextBox()
        {
            TextChanged += UpdateTextBox_TextChanged;
        }

        private void UpdateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            GetBindingExpression(TextProperty)?.UpdateSource();
        }
    }
}
