using Common.Log;
using Core;
using Core.Notifiers;
using Core.Repositories;
using Core.Settings;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public class DepositContractPoolService
    {
        private readonly ITransferContractQueueServiceFactory _transferContractQueueServiceFactory;
        private readonly IContractService _contractService;
        private readonly IPaymentService _paymentService;
        private readonly ILog _logger;
        private readonly IBaseSettings _settings;
        private readonly ISlackNotifier _slackNotifier;
        private static DateTime _lastWarningSentTime = DateTime.MinValue;
        private readonly IDepositContractService _depositContractService;
        private readonly ICoinRepository _coinRepository;
        private readonly IDepositContractUserAssignmentQueueService _depositContractUserAssignmentQueueService;
        private readonly IDepositContractQueueService _depositContractQueueService;

        public DepositContractPoolService(
            IDepositContractService depositContractService,
            IBaseSettings settings,
            IContractService contractService,
            IPaymentService paymentService,
            ISlackNotifier slackNotifier,
            ICoinRepository coinRepository,
            ILog logger,
            IDepositContractUserAssignmentQueueService depositContractUserAssignmentQueueService,
            IDepositContractQueueService depositContractQueueService)
        {
            _coinRepository = coinRepository;
            _depositContractService = depositContractService;
            _settings = settings;
            _contractService = contractService;
            _paymentService = paymentService;
            _slackNotifier = slackNotifier;
            _logger = logger;
            _depositContractUserAssignmentQueueService = depositContractUserAssignmentQueueService;
            _depositContractQueueService = depositContractQueueService;
        }

        public async Task Execute()
        {
            await InternalBalanceCheck();

            int currentCount = await _depositContractQueueService.Count();

            if (currentCount < _settings.MinContractPoolLength)
            {
                while (currentCount < _settings.MaxContractPoolLength)
                {
                    await InternalBalanceCheck();

                    List<string> trHashes = new List<string>(_settings.ContractsPerRequest);

                    for (int i = 0; i < _settings.ContractsPerRequest; i++)
                    {
                        var transferContractTrHash = 
                            await _depositContractService.CreateDepositContractTrHashWithoutUserAsync();
                        trHashes.Add(transferContractTrHash);
                    }

                    IEnumerable<string> contractAddresses = await _contractService.GetContractsAddresses(trHashes);
                    List<Task> contractPushTasks = new List<Task>();

                    foreach (var address in contractAddresses)
                    {
                        await _depositContractQueueService.PushContract(new DepositContract()
                        {
                            ContractAddress = address,
                            UserAddress = null,
                            EthAdapterAddress = _settings.EthereumAdapterAddress,
                            AssignmentHash = null,
                        });
                    }

                    currentCount += _settings.ContractsPerRequest;
                }
            }
        }

        private async Task InternalBalanceCheck()
        {
            try
            {
                var balance = await _paymentService.GetMainAccountBalance();
                if (balance < _settings.MainAccountMinBalance)
                {
                    if ((DateTime.UtcNow - _lastWarningSentTime).TotalHours > 1)
                    {
                        string message = $"Main account {_settings.EthereumMainAccount} balance is less that {_settings.MainAccountMinBalance} ETH !";

                        await _logger.WriteWarningAsync("TransferContractPoolService", "InternalBalanceCheck", "", message);
                        await _slackNotifier.FinanceWarningAsync(message);

                        _lastWarningSentTime = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception e)
            {
                await _logger.WriteErrorAsync("TransferContractPoolService", "InternalBalanceCheck", "", e);
            }
        }
    }
}
