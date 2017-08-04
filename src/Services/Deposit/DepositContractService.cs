using Core;
using Core.Exceptions;
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
    public interface IDepositContractService
    {
        Task<string> CreateDepositContractTrHashWithoutUserAsync();

        Task<string> GetUserAddressForDepositContract(string depositContractAddress);

        Task<string> SetUserAddressForDepositContract(string userAddress, string depositContractAddress);

        Task<IDepositContract> GetDepositContract(string userAddress);
    }

    public class DepositContractService : IDepositContractService
    {
        private readonly ICoinRepository _coinRepository;
        private readonly IContractService _contractService;
        private readonly IBaseSettings _settings;
        private readonly IDepositContractRepository _depositContractRepository;
        private readonly ITransferContractQueueServiceFactory _transferContractQueueServiceFactory;
        private readonly ITransferContractUserAssignmentQueueService _transferContractUserAssignmentQueueService;
        private readonly Web3 _web3;
        private readonly IPaymentService _paymentService;
        private readonly IErcInterfaceService _ercInterfaceService;

        public DepositContractService(IContractService contractService,
            IDepositContractRepository depositContractRepository,
            IBaseSettings settings,
            ITransferContractQueueServiceFactory transferContractQueueServiceFactory,
            ITransferContractUserAssignmentQueueService transferContractUserAssignmentQueueService,
            IPaymentService paymentService,
            Web3 web3)
        {
            _paymentService = paymentService;
            _web3 = web3;
            _contractService = contractService;
            _depositContractRepository = depositContractRepository;
            _settings = settings;
            _transferContractQueueServiceFactory = transferContractQueueServiceFactory;
            _transferContractUserAssignmentQueueService = transferContractUserAssignmentQueueService;
        }

        public async Task<string> CreateDepositContractTrHashWithoutUserAsync()
        {
            string transactionHash;

             transactionHash =
                    await _contractService.CreateContractWithoutBlockchainAcceptance(_settings.DepositContract.Abi,
                    _settings.DepositContract.ByteCode, _settings.EthereumAdapterAddress, _settings.DepositAdminContract.Address);

            return transactionHash;
        }

        //public async Task<string> CreateTransferContract(string userAddress)
        //{
        //    IDepositContract contract = await GetDepositContract(userAddress);

        //    if (contract != null)
        //    {
        //        throw new ClientSideException(ExceptionType.EntityAlreadyExists, $"Transfer account for {userAddress} already exists");
        //    }

        //    ICoin coin = await GetCoinWithCheck(coinAdapterAddress);
        //    string queueName = QueueHelper.GenerateQueueNameForContractPool(coinAdapterAddress);
        //    ITransferContractQueueService transferContractQueueService = _transferContractQueueServiceFactory.Get(queueName);
        //    ITransferContract transferContract = await transferContractQueueService.GetContract();
        //    transferContract.UserAddress = userAddress;

        //    await _depositContractRepository.SaveAsync(transferContract);
        //    await _transferContractUserAssignmentQueueService.PushContract(new TransferContractUserAssignment()
        //    {
        //        TransferContractAddress = transferContract.ContractAddress,
        //        UserAddress = userAddress,
        //        CoinAdapterAddress = coin.AdapterAddress
        //    });

        //    return transferContract.ContractAddress;
        //}

        public async Task<string> GetUserAddressForDepositContract(string depositContractAddress)
        {
            IDepositContract depositContract = await _depositContractRepository.GetByAddressAsync(depositContractAddress);

            if (depositContract == null)
            {
                throw new ClientSideException(ExceptionType.WrongParams, $"Transfer contract with address {depositContractAddress} does not exist");
            }

            string adminAbi = _settings.DepositAdminContract.Abi;

            var contract = _web3.Eth.GetContract(adminAbi, _settings.DepositAdminContract.Address);
            var function = contract.GetFunction("addDepositContractUser");
            //function setTransferAddressUser(address userAddress, address transferAddress) onlyowner{
            string userAddress =
                await function.CallAsync<string>("getDepositContractUser");
            depositContract.UserAddress = userAddress;

            await _depositContractRepository.SaveAsync(depositContract);

            return userAddress;
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
            //function setTransferAddressUser(address userAddress, address transferAddress) onlyowner{
            string transaction =
                await function.SendTransactionAsync(_settings.EthereumMainAccount, depositContractAddress, userAddress);
            depositContract.UserAddress = userAddress;

            await _depositContractRepository.SaveAsync(depositContract);

            return transaction;
        }

        public async Task<IDepositContract> GetDepositContract(string userAddress)
        {
            IDepositContract contract = await _depositContractRepository.GetByUserAsync(userAddress);

            return contract;
        }
    }
}
