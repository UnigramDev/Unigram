//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class FormulaPopup : ContentPopup
    {
        // TDesktop's samples, one of which is shown while there is no source.
        private static readonly string[] _samples =
        {
            "e^{i\\pi}=-1",
            "x^n+y^n=z^n",
            "\\sin^2\\alpha+\\cos^2\\alpha=1",
            "x_{1,2}=\\frac{-b\\pm\\sqrt{b^2-4ac}}{2a}",
        };

        /// <param name="separateLine">
        /// Whether the formula goes on a line of its own, offered as a choice; null where there is
        /// no choice - a formula being edited, or one going where only text can.
        /// </param>
        public FormulaPopup(string formula = null, bool? separateLine = null)
        {
            InitializeComponent();

            Title = string.IsNullOrEmpty(formula) ? Strings.RichEditorInsertFormula : Strings.RichEditorEditFormula;
            PrimaryButtonText = Strings.Save;
            SecondaryButtonText = Strings.Cancel;

            if (separateLine is bool value)
            {
                SeparateLine.IsChecked = value;
                SeparateLine.Visibility = Visibility.Visible;
            }

            Sample.Source = _samples[(Environment.TickCount & int.MaxValue) % _samples.Length];

            Input.Text = formula ?? string.Empty;
            Input.SelectAll();

            UpdatePreview();
        }

        public string Source => Input.Text.Trim().Replace('\r', ' ').Replace('\n', ' ');

        public bool IsSeparateLine => SeparateLine.IsChecked == true;

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var source = Source;
            var empty = source.Length == 0;

            IsPrimaryButtonEnabled = !empty;

            // A source still being typed is often not a formula yet.
            try
            {
                Result.Source = source;
            }
            catch
            {
            }

            Result.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            Sample.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;

                if (Source.Length > 0)
                {
                    Hide(ContentDialogResult.Primary);
                }
                else
                {
                    VisualUtilities.ShakeView(Input);
                }
            }
        }
    }
}
