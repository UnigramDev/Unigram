using System;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Cells.Premium
{
    public sealed partial class PremiumFeatureUniqueReactionsCell : UserControl
    {
        public PremiumFeatureUniqueReactionsCell()
        {
            InitializeComponent();
        }

        public void UpdateFeature(IProtoService protoService)
        {
            var reactions = protoService.Reactions.Values.Where(x => x.IsPremium).ToList();

            var cols = 4;
            var rows = (int)Math.Ceiling((double)reactions.Count / cols);

            Presenter.ColumnDefinitions.Clear();
            Presenter.RowDefinitions.Clear();

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    var i = x + y * cols;

                    var button = Presenter.Children[i] as HyperlinkButton;
                    if (button == null)
                    {
                        if (i < reactions.Count)
                        {
                            var item = reactions[i];

                            var view2 = new LottieView();
                            view2.AutoPlay = false;
                            view2.IsLoopingEnabled = false;
                            view2.FrameSize = new Size(64, 64);
                            view2.DecodeFrameType = DecodePixelType.Logical;
                            view2.Width = 64;
                            view2.Height = 64;

                            protoService.DownloadFile(item.AroundAnimation.StickerValue.Id, 32);

                            var file = item.CenterAnimation.StickerValue;
                            if (file.Local.IsDownloadingCompleted)
                            {
                                view2.Source = UriEx.ToLocal(file.Local.Path);
                            }
                            else
                            {
                                view2.Source = null;

                                UpdateManager.Subscribe(view2, protoService, file, /*UpdateReaction*/UpdateFile, true);

                                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                                {
                                    protoService.DownloadFile(file.Id, 32);
                                }
                            }

                            button = new HyperlinkButton
                            {
                                Tag = reactions[i],
                                Content = view2,
                                Style = BootStrapper.Current.Resources["EmptyHyperlinkButtonStyle"] as Style
                            };

                            button.Click += Reaction_Click;
                            Presenter.Children.Add(button);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    Grid.SetColumn(button, x);
                    Grid.SetRow(button, y);

                    if (y == 0)
                    {
                        Presenter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                    }
                }

                Presenter.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            }
        }

        private void UpdateFile(object target, File file)
        {
            if (target is LottieView lottie)
            {
                lottie.Source = UriEx.ToLocal(file.Local.Path);
            }
        }

        private void Reaction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton button && button.Tag is Reaction reaction && button.Content is LottieView centerView)
            {
                var center = reaction.CenterAnimation.StickerValue;
                var around = reaction.AroundAnimation.StickerValue;

                if (center.Local.IsDownloadingCompleted && around.Local.IsDownloadingCompleted)
                {
                    var transform = button.TransformToVisual(this);
                    var point = transform.TransformPoint(new Windows.Foundation.Point());

                    var dispatcher = DispatcherQueue.GetForCurrentThread();

                    var aroundView = new LottieView();
                    aroundView.Width = 64 * 3;
                    aroundView.Height = 64 * 3;
                    aroundView.IsLoopingEnabled = false;
                    aroundView.FrameSize = new Size(64 * 3, 64 * 3);
                    aroundView.DecodeFrameType = DecodePixelType.Logical;
                    aroundView.Source = UriEx.ToLocal(around.Local.Path);
                    aroundView.Margin = new Thickness(point.X - 64, point.Y - 64, 0, 0);
                    aroundView.HorizontalAlignment = HorizontalAlignment.Left;
                    aroundView.VerticalAlignment = VerticalAlignment.Top;
                    aroundView.PositionChanged += (s, args) =>
                    {
                        if (args == 1)
                        {
                            dispatcher.TryEnqueue(() => Overlay.Children.Remove(aroundView));
                        }
                    };

                    centerView.Play();
                    Overlay.Children.Add(aroundView);
                }
            }
        }
    }
}
