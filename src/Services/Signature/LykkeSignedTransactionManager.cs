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
using Services.Signature;
using Nethereum.RPC.TransactionManagers;
using System;

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
        private readonly INonceCalculator _nonceCalculator;

        public IClient Client { get; set; }
        public BigInteger DefaultGasPrice { get; set; }
        public BigInteger DefaultGas { get; set; }

        public LykkeSignedTransactionManager(Web3 web3, ILykkeSigningAPI signatureApi, IBaseSettings baseSettings, INonceCalculator nonceCalculator)
        {
            _nonceCalculator = nonceCalculator;
            _baseSettings = baseSettings;
            _maxGasPrice = new BigInteger(_baseSettings.MaxGasPrice);
            _minGasPrice = new BigInteger(_baseSettings.MinGasPrice);
            _signatureApi = signatureApi;
            Client = web3.Client;
            _web3 = web3;
        }

        public async Task<string> SendTransactionAsync<T>(T transaction) where T : TransactionInput
        {
            var ethSendTransaction = new EthSendRawTransaction(Client);
            var currentGasPriceHex = await _web3.Eth.GasPrice.SendRequestAsync();
            var currentGasPrice = currentGasPriceHex.Value;
            var nonce = await _nonceCalculator. GetNonceAsync(transaction);
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
            var gasValue = Constants.GasForCoinTransaction;
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
