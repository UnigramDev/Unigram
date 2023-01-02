//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;
using Unigram.Logs;

namespace Unigram.Services.ViewService
{
    class SecondaryViewSynchronizationContextDecorator : SynchronizationContext
    {
        private readonly ViewLifetimeControl _control;
        private readonly SynchronizationContext _context;

        public SecondaryViewSynchronizationContextDecorator(ViewLifetimeControl control, SynchronizationContext context)
        {
            _control = control ?? throw new ArgumentNullException(nameof(control));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }


        public override void OperationStarted()
        {
            var count = _control.StartViewInUse();
            if (count != -1)
            {
                _context.OperationStarted();
                Logger.Info("SecondaryViewSynchronizationContextDecorator : OperationStarted: " + count);
            }
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            _context.Send(d, state);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            _context.Post(d, state);
        }

        public override void OperationCompleted()
        {
            _context.OperationCompleted();

            var count = _control.StopViewInUse();
            if (count != -1)
            {
                Logger.Info("SecondaryViewSynchronizationContextDecorator : OperationCompleted: " + count);
            }
        }

        public override SynchronizationContext CreateCopy()
        {
            var control = ViewLifetimeControl.GetForCurrentView();
            return new SecondaryViewSynchronizationContextDecorator(control, _context.CreateCopy());
        }
    }
}
