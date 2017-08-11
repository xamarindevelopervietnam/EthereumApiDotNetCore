using Core;
using Core.Repositories;
using Core.Settings;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AzureStorage.Queue;
using Core.Exceptions;
using Core.Utils;

namespace Services
{
    public class DepositContractUserAssignment : QueueMessageBase
    {
        public string UserAddress { get; set; }

        public string DepositContractAddress { get; set; }
    }

    public interface IDepositAdminContractService
    {
        Task CreateIfNotExistsAsync();
        Task<IDepositAdminContract> GetAsync();
    }

    public class DepositAdminContractService : IDepositAdminContractService
    {
        private readonly IDepositAdminContractRepository _depositAdminContractRepository;
        private readonly IBaseSettings _baseSettings;
        private readonly IContractService _contractService;
        private readonly IWeb3 _web3;
        private readonly IQueueExt _depositAssignmentQueue;
        private readonly IDepositContractRepository _depositContractRepository;
        private readonly IDepositContractQueueService _depositContractQueueService;
        private readonly IDepositContractUserAssignmentQueueService _depositContractUserAssignmentQueueService;

        public DepositAdminContractService(IBaseSettings baseSettings,
            IDepositAdminContractRepository depositAdminContractRepository,
            IContractService contractService,
            IWeb3 web3,
            IQueueFactory queueFactory,
            IDepositContractRepository depositContractRepository,
            IDepositContractQueueService depositContractQueueService,
            IDepositContractUserAssignmentQueueService depositContractUserAssignmentQueueService)
        {
            _contractService = contractService;
            _baseSettings = baseSettings;
            _depositAdminContractRepository = depositAdminContractRepository;
            _web3 = web3;
            _depositAssignmentQueue = queueFactory.Build(Constants.DepositContractsAssignmentQueue) ;
            _depositContractRepository = depositContractRepository;
            _depositContractQueueService = depositContractQueueService;
            _depositContractUserAssignmentQueueService = depositContractUserAssignmentQueueService;
        }

        public async Task CreateIfNotExistsAsync()
        {
            IDepositAdminContract adminContract = await GetAsync();
            if (adminContract != null)
            {
                return;
            }

            string contractAddress = await _contractService.CreateContract(_baseSettings.DepositAdminContract.Abi, 
                _baseSettings.DepositAdminContract.ByteCode);
            await _depositAdminContractRepository.SaveAsync(new DepositAdminContract()
            {
                ContractAddress = contractAddress,
                OwnerAddress = _baseSettings.EthereumMainAccount,
            });
        }

        public async Task<IDepositAdminContract> GetAsync()
        {
            IDepositAdminContract adminContract = await _depositAdminContractRepository.GetAsync();

            return adminContract;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userAddress"></param>
        /// <returns>address of deposit contract</returns>
        public async Task<string> AssignDepositContractToUserAsync(string userAddress)
        {
            IDepositAdminContract adminContract = await _depositAdminContractRepository.GetAsync();
            IDepositContract existingContract = await _depositContractRepository.GetByUserAsync(userAddress);

            if (existingContract != null)
            {
                throw new ClientSideException(ExceptionType.EntityAlreadyExists, $"Deposit account for {userAddress} - already exists");
            }

            IDepositContract depositContract = await _depositContractQueueService.GetContract();
            depositContract.UserAddress = userAddress;

            await _depositContractRepository.SaveAsync(depositContract);
            await _depositContractUserAssignmentQueueService.PushContract(new DepositContractUserAssignment()
            {
                DepositContractAddress = depositContract.ContractAddress,
                UserAddress = userAddress,
            });

            return depositContract.ContractAddress;
            /*
             var ethereumContract = _web3.Eth.GetContract(coinAbi, assignment.CoinAdapterAddress);
             var function = ethereumContract.GetFunction("setTransferAddressUser");
             string transactionHash =
                await function.SendTransactionAsync(_settings.EthereumMainAccount,
                assignment.UserAddress, assignment.TransferContractAddress);
             var transferContract = await _transferContractRepository.GetAsync(assignment.TransferContractAddress);
             transferContract.AssignmentHash = transactionHash;
             await _transferContractRepository.SaveAsync(transferContract);*/
        }

        public async Task Migrate()
        {
            throw new NotImplementedException();
        }
    }
}
