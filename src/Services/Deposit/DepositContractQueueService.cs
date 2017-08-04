using AzureStorage.Queue;
using Core;
using Core.Exceptions;
using Core.Notifiers;
using Core.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Services
{
    public interface IDepositContractQueueService
    {
        Task<IDepositContract> GetContract();
        Task PushContract(IDepositContract transferContract);
        Task<int> Count();
    }

    public class DepositContractQueueService : IDepositContractQueueService
    {
        private readonly IQueueExt _queue;
        private readonly IDepositContractRepository _depositContractRepository;
        private readonly ISlackNotifier _slackNotifier;
        private readonly ICoinRepository _coinRepository;

        public DepositContractQueueService(IQueueFactory queueFactory,
            IDepositContractRepository depositContractRepository, ISlackNotifier slackNotifier)
        {
            _depositContractRepository = depositContractRepository;
            _slackNotifier = slackNotifier;
            _queue = queueFactory.Build(Constants.DepositContractsQueue);
        }

        public async Task<IDepositContract> GetContract()
        {
            string contractSerialized = await GetContractRaw();
            IDepositContract contract = Newtonsoft.Json.JsonConvert.DeserializeObject<DepositContract>(contractSerialized);

            return contract;
        }

        public async Task<string> GetContractRaw()
        {
            //TODO: think about locking code below
            var message = await _queue.GetRawMessageAsync();
            if (message == null)
                NotifyAboutError();

            await _queue.FinishRawMessageAsync(message);

            var contract = message.AsString;

            if (string.IsNullOrWhiteSpace(contract))
                NotifyAboutError();

            return contract;
        }

        public async Task PushContract(IDepositContract depositContract)
        {
            string depositContractSerialized = Newtonsoft.Json.JsonConvert.SerializeObject(depositContract);

            await _queue.PutRawMessageAsync(depositContractSerialized);
        }

        public async Task<int> Count()
        {
            return await _queue.Count() ?? 0;
        }

        public void NotifyAboutError()
        {
            _slackNotifier.ErrorAsync("Ethereum Core Service! Deposit User contract pool is empty!");
            throw new ClientSideException(ExceptionType.ContractPoolEmpty, "Transfer contract pool is empty!");
        }
    }
}
