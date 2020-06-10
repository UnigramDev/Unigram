using System;
using System.Threading;

namespace Unigram.Services.ViewService
{
    class SecondaryViewSynchronizationContextDecorator : SynchronizationContext
    {
        private readonly ViewLifetimeControl control;
        private readonly SynchronizationContext context;

        public SecondaryViewSynchronizationContextDecorator(ViewLifetimeControl control, SynchronizationContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (control == null)
                throw new ArgumentNullException(nameof(control));
            this.control = control;
            this.context = context;
        }


        public override void OperationStarted()
        {

            try
            {
                var count = control.StartViewInUse();
                Logging.LoggingService.WriteLine("SecondaryViewSynchronizationContextDecorator : OperationStarted: " + count);
                context.OperationStarted();
            }
            catch (ViewLifetimeControl.ViewLifeTimeException)
            {
                //Don't need to do anything, operation can't be started
            }

        }

        public override void Send(SendOrPostCallback d, object state)
        {
            context.Send(d, state);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            context.Post(d, state);
        }

        public override void OperationCompleted()
        {
            try
            {
                context.OperationCompleted();
                var count = control.StopViewInUse();
                Logging.LoggingService.WriteLine("SecondaryViewSynchronizationContextDecorator : OperationCompleted: " + count);
            }
            catch (ViewLifetimeControl.ViewLifeTimeException)
            {
                //Don't need to do anything, operation can't be completed
            }
        }

        public override SynchronizationContext CreateCopy()
        {
            var copyControl = ViewLifetimeControl.GetForCurrentView();
            copyControl = copyControl != control ? copyControl : control;
            return new SecondaryViewSynchronizationContextDecorator(copyControl, context.CreateCopy());
        }
    }
}
