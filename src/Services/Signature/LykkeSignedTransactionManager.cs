using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AzureStorage.Queue;
using Core;
using Core.Messages.Nonce;
using Core.Repositories;
using Core.Settings;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Util;
using Nethereum.Web3;
using Newtonsoft.Json;
using SigningServiceApiCaller;
using SigningServiceApiCaller.Models;

namespace Services.Signature
{
    public class LykkeSignedTransactionManager : ITransactionManager
    {
        private readonly IBaseSettings _baseSettings;
        private readonly EthEstimateGas _estimateGas;
        private readonly INonceCalculator _nonceCalculator;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores;
        private readonly EthSendRawTransaction _sendRawTransaction;
        private readonly ILykkeSigningAPI _signingApi;
        private readonly ITransactionRouter _transactionRouter;
        private readonly Web3 _web3;
        private readonly IGasPriceRepository _gasPriceRepository;
        private readonly IQueueFactory _queueFactory;
        private readonly IQueueExt _monitoringNonceQueue;
        private readonly ITransactionInfoRepository _transactionInfoRepository;

        public LykkeSignedTransactionManager(
            IBaseSettings baseSettings,
            INonceCalculator nonceCalculator,
            ILykkeSigningAPI signingApi,
            ITransactionRouter transactionRouter,
            Web3 web3,
            IGasPriceRepository gasPriceRepository,
            IQueueFactory queueFactory,
            ITransactionInfoRepository transactionInfoRepository)
        {
            _baseSettings = baseSettings;
            _estimateGas = new EthEstimateGas(web3.Client);
            _nonceCalculator = nonceCalculator;
            _semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
            _sendRawTransaction = new EthSendRawTransaction(web3.Client);
            _signingApi = signingApi;
            _transactionRouter = transactionRouter;
            _web3 = web3;
            _gasPriceRepository = gasPriceRepository;
            _queueFactory = queueFactory;
            _monitoringNonceQueue = _queueFactory.Build(Constants.TransactionMonitoringNonceQueue);
            _transactionInfoRepository = transactionInfoRepository;

            Client = web3.Client;
        }


        public IClient Client { get; set; }

        public BigInteger DefaultGasPrice { get; set; }

        public BigInteger DefaultGas { get; set; }


        public async Task<HexBigInteger> EstimateGasAsync<T>(T callInput) where T : CallInput
        {
            if (Client == null)
            {
                throw new NullReferenceException("Client not configured");
            }

            if (callInput == null)
            {
                throw new ArgumentNullException(nameof(callInput));
            }

            return await _estimateGas.SendRequestAsync(callInput);
        }

        public async Task<string> SendTransactionAsync<T>(T transaction)
            where T : TransactionInput
        {
            if (transaction == null)
            {
                throw new ArgumentNullException();
            }

            return await SendTransactionAsync
            (
                transaction.From,
                transaction.To,
                transaction.Data,
                transaction.Value ?? new BigInteger(0),
                transaction.GasPrice,
                transaction.Gas
            );
        }

        public async Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount)
        {
            return await SendTransactionAsync
            (
                from,
                to,
                string.Empty,
                amount,
                null,
                null
            );
        }

        private async Task<string> SendTransactionAsync(string from, string to, string data, BigInteger value, BigInteger? gasPrice, BigInteger? gasValue)
        {
            from = from == Constants.AddressForRoundRobinTransactionSending
                 ? await _transactionRouter.GetNextSenderAddressAsync()
                 : from;

            var semaphore = _semaphores.GetOrAdd(from, f => new SemaphoreSlim(1, 1));

            try
            {
                await semaphore.WaitAsync();

                (gasPrice, gasValue) = await GetGasPriceAndValueAsync(gasPrice, gasValue);

                HexBigInteger nonce;
                var queue = _queueFactory.Build(Constants.AddressNonceQueueFor + from);
                var nonceMessage = await queue.GetRawMessageAsync();

                if (nonceMessage == null)
                {
                    nonce = await _nonceCalculator.GetNonceAsync(from, true);
                } else
                {
                    var newMessage = JsonConvert.DeserializeObject<NextNonceForAddressMessage>(nonceMessage.AsString);
                    await queue.FinishRawMessageAsync(nonceMessage);
                    var parsedNonce = BigInteger.Parse(newMessage.Nonce);

                    gasPrice = (gasPrice.Value * 11) / 10; // + 10%
                    nonce = new HexBigInteger(parsedNonce);
                }

                var transaction = new Nethereum.Signer.Transaction(to, value, nonce.Value, gasPrice.Value, gasValue.Value, data);
                var signRequest = new EthereumTransactionSignRequest
                {
                    FromProperty = new AddressUtil().ConvertToChecksumAddress(from),
                    Transaction = transaction.GetRLPEncoded().ToHex()
                };

                var signResponse = await _signingApi.ApiEthereumSignPostAsync(signRequest);
                var txHash = await _sendRawTransaction.SendRequestAsync(signResponse.SignedTransaction.EnsureHexPrefix());

                #region Put in Monitoring

                await _transactionInfoRepository.SaveAsync(new TransactionInfo()
                {
                    Date = DateTime.UtcNow,
                    From = from,
                    TransactionHash = txHash,
                    Nonce = nonce.ToString(),
                });

                string serializedMonitoringNonce = Newtonsoft.Json.JsonConvert.SerializeObject(new TransactionNonceMonitoringMessage()
                {
                    FromAddress = from,
                    Nonce = nonce.ToString(),
                    TransactionHash = txHash
                });

                await _monitoringNonceQueue.PutRawMessageAsync(serializedMonitoringNonce);

                #endregion 

                return txHash;
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<(BigInteger? gasPrice, BigInteger? gasValue)> GetGasPriceAndValueAsync(BigInteger? gasPrice, BigInteger? gasValue)
        {
            var gasPriceSetting = await _gasPriceRepository.GetAsync();
            var currentGasPrice = (await _web3.Eth.GasPrice.SendRequestAsync()).Value;
            var selectedGasPrice = currentGasPrice * _baseSettings.GasPricePercentage / 100;

            if (selectedGasPrice > gasPriceSetting.Max)
            {
                selectedGasPrice = gasPriceSetting.Max;
            }
            else if (selectedGasPrice < gasPriceSetting.Min)
            {
                selectedGasPrice = gasPriceSetting.Min;
            }

            gasPrice = gasPrice == null || gasPrice.Value == 0 ? selectedGasPrice : gasPrice;
            gasValue = gasValue == null || gasValue.Value == 0 ? Constants.GasForCoinTransaction : gasValue;

            return (gasPrice, gasValue);
        }
    }
}