using System;
using System.Threading;
#if !WINDOWS_PHONE
using System.Threading.Tasks;
#endif
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
#if WINDOWS_PHONE
    public class Worker
    {
        private readonly Thread _thread;

        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(true);

        public ThreadState ThreadState
        {
            get { return _thread.ThreadState; }
        }

        public string Name { get { return _thread.Name; } }

        public Worker(ParameterizedThreadStart start, string name)
        {
            _thread = new Thread(state => OnThreadStartInternal(start));
            _thread.Name = name;
            //_thread.IsBackground = true;
            _thread.Start(this);
        }

        private void OnThreadStartInternal(ParameterizedThreadStart start)
        {
            while (true)
            {

                try
                {
                    start(this);
                }
                catch (Exception e)
                {
                    TLUtils.WriteException(e);
                }

                _resetEvent.WaitOne();
            }
        }

        public bool IsWaiting
        {
            get{ return ThreadState == ThreadState.WaitSleepJoin; }
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
#else
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
#endif
}