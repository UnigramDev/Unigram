using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Navigation
{
    public sealed class DeferralManager
    {
        int _count = 0;
        TaskCompletionSource<object> _completed = new TaskCompletionSource<object>();
        public Deferral GetDeferral()
        {
            System.Threading.Interlocked.Increment(ref _count);
            return new Deferral(() =>
            {
                var count = System.Threading.Interlocked.Decrement(ref _count);
                if (count == 0) _completed.SetResult(null);
            });
        }
        public bool IsComplete()
        {
            return WaitForDeferralsAsync().IsCompleted;
        }
        public Task WaitForDeferralsAsync()
        {
            if (_count == 0) return Task.CompletedTask;
            return _completed.Task;
        }
    }

    public sealed class Deferral
    {
        private Action _callback;
        public Deferral(Action callback)
        {
            _callback = callback;
        }
        public void Complete()
        {
            _callback.Invoke();
        }
    }
}
