﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Lykke.Service.EthereumCore.Core;
using Lykke.Service.EthereumCore.Core.ContractEvents;
using Lykke.Service.EthereumCore.Core.Repositories;
using Lykke.Service.EthereumCore.Core.Settings;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Lykke.Service.EthereumCore.Services.New.Models;

namespace Lykke.Service.EthereumCore.Services
{
    public interface IContractService
    {
        Task<IEnumerable<string>> GetContractsAddresses(IEnumerable<string> transactionHashes);
        Task<string> CreateContractWithoutBlockchainAcceptance(string abi, string bytecode, params object[] constructorParams);
        Task<string> CreateContract(string abi, string bytecode, ulong gas = 2000000, params object[] constructorParams);
        Task<ContractDeploymentInfo> CreateContractWithDeploymentInfo(string abi, string bytecode, params object[] constructorParams);
        Task<HexBigInteger> GetFilterEventForUserContractPayment();
        Task<HexBigInteger> CreateFilterEventForUserContractPayment();
        Task<UserPaymentEvent[]> GetNewPaymentEvents(HexBigInteger filter);
        Task<BigInteger> GetCurrentBlock();
        Task<List<T>> GetEvents<T>(string address, string abi, string eventName, HexBigInteger filter) where T : new();
        Task<HexBigInteger> CreateFilter(string address, string abi, string eventName);
    }

    public class ContractService : IContractService
    {
        private readonly IBaseSettings _settings;
        private readonly IAppSettingsRepository _appSettings;
        private readonly Web3 _web3;

        public ContractService(IBaseSettings settings, IAppSettingsRepository appSettings, Web3 web3)
        {
            _web3 = web3;
            _settings = settings;
            _appSettings = appSettings;
        }

        public async Task<ContractDeploymentInfo> CreateContractWithDeploymentInfo(string abi, string bytecode, params object[] constructorParams)
        {
            // deploy contract
            var transactionHash = await _web3.Eth.DeployContract.SendRequestAsync(abi, bytecode, _settings.EthereumMainAccount, new HexBigInteger(2000000), constructorParams);

            // get contract transaction
            TransactionReceipt receipt;
            while ((receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash)) == null)
            {
                await Task.Delay(100);
            }

            // check if contract byte code is deployed
            var code = await _web3.Eth.GetCode.SendRequestAsync(receipt.ContractAddress);

            if (string.IsNullOrWhiteSpace(code) || code == "0x")
            {
                throw new Exception("Code was not deployed correctly, verify bytecode or enough gas was to deploy the contract");
            }

            return new ContractDeploymentInfo()
            {
                ContractAddress = receipt.ContractAddress,
                TransactionHash = transactionHash
            };
        }

        public async Task<string> CreateContract(string abi, string bytecode, ulong gas = 2000000, params object[] constructorParams)
        {
            // deploy contract
            var transactionHash = await _web3.Eth.DeployContract.SendRequestAsync(abi, bytecode, _settings.EthereumMainAccount, new HexBigInteger(gas), constructorParams);

            // get contract transaction
            TransactionReceipt receipt;
            while ((receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash)) == null)
            {
                await Task.Delay(100);
            }

            // check if contract byte code is deployed
            var code = await _web3.Eth.GetCode.SendRequestAsync(receipt.ContractAddress);

            if (string.IsNullOrWhiteSpace(code) || code == "0x")
            {
                throw new Exception("Code was not deployed correctly, verify bytecode or enough gas was to deploy the contract");
            }

            return receipt.ContractAddress;
        }


        /// <returns>transaction hash</returns>
        public async Task<string> CreateContractWithoutBlockchainAcceptance(string abi, string bytecode, params object[] constructorParams)
        {
            // deploy contract
            var transactionHash = await _web3.Eth.DeployContract.SendRequestAsync(abi, bytecode, _settings.EthereumMainAccount, new HexBigInteger(2000000), constructorParams);

            return transactionHash;
        }

        public async Task<IEnumerable<string>> GetContractsAddresses(IEnumerable<string> transactionHashes)
        {
            if (transactionHashes == null || transactionHashes.Count() == 0)
            {
                return new List<string>();
            }

            List<string> addresses = new List<string>(transactionHashes.Count());
            foreach (var tr in transactionHashes)
            {
                TransactionReceipt receipt;
                while ((receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(tr)) == null)
                {
                    await Task.Delay(100);
                }

                // check if contract byte code is deployed
                var code = await _web3.Eth.GetCode.SendRequestAsync(receipt.ContractAddress);

                if (string.IsNullOrWhiteSpace(code) || code == "0x")
                {
                    throw new Exception("Code was not deployed correctly, verify bytecode or enough gas was to deploy the contract");
                }

                addresses.Add(receipt.ContractAddress);
            }

            return addresses;
        }


        public async Task<HexBigInteger> GetFilterEventForUserContractPayment()
        {
            var setting = await _appSettings.GetSettingAsync(Constants.EthereumFilterSettingKey);
            if (!string.IsNullOrWhiteSpace(setting))
                return new HexBigInteger(setting);

            return await CreateFilterEventForUserContractPayment();
        }

        public async Task<HexBigInteger> CreateFilterEventForUserContractPayment()
        {
            var filter = await CreateFilter(_settings.MainContract.Address, _settings.MainContract.Abi, Constants.UserPaymentEvent);
            //save filter for next launch
            await _appSettings.SetSettingAsync(Constants.EthereumFilterSettingKey, filter.HexValue);
            return filter;
        }

        public async Task<HexBigInteger> CreateFilter(string address, string abi, string eventName)
        {
            var contract = new Web3(_settings.EthereumUrl).Eth.GetContract(abi, address);
            var filter = await contract.GetEvent(eventName).CreateFilterAsync();
            return filter;
        }

        public async Task<List<T>> GetEvents<T>(string address, string abi, string eventName, HexBigInteger filter) where T : new()
        {
            var contract = _web3.Eth.GetContract(abi, address);
            var ev = contract.GetEvent(eventName);
            var events = await ev.GetFilterChanges<T>(filter);
            if (events == null) return new List<T>();
            // group by because of block chain reconstructions
            return events.GroupBy(o => new { o.Log.Address, o.Log.Data }).Select(o => o.First()).Select(o => o.Event).ToList();
        }


        public async Task<UserPaymentEvent[]> GetNewPaymentEvents(HexBigInteger filter)
        {
            return (await GetEvents<UserPaymentEvent>(_settings.MainContract.Address, _settings.MainContract.Abi, Constants.UserPaymentEvent, filter)).ToArray();
        }

        public async Task<BigInteger> GetCurrentBlock()
        {
            var web3 = new Web3(_settings.EthereumUrl);
            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            return blockNumber.Value;
        }

    }
}
