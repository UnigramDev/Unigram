using System;
using System.Numerics;
using Unigram.ViewModels;
using Windows.ApplicationModel;
using Windows.UI.Composition;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class AboutPage : Page
    {
        int numClicks = 0;
        public AboutViewModel ViewModel => DataContext as AboutViewModel;

        public AboutPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<AboutViewModel>();
        }
        
        private void btnLogo_Click(object sender, RoutedEventArgs e)
        {
            numClicks++;
            if (numClicks == 10)
            {
                flyEgg.ShowAt(sender as FrameworkElement);
                txtEgg.Text = "What are you doing?";
            }
            else if (numClicks == 15)
            {
                flyEgg.ShowAt(sender as FrameworkElement);
                txtEgg.Text = "No no no, please STOP";
            }
            else if (numClicks == 18)
            {
                flyEgg.ShowAt(sender as FrameworkElement);
                txtEgg.Text = "Please, STOP NOW!!!.";
            }
            else if (numClicks == 20)
            {
                DropRocketAnimation();
            }
        }

        private void DropRocketAnimation()
        {
            unigramLogo.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 1);

            TransformGroup group = new TransformGroup();
            group.Children.Add(new RotateTransform());
            group.Children.Add(new TranslateTransform());
            unigramLogo.RenderTransform = group;
            
            Storyboard story = new Storyboard();
            DoubleAnimationUsingKeyFrames anim1 = new DoubleAnimationUsingKeyFrames();

            var key1 = new EasingDoubleKeyFrame //Rotation
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500)),
                Value = 50
            };

            var key2 = new EasingDoubleKeyFrame //Stable
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(550)),
                Value = 50
            };

            var key3 = new EasingDoubleKeyFrame //Rotation1
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3000)),
                Value = 135
            };

            anim1.KeyFrames.Add(key1);
            anim1.KeyFrames.Add(key2);
            anim1.KeyFrames.Add(key3);

            DoubleAnimation anim2 = new DoubleAnimation
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2000)),
                BeginTime = TimeSpan.FromSeconds(1.5),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
                To = 2000
            };

            Storyboard.SetTargetProperty(anim1, "(UIElement.RenderTransform).(TransformGroup.Children)[0].(RotateTransform.Angle)");
            Storyboard.SetTarget(anim1, unigramLogo);
            Storyboard.SetTargetProperty(anim2, "(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)");
            Storyboard.SetTarget(anim2, unigramLogo);

            story.Children.Add(anim1);
            story.Children.Add(anim2);

            story.Begin();
        }
    }
}
