using System;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Web3;
using Newtonsoft.Json.Linq;
using SigningServiceApiCaller;
using Core.Settings;
using Nethereum.RPC.TransactionManagers;

namespace LkeServices.Signature
{
    public class SignatureInterceptor : RequestInterceptor
    {
        private readonly ITransactionManager _transactionManager;

        public SignatureInterceptor(ITransactionManager transactionManager)
        {
            _transactionManager = transactionManager;
        }

        public RpcResponse BuildResponse(object results, string route = null)
        {
            return new RpcResponse(route, JToken.FromObject(results));
        }
        
        public override async Task<object> InterceptSendRequestAsync<TResponse>(
            Func<Nethereum.JsonRpc.Client.RpcRequest, string, Task<TResponse>> interceptedSendRequestAsync, Nethereum.JsonRpc.Client.RpcRequest request, 
            string route = null)
        {
            if (request.Method == "eth_sendTransaction")
            {
                TransactionInput transaction = (TransactionInput)request.RawParameters[0];
                return await SignAndSendTransaction(transaction, route);
            }
            return await interceptedSendRequestAsync(request, route).ConfigureAwait(false);
        }

        public override async Task<object> InterceptSendRequestAsync<TResponse>(Func<string, string, object[], Task<TResponse>> interceptedSendRequestAsync, string method, string route = null, params object[] paramList)
        {
            if (method == "eth_sendTransaction")
            {
                TransactionInput transaction = (TransactionInput)paramList[0];
                return await SignAndSendTransaction(transaction, route);
            }
            return await interceptedSendRequestAsync(method, route, paramList).ConfigureAwait(false);
        }

        private async Task<string> SignAndSendTransaction(TransactionInput transaction, string route)
        {
            return await _transactionManager.SendTransactionAsync(transaction).ConfigureAwait(false);
        }
    }
}
