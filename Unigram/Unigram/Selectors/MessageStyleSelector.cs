//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Unigram.Common;
using Unigram.ViewModels;

namespace Unigram.Selectors
{
    public class MessageStyleSelector : StyleSelector
    {
        public Style ExpandedStyle { get; set; }
        public Style MessageStyle { get; set; }
        public Style ServiceStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if (item is MessageViewModel message && message.IsService())
            {
                return ServiceStyle;
            }

            // Windows 11 Multiple selection mode looks nice
            if (ApiInfo.IsWindows11)
            {
                return MessageStyle;
            }

            // Legacy expanded style for Windows 10
            return ExpandedStyle;
        }
    }
}
