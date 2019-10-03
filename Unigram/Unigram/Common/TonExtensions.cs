using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ton.Tonlib.Api;

namespace Ton.Tonlib.Api
{
    static class TonExtensiosn
    {
        public static long GetBalance(this GenericAccountState accountState)
        {
            var value = 0L;

            switch (accountState)
            {
                case GenericAccountStateRaw stateRaw:
                    value = stateRaw.AccountState.Balance;
                    break;
                case GenericAccountStateTestGiver stateTestGiver:
                    value = stateTestGiver.AccountState.Balance;
                    break;
                case GenericAccountStateTestWallet stateTestWallet:
                    value = stateTestWallet.AccountState.Balance;
                    break;
                case GenericAccountStateUninited stateUninited:
                    value = stateUninited.AccountState.Balance;
                    break;
                case GenericAccountStateWallet stateWallet:
                    value = stateWallet.AccountState.Balance;
                    break;
            }

            if (value >= 0)
            {
                return value;
            }

            return 0;
        }

        public static InternalTransactionId GetLastTransactionId(this GenericAccountState accountState)
        {
            switch (accountState)
            {
                case GenericAccountStateRaw stateRaw:
                    return stateRaw.AccountState.LastTransactionId;
                case GenericAccountStateTestGiver stateTestGiver:
                    return stateTestGiver.AccountState.LastTransactionId;
                case GenericAccountStateTestWallet stateTestWallet:
                    return stateTestWallet.AccountState.LastTransactionId;
                case GenericAccountStateUninited stateUninited:
                    return stateUninited.AccountState.LastTransactionId;
                case GenericAccountStateWallet stateWallet:
                    return stateWallet.AccountState.LastTransactionId;
            }

            return null;
        }
    }
}

namespace Ton.Tonlib
{
    static class TonExtensions
    {
        public static void Send(this Client client, Function function, Action<BaseObject> handler)
        {
            client.Send(function, new TonHandler(handler));
        }

        public static void Send(this Client client, Function function)
        {
            client.Send(function, null);
        }

        public static Task<BaseObject> SendAsync(this Client client, Function function)
        {
            var tsc = new TonCompletionSource();
            client.Send(function, tsc);

            return tsc.Task;
        }
    }

    class TonCompletionSource : TaskCompletionSource<BaseObject>, ClientResultHandler
    {
        public void OnResult(BaseObject result)
        {
            SetResult(result);
        }
    }

    class TonHandler : ClientResultHandler
    {
        private Action<BaseObject> _callback;

        public TonHandler(Action<BaseObject> callback)
        {
            _callback = callback;
        }

        public void OnResult(BaseObject result)
        {
            _callback(result);
        }
    }
}
