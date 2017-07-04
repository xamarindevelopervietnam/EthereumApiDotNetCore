using BusinessModels;
using BusinessModels.PrivateWallet;
using Core.Exceptions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Util;
using Nethereum.Web3;
using Services.Signature;
using Services.Transactions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Services.PrivateWallet
{
    public interface IPrivateWalletService
    {
        Task<string> GetTransactionForSigning(EthTransaction ethTransaction);
        Task<string> SubmitSignedTransaction(string from, string signedTrHex);
        //Task<bool> CheckTransactionSign(string from, string signedTrHex);
    }

    public class PrivateWalletService : IPrivateWalletService
    {
        private readonly IWeb3 _web3;
        private readonly INonceCalculator _nonceCalculator;
        private readonly IRawTransactionSubmitter _rawTransactionSubmitter;

        public PrivateWalletService(IWeb3 web3, 
            INonceCalculator nonceCalculator, 
            IRawTransactionSubmitter rawTransactionSubmitter)
        {
            _rawTransactionSubmitter = rawTransactionSubmitter;
            _nonceCalculator = nonceCalculator;
            _web3 = web3;
        }

        public async Task<string> GetTransactionForSigning(EthTransaction ethTransaction)
        {
            string from = ethTransaction.FromAddress;

            var gas = new Nethereum.Hex.HexTypes.HexBigInteger(ethTransaction.GasAmount);
            var gasPrice = new Nethereum.Hex.HexTypes.HexBigInteger(ethTransaction.GasPrice);
            var nonce = await _nonceCalculator.GetNonceAsync(from);
            var to = ethTransaction.ToAddress;
            var value = new Nethereum.Hex.HexTypes.HexBigInteger(ethTransaction.Value);
            var tr = new Nethereum.Signer.Transaction(to, value, nonce, gasPrice, gas);
            var hex = tr.GetRLPEncoded().ToHex();

            return hex;
        }

        public async Task<string> SubmitSignedTransaction(string from, string signedTrHex)
        {
            string transactionHex = await _rawTransactionSubmitter.SubmitSignedTransaction(from, signedTrHex);

            return transactionHex;
        }
    }
}
