namespace Unigram.Client.Views
{
    using Windows.UI.Xaml.Media.Animation;
    using Windows.UI.Xaml.Navigation;

    public sealed partial class About
    {
        public About()
        {
            this.InitializeComponent();
        }

        // Stuff to make the back-button interaction possible
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var backStack = Frame.BackStack;
            var backStackCount = backStack.Count;

            if (backStackCount > 0)
            {
                var masterPageEntry = backStack[backStackCount - 1];
                backStack.RemoveAt(backStackCount - 1);

                // #TODO Restore the previous opened note when going back
                try
                {
                    var modifiedEntry = new PageStackEntry(masterPageEntry.SourcePageType, null, masterPageEntry.NavigationTransitionInfo);
                    backStack.Add(modifiedEntry);
                }
                catch // If stuff goes to the shitter, go back to Home
                {
                    Frame.Navigate(typeof(Home), new DrillInNavigationTransitionInfo());
                }
            }
        }
    }
}
