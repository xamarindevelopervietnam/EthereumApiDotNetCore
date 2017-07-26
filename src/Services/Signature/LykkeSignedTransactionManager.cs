using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Web3;
using Nethereum.Hex.HexConvertors.Extensions;
using SigningServiceApiCaller;
using Nethereum.ABI.Util;
using Nethereum.Util;
using Nethereum.Signer;
using SigningServiceApiCaller.Models;
using Core;
using Core.Settings;
using System.Threading;
using Services.Signature;
using Nethereum.RPC.TransactionManagers;
using System;
using BusinessModels;

namespace LkeServices.Signature
{
    public class LykkeSignedTransactionManager : ITransactionManager
    {
        private static BigInteger _minGasPrice;
        private static BigInteger _maxGasPrice;
        private BigInteger _nonceCount = -1;
        private readonly ILykkeSigningAPI _signatureApi;
        private readonly Web3 _web3;
        private readonly IBaseSettings _baseSettings;
        private readonly SemaphoreSlim _readLock;
        private readonly INonceCalculator _nonceCalculator;
        private readonly IRoundRobinTransactionSender _roundRobinTransactionSender;

        public IClient Client { get; set; }
        public BigInteger DefaultGasPrice { get; set; }
        public BigInteger DefaultGas { get; set; }

        public LykkeSignedTransactionManager(Web3 web3, 
            ILykkeSigningAPI signatureApi, 
            IBaseSettings baseSettings, 
            INonceCalculator nonceCalculator,
            IRoundRobinTransactionSender roundRobinTransactionSender)
        {
            _nonceCalculator = nonceCalculator;
            _baseSettings = baseSettings;
            _maxGasPrice = new BigInteger(_baseSettings.MaxGasPrice);
            _minGasPrice = new BigInteger(_baseSettings.MinGasPrice);
            _signatureApi = signatureApi;
            Client = web3.Client;
            _web3 = web3;
            _roundRobinTransactionSender = roundRobinTransactionSender;
            _readLock = new SemaphoreSlim(1, 1);
        }

        public async Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)
        {
            var ethGetTransactionCount = new EthGetTransactionCount(Client);
            var nonce = transaction.Nonce;
            if (nonce == null)
            {
                nonce = await ethGetTransactionCount.SendRequestAsync(transaction.From).ConfigureAwait(false);

                if (nonce.Value <= _nonceCount)
                {
                    _nonceCount = _nonceCount + 1;
                    nonce = new HexBigInteger(_nonceCount);
                }
                else
                {
                    _nonceCount = nonce.Value;
                }
            }

            return nonce;
        }

        public async Task<string> SendTransactionAsync<T>(T transaction) where T : TransactionInput
        {
            var ethSendTransaction = new EthSendRawTransaction(Client);
            var currentGasPriceHex = await _web3.Eth.GasPrice.SendRequestAsync();
            var currentGasPrice = currentGasPriceHex.Value;
            HexBigInteger nonce;
            
            #region RoundRobin

            if (transaction.From == Constants.AddressForRoundRobinTransactionSending)
            {
                //Send from RoundRobin pool
                AddressNonceModel senderInfo = await _roundRobinTransactionSender.GetSenderAndHisNonce();
                transaction.From = senderInfo.Address;
                nonce = new HexBigInteger(senderInfo.Nonce);
            }
            else
            {
                //Send from EthereumMainAccount
                try
                {
                    await _readLock.WaitAsync();
                    nonce = await GetNonceAsync(transaction);
                }
                finally
                {
                    _readLock.Release();
                }
            }

            #endregion

            var value = transaction.Value?.Value ?? 0;
            BigInteger selectedGasPrice = currentGasPrice * _baseSettings.GasPricePercentage / 100;
            if (selectedGasPrice > _maxGasPrice)
            {
                selectedGasPrice = _maxGasPrice;
            }
            else if (selectedGasPrice < _minGasPrice)
            {
                selectedGasPrice = _minGasPrice;
            }

            var gasPrice = selectedGasPrice;
            var gasValue = transaction.Gas?.Value ?? Constants.GasForCoinTransaction;
            var tr = new Nethereum.Signer.Transaction(transaction.To, value, nonce, gasPrice, gasValue, transaction.Data);
            var hex = tr.GetRLPEncoded().ToHex();

            var requestBody = new EthereumTransactionSignRequest()
            {
                FromProperty = new AddressUtil().ConvertToChecksumAddress(transaction.From),
                Transaction = hex
            };

            var response = await _signatureApi.ApiEthereumSignPostAsync(requestBody);

            return await ethSendTransaction.SendRequestAsync(response.SignedTransaction.EnsureHexPrefix()).ConfigureAwait(false);
        }

        public async Task<HexBigInteger> EstimateGasAsync<T>(T callInput) where T : CallInput
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (callInput == null) throw new ArgumentNullException(nameof(callInput));
            var ethEstimateGas = new EthEstimateGas(Client);
            return await ethEstimateGas.SendRequestAsync(callInput);
        }

        public async Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount)
        {
            return await SendTransactionAsync(new TransactionInput("", to, from, new HexBigInteger(DefaultGas), amount));
        }
    }
}
