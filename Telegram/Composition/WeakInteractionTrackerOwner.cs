//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Foundation;
using Windows.UI.Composition.Interactions;

namespace Telegram.Composition
{
    public class WeakInteractionTrackerOwner : IInteractionTrackerOwner
    {
        public event TypedEventHandler<InteractionTracker, InteractionTrackerIdleStateEnteredArgs> IdleStateEntered;
        public event TypedEventHandler<InteractionTracker, InteractionTrackerInertiaStateEnteredArgs> InertiaStateEntered;
        public event TypedEventHandler<InteractionTracker, InteractionTrackerInteractingStateEnteredArgs> InteractingStateEntered;
        public event TypedEventHandler<InteractionTracker, InteractionTrackerValuesChangedArgs> ValuesChanged;
        public event TypedEventHandler<InteractionTracker, InteractionTrackerCustomAnimationStateEnteredArgs> CustomAnimationStateEntered;

        void IInteractionTrackerOwner.IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            IdleStateEntered?.Invoke(sender, args);
        }

        void IInteractionTrackerOwner.InertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            InertiaStateEntered?.Invoke(sender, args);
        }

        void IInteractionTrackerOwner.InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            InteractingStateEntered?.Invoke(sender, args);
        }

        void IInteractionTrackerOwner.ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            ValuesChanged?.Invoke(sender, args);
        }

        void IInteractionTrackerOwner.CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
        {
            CustomAnimationStateEntered?.Invoke(sender, args);
        }

        public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args)
        {
        }
    }
}
