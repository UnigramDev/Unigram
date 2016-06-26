using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public class Worker
    {
        private readonly Task _thread;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(true);

        public TaskStatus ThreadState
        {
            get { return _thread.Status; }
        }

        public Worker(Action<object> start, string name)
        {
            _thread = new Task(state => OnThreadStartInternal(start, state), this, TaskCreationOptions.LongRunning);
            //_thread.Name = name;
            //_thread.IsBackground = true;
            _thread.Start();
        }

        private bool _isWorking;

        private void OnThreadStartInternal(Action<object> start, object state)
        {
            while (true)
            {
                try
                {
                    start(state);
                }
                catch (Exception e)
                {
                    TLUtils.WriteException(e);
                }
                _isWorking = false;
                _isWorking = _resetEvent.WaitOne();
            }
        }

        public bool IsWaiting
        {
            get
            {
                return true;
                return !_isWorking;
            }
        }

        public void Start()
        {
            _resetEvent.Set();
        }

        public void Stop()
        {
            _resetEvent.Reset();
        }
    }
}