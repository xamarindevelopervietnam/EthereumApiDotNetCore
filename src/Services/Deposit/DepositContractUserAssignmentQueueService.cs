using AzureStorage.Queue;
using Core;
using Core.Exceptions;
using Core.Notifiers;
using Core.Repositories;
using Core.Settings;
using Core.Utils;
using Nethereum.Web3;
using Services.Deposit;
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
    }

    public class DepositContractUserAssignmentQueueService : IDepositContractUserAssignmentQueueService
    {
        private readonly IQueueExt _queue;
        private readonly ITransferContractRepository _transferContractRepository;
        private readonly ISlackNotifier _slackNotifier;
        private readonly IBaseSettings _settings;
        private readonly Web3 _web3;

        public DepositContractUserAssignmentQueueService(IQueueFactory queueFactory,
            ITransferContractRepository transferContractRepository,
            IBaseSettings settings, Web3 web3)
        {
            _web3 = web3;
            _transferContractRepository = transferContractRepository;
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
            //var ethereumContract = _web3.Eth.GetContract(coinAbi, assignment.CoinAdapterAddress);
            //var function = ethereumContract.GetFunction("setTransferAddressUser");
            ////function setTransferAddressUser(address userAddress, address transferAddress) onlyowner{

            //string transactionHash =
            //    await function.SendTransactionAsync(_settings.EthereumMainAccount,
            //    assignment.UserAddress, assignment.TransferContractAddress);
            //var transferContract = await _transferContractRepository.GetAsync(assignment.TransferContractAddress);
            //transferContract.AssignmentHash = transactionHash;

            //await _transferContractRepository.SaveAsync(transferContract);
        }
    }
}
