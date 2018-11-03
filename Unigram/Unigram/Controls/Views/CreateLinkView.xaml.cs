using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Unigram.Common;
using Unigram.Services;
using Telegram.Td.Api;

namespace Unigram.Controls.Views
{
    public sealed partial class CreateLinkView : ContentDialog
    {
        private readonly IProtoService _protoService;

        public CreateLinkView(IProtoService protoService)
        {
            InitializeComponent();

            _protoService = protoService;

            Title = Strings.Resources.CreateLink;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public string Text
        {
            get
            {
                return TextField.Text;
            }
            set
            {
                TextField.Text = value;
            }
        }

        public string Link
        {
            get
            {
                return LinkField.Text;
            }
            set
            {
                LinkField.Text = value;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(Text))
            {
                VisualUtilities.ShakeView(TextField);
                args.Cancel = true;
                return;
            }

            if (IsUrlInvalid(Link))
            {
                VisualUtilities.ShakeView(LinkField);
                args.Cancel = true;
            }
        }

        private bool IsUrlInvalid(string url)
        {
            if (_protoService != null)
            {
                var response = _protoService.Execute(new GetTextEntities(url));
                if (response is TextEntities entities)
                {
                    return !(entities.Entities.Count == 1 && entities.Entities[0].Offset == 0 && entities.Entities[0].Length == url.Length && entities.Entities[0].Type is TextEntityTypeUrl);
                }
            }
            else
            {
                return !Uri.TryCreate(url, UriKind.Absolute, out Uri result);
            }

            return true;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
