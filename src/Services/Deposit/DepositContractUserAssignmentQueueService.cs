using AzureStorage.Queue;
using Core;
using Core.Exceptions;
using Core.Notifiers;
using Core.Repositories;
using Core.Settings;
using Core.Utils;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Services
{
    public interface IDepositContractUserAssignmentQueueService
    {
        Task PushContract(DepositContractUserAssignment assignment);
        Task CompleteAssignment(DepositContractUserAssignment assignment);
        Task<string> GetUserOnLegacyAdapter(string adapterContractAddress, string depositContract);
        Task<string> SetUserToLegacyAdapter(string coinAdapterAddress, string depositContractAddress, string userAddress);
        Task<string> SetUserAddressForDepositContract(string userAddress, string depositContractAddress);
    }

    public class DepositContractUserAssignmentQueueService : IDepositContractUserAssignmentQueueService
    {
        private readonly IQueueExt _queue;
        private readonly IDepositContractRepository _depositContractRepository;
        private readonly IBaseSettings _settings;
        private readonly Web3 _web3;
        private readonly IDepositContractService _depositContractService;
        private readonly ICoinRepository _coinRepository;

        public DepositContractUserAssignmentQueueService(IQueueFactory queueFactory,
            IDepositContractRepository depositContractRepository,
            IBaseSettings settings, Web3 web3,
            ICoinRepository coinRepository)
        {
            _coinRepository = coinRepository;
            _web3 = web3;
            _depositContractRepository = depositContractRepository;
            _queue = queueFactory.Build(Constants.DepositContractsAssignmentQueue);
            _settings = settings;
        }

        public async Task PushContract(DepositContractUserAssignment transferContract)
        {
            string transferContractSerialized = Newtonsoft.Json.JsonConvert.SerializeObject(transferContract);
            await _queue.PutRawMessageAsync(transferContractSerialized);
        }

        public async Task<int> Count()
        {
            return await _queue.Count() ?? 0;
        }

        public async Task CompleteAssignment(DepositContractUserAssignment assignment)
        {
            var depositContract = await _depositContractRepository.GetByAddressAsync(assignment.DepositContractAddress);
            string depositContractAddress = assignment.DepositContractAddress;
            string userAddress = assignment.UserAddress.ToLower();
            string depositContractAssignmentHash = await SetUserAddressForDepositContract(userAddress, depositContractAddress);
            string depositContractLegacyAssignmentHash = 
                await SetUserToLegacyAdapter(_settings.EthereumAdapterAddress, depositContractAddress, userAddress);
            depositContract.AssignmentHash = depositContractAssignmentHash;
            depositContract.LegacyEthAdapterAssignmentHash = depositContractLegacyAssignmentHash;

            await _depositContractRepository.SaveAsync(depositContract);
        }

        public async Task<string> SetUserAddressForDepositContract(string userAddress, string depositContractAddress)
        {
            IDepositContract depositContract = await _depositContractRepository.GetByAddressAsync(depositContractAddress);

            if (depositContract == null)
            {
                throw new ClientSideException(ExceptionType.WrongParams, $"Transfer contract with address {depositContractAddress} does not exist");
            }

            string adminAbi = _settings.DepositAdminContract.Abi;

            var contract = _web3.Eth.GetContract(adminAbi, _settings.DepositAdminContract.Address);
            var function = contract.GetFunction("addDepositContractUser");
            string transaction =
                await function.SendTransactionAsync(_settings.EthereumMainAccount, depositContractAddress, userAddress);
            depositContract.UserAddress = userAddress;

            await _depositContractRepository.SaveAsync(depositContract);

            return transaction;
        }

        public async Task<string> SetUserToLegacyAdapter(string coinAdapterAddress, string depositContractAddress, string userAddress)
        {
            ICoin coinAdapter = await _coinRepository.GetCoinByAddress(coinAdapterAddress);
            if (coinAdapter == null)
            {
                throw new Exception($"CoinAdapterAddress {coinAdapterAddress} does not exis");
            }

            string coinAbi;
            if (coinAdapter.ContainsEth)
            {
                coinAbi = _settings.EthAdapterContract.Abi;
            }
            else
            {
                coinAbi = _settings.TokenAdapterContract.Abi;
            }

            var ethereumContract = _web3.Eth.GetContract(coinAbi, coinAdapterAddress);
            var function = ethereumContract.GetFunction("setTransferAddressUser");

            string transactionHash =
                await function.SendTransactionAsync(_settings.EthereumMainAccount,
                userAddress, depositContractAddress);

            return transactionHash;
        }

        public async Task<string> GetUserOnLegacyAdapter(string adapterContractAddress, string depositContract)
        {
            var coinAFromDb = await _coinRepository.GetCoinByAddress(adapterContractAddress);
            string abi = coinAFromDb.ContainsEth ? _settings.EthAdapterContract.Abi : _settings.TokenAdapterContract.Abi;
            var contract = _web3.Eth.GetContract(abi, coinAFromDb.AdapterAddress);
            var balance = contract.GetFunction("getTransferAddressUser");

            return await balance.CallAsync<string>(depositContract);
        }
    }
}
