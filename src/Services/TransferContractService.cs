﻿using Core;
using Core.Repositories;
using Core.Settings;
using Core.Utils;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Services
{
    public interface ITransferContractService
    {
        Task<string> CreateTransferContractTrHashWithoutUser(string coinAdapterAddress);
        Task<string> CreateTransferContract(string userAddress, string coinAdapterAddress);

        Task<ITransferContract> GetTransferContract(string userAddress, string coinAdapterAddress);

        Task<string> RecievePaymentFromTransferContract(string transferContractAddress,
            string coinAdapterAddress, string userAddress);
    }

    public class TransferContractService : ITransferContractService
    {
        private readonly ICoinRepository _coinRepository;
        private readonly IContractService _contractService;
        private readonly IBaseSettings _settings;
        private readonly ITransferContractRepository _transferContractRepository;
        private readonly ITransferContractQueueServiceFactory _transferContractQueueServiceFactory;
        private readonly ITransferContractUserAssignmentQueueService _transferContractUserAssignmentQueueService;
        private readonly Web3 _web3;

        public TransferContractService(IContractService contractService,
            ITransferContractRepository transferContractRepository,
            ICoinRepository coinRepository,
            IBaseSettings settings,
            ITransferContractQueueServiceFactory transferContractQueueServiceFactory,
            ITransferContractUserAssignmentQueueService transferContractUserAssignmentQueueService,
            Web3 web3
            )
        {
            _web3 = web3;
            _coinRepository = coinRepository;
            _contractService = contractService;
            _transferContractRepository = transferContractRepository;
            _settings = settings;
            _transferContractQueueServiceFactory = transferContractQueueServiceFactory;
            _transferContractUserAssignmentQueueService = transferContractUserAssignmentQueueService;
        }

        public async Task<string> CreateTransferContractTrHashWithoutUser(string coinAdapterAddress)
        {
            ICoin coin = await GetCoinWithCheck(coinAdapterAddress);

            string externalTokenAddress = coin.ExternalTokenAddress;
            bool containsEth = coin.ContainsEth;
            string transactionHash;

            if (containsEth)
            {
                transactionHash =
                    await _contractService.CreateContractWithoutBlockchainAcceptance(_settings.EthTransferContract.Abi,
                    _settings.EthTransferContract.ByteCode, coinAdapterAddress);
            }
            else
            {
                transactionHash =
                    await _contractService.CreateContractWithoutBlockchainAcceptance(_settings.TokenTransferContract.Abi,
                    _settings.TokenTransferContract.ByteCode, coinAdapterAddress, externalTokenAddress);
            }

            return transactionHash;
        }

        public async Task<string> CreateTransferContract(string userAddress, string coinAdapterAddress)
        {
            ITransferContract contract = await GetTransferContract(userAddress, coinAdapterAddress);

            if (contract != null)
            {
                throw new Exception($"Transfer account for {userAddress} - {coinAdapterAddress} already exists");
            }

            ICoin coin = await GetCoinWithCheck(coinAdapterAddress);
            string queueName = QueueHelper.GenerateQueueNameForContractPool(coinAdapterAddress);
            ITransferContractQueueService transferContractQueueService = _transferContractQueueServiceFactory.Get(queueName);
            ITransferContract transferContract = await transferContractQueueService.GetContract();
            transferContract.UserAddress = userAddress;

            await _transferContractRepository.SaveAsync(transferContract);
            await _transferContractUserAssignmentQueueService.PushContract(new TransferContractUserAssignment()
            {
                TransferContractAddress = transferContract.ContractAddress,
                UserAddress = userAddress,
                CoinAdapterAddress = coin.AdapterAddress
            });

            return transferContract.ContractAddress;
        }

        public async Task<string> SetUserAddressForTransferContract(string userAddress, string transferContractAddress)
        {
            ITransferContract transferContract = await _transferContractRepository.GetAsync(transferContractAddress);

            if (transferContract == null)
            {
                throw new Exception($"Transfer contract with address {transferContractAddress} does not exist");
            }

            ICoin coin = await _coinRepository.GetCoinByAddress(transferContract.CoinAdapterAddress);

            if (coin == null)
            {
                throw new Exception($"Coin with address {transferContract.CoinAdapterAddress} does not exist");
            }

            string coinAbi = _settings.CoinAbi;

            var contract = _web3.Eth.GetContract(coinAbi, transferContract.CoinAdapterAddress);
            var function = contract.GetFunction("setTransferAddressUser");
            //function setTransferAddressUser(address userAddress, address transferAddress) onlyowner{
            string transaction =
                await function.SendTransactionAsync(_settings.EthereumMainAccount, userAddress, transferContractAddress);
            transferContract.UserAddress = userAddress;

            await _transferContractRepository.SaveAsync(transferContract);

            return transaction;
        }

        public async Task<ITransferContract> GetTransferContract(string userAddress, string coinAdapterAddress)
        {
            ITransferContract contract = await _transferContractRepository.GetAsync(userAddress, coinAdapterAddress);

            return contract;
        }

        public async Task<string> RecievePaymentFromTransferContract(string transferContractAddress,
            string coinAdapterAddress, string userAddress)
        {
            ICoin coinDb = await _coinRepository.GetCoinByAddress(coinAdapterAddress);

            if (!coinDb.BlockchainDepositEnabled)
                throw new Exception("Coin must be payable");

            Contract contract;

            if (coinDb.ContainsEth)
            {
                contract = _web3.Eth.GetContract(_settings.EthTransferContract.Abi, transferContractAddress);
            }
            else
            {
                contract = _web3.Eth.GetContract(_settings.TokenTransferContract.Abi, transferContractAddress);
            }

            var cashin = contract.GetFunction("cashin");
            string tr;

            //function cashin(uint id, address coin, address receiver, uint amount, uint gas, bytes params)
            tr = await cashin.SendTransactionAsync(_settings.EthereumMainAccount,
            new HexBigInteger(Constants.GasForCoinTransaction), new HexBigInteger(0));

            return tr;
        }

        private async Task<string> Create(string userAddress, string coinAdapterAddress, ICoin coin)
        {
            string externalTokenAddress = coin.ExternalTokenAddress;
            bool containsEth = coin.ContainsEth;
            string abi;
            string byteCode;

            if (containsEth)
            {
                abi = _settings.EthTransferContract.Abi;
                byteCode = _settings.EthTransferContract.ByteCode;
            }
            else
            {
                abi = _settings.TokenTransferContract.Abi;
                byteCode = _settings.TokenTransferContract.ByteCode;
            }

            string transferContractAddress = await _contractService.CreateContract(abi, byteCode, coinAdapterAddress);

            await _transferContractRepository.SaveAsync(new TransferContract()
            {
                CoinAdapterAddress = coinAdapterAddress,
                ContainsEth = containsEth,
                ContractAddress = transferContractAddress,
                ExternalTokenAddress = externalTokenAddress,
                UserAddress = userAddress,
            });

            return transferContractAddress;
        }

        private async Task<ICoin> GetCoinWithCheck(string coinAdapterAddress)
        {
            ICoin coin = await _coinRepository.GetCoinByAddress(coinAdapterAddress);

            if (coin == null)
            {
                throw new Exception($"Coin with address {coinAdapterAddress} does not exist");
            }

            return coin;
        }
    }
}
