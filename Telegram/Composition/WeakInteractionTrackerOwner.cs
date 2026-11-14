//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.UI.Composition.Interactions;

namespace Telegram.Composition
{
    public partial class WeakInteractionTrackerOwner : IInteractionTrackerOwner
    {
        public event EventHandler<InteractionTracker, InteractionTrackerIdleStateEnteredArgs> IdleStateEntered;
        public event EventHandler<InteractionTracker, InteractionTrackerInertiaStateEnteredArgs> InertiaStateEntered;
        public event EventHandler<InteractionTracker, InteractionTrackerInteractingStateEnteredArgs> InteractingStateEntered;
        public event EventHandler<InteractionTracker, InteractionTrackerValuesChangedArgs> ValuesChanged;
        public event EventHandler<InteractionTracker, InteractionTrackerCustomAnimationStateEnteredArgs> CustomAnimationStateEntered;

        void IInteractionTrackerOwner.IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            try
            {
                IdleStateEntered?.Invoke(sender, args);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        void IInteractionTrackerOwner.InertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            try
            {
                InertiaStateEntered?.Invoke(sender, args);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        void IInteractionTrackerOwner.InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            try
            {
                InteractingStateEntered?.Invoke(sender, args);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        void IInteractionTrackerOwner.ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            try
            {
                ValuesChanged?.Invoke(sender, args);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        void IInteractionTrackerOwner.CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
        {
            try
            {
                CustomAnimationStateEntered?.Invoke(sender, args);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args)
        {
        }
    }
}
