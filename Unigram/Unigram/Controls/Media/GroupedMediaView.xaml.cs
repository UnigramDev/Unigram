using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Media
{
    public sealed partial class GroupedMediaView : UserControl
    {
        public TLMessage ViewModel => DataContext as TLMessage;

        public GroupedMediaView()
        {
            InitializeComponent();
        }

        private object UpdateSelf(TLMessage message)
        {
            if (message == null)
            {
                return null;
            }

            var groupMedia = message.Media as TLMessageMediaGroup;
            if (groupMedia == null)
            {
                return null;
            }

            var groupedMessages = groupMedia.Layout;
            var positions = groupedMessages.Positions.ToList();

            var width = 320d;
            var height = 420d;

            var stackPanel = new StackPanel();
            stackPanel.Width = groupedMessages.Width / 800d * width;
            stackPanel.Height = groupedMessages.Height * height;
            stackPanel.Margin = new Thickness(-1);

            if (groupedMessages.Messages.Count < 2 && groupedMessages.Messages.Count > 0)
            {
                LayoutRoot.Children.Add(new ContentControl
                {
                    Content = groupedMessages.Messages[0],
                    ContentTemplateSelector = App.Current.Resources["MediaTemplateSelector"] as DataTemplateSelector,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch
                });
            }

            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];

                var top = 0.0;
                var left = 0.0;

                if (i > 0)
                {
                    var pos = positions[i - 1];
                    // in one row
                    if (pos.Value.MinY == position.Value.MinY)
                    {
                        top = -(height * pos.Value.Height);

                        for (var j = i - 1; j >= 0; j--)
                        {
                            pos = positions[j];
                            if (pos.Value.MinY == position.Value.MinY)
                            {
                                left += pos.Value.Width / 800d * width;
                            }
                        }
                    }
                    // in one column
                    else if (position.Value.SpanSize == groupedMessages.MaxSizeWidth || position.Value.SpanSize == 1000)
                    {
                        left = position.Value.LeftSpanOffset / 800d * width;
                        // find common big message
                        KeyValuePair<TLMessage, GroupedMessagePosition>? leftColumn = null;
                        for (var j = i - 1; j >= 0; j--)
                        {
                            pos = positions[j];
                            if (pos.Value.SiblingHeights != null)
                            {
                                leftColumn = pos;
                                break;
                            }
                            else
                            {
                                top += (height * pos.Value.Height);
                            }
                        }
                        // set top
                        if (leftColumn != null)
                        {
                            top -= (leftColumn.Value.Value.Height * height);
                        }
                        else
                        {
                            top = 0;
                        }
                    }
                }

                var border = GetPhoto(position, groupedMessages, left, top);
                stackPanel.Children.Add(border);
            }

            if (stackPanel.Children.Count > 0)
            {
                LayoutRoot.Children.Clear();
                LayoutRoot.Children.Add(stackPanel);
            }

            return null;
        }

        private FrameworkElement GetPhoto(KeyValuePair<TLMessage, GroupedMessagePosition> position, GroupedMessages groupedMessages, double left, double top)
        {
            var margin = 1;

            var width = 320d;
            var height = 420d;

            var element = new ContentControl
            {
                Content = position.Key,
                ContentTemplateSelector = App.Current.Resources["MediaTemplateSelector"] as DataTemplateSelector,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };

            element.DataContext = position.Key;
            element.Width = position.Value.Width / 800d * width - margin * 2;
            element.Height = height * position.Value.Height - margin * 2;
            element.Margin = new Thickness(left + margin, top + margin, margin, margin);
            element.HorizontalAlignment = HorizontalAlignment.Left;
            element.VerticalAlignment = VerticalAlignment.Top;

            return element;

            var photo = position.Key.GetPhoto();

            var transferBinding = new Binding();
            transferBinding.Path = new PropertyPath("IsTransferring");
            transferBinding.Source = photo;

            var progressBinding = new Binding();
            progressBinding.Path = new PropertyPath("Progress");
            progressBinding.Source = photo;

            var child = new ImageView();
            child.DataContext = position.Key;
            child.Source = (ImageSource)DefaultPhotoConverter.Convert(photo, true);
            child.Click += Click;
            child.Stretch = Stretch.UniformToFill;
            child.HorizontalContentAlignment = HorizontalAlignment.Center;
            child.VerticalContentAlignment = VerticalAlignment.Center;

            var transfer = new TransferButton();
            transfer.DataContext = position.Key;
            transfer.Completed += Completed;
            transfer.Transferable = photo;
            transfer.Style = Application.Current.Resources["MediaTransferButtonStyle"] as Style;
            transfer.SetBinding(TransferButton.IsTransferringProperty, transferBinding);
            transfer.SetBinding(TransferButton.ProgressProperty, progressBinding);

            var grid = new Grid();
            grid.Name = "Media";
            grid.DataContext = position.Key;
            grid.Width = position.Value.Width / 800d * width - margin * 2;
            grid.Height = height * position.Value.Height - margin * 2;
            grid.Margin = new Thickness(left + margin, top + margin, margin, margin);
            grid.HorizontalAlignment = HorizontalAlignment.Left;
            grid.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(child);
            grid.Children.Add(transfer);

            return grid;
        }

        public event RoutedEventHandler Click;
        public event EventHandler<TransferCompletedEventArgs> Completed;
    }
}
