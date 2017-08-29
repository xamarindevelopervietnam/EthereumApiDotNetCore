using System.Threading.Tasks;
using Core.Repositories;
using Nethereum.Web3;
using Services;
using Common.Log;
using Core.Settings;
using System.Numerics;
using System;
using Common;
using Lykke.JobTriggers.Triggers.Attributes;
using Lykke.JobTriggers.Triggers.Bindings;
using Core;

namespace EthereumJobs.Job
{
    public class MonitoringDepositTransactionsJob
    {
        private readonly ILog _logger;
        private readonly IDepositContractTransactionService _depositContractTransactionService;
        private readonly IBaseSettings _settings;

        public MonitoringDepositTransactionsJob(IBaseSettings settings,
            ILog logger,
            IDepositContractTransactionService depositContractTransactionService
            )
        {
            _settings = settings;
            _logger = logger;
            _depositContractTransactionService = depositContractTransactionService;
        }

        [QueueTrigger(Constants.ContractDepositQueue, 100, true)]
        public async Task Execute(DepositContractTransaction transaction, QueueTriggeringContext context)
        {
            try
            {
                await _depositContractTransactionService.TransferToCoinAdapterContract(transaction);
            }
            catch (Exception ex)
            {
                if (ex.Message != transaction.LastError)
                    await _logger.WriteWarningAsync("MonitoringDepositTransactions", "Execute", 
                        $"ContractAddress: [{transaction.DepositContractAddress}]", "");

                transaction.LastError = ex.Message;

                if (transaction.DequeueCount >= 5)
                {
                    context.MoveMessageToPoison();
                }
                else
                {
                    transaction.DequeueCount++;
                    context.MoveMessageToEnd();
                    context.SetCountQueueBasedDelay(_settings.MaxQueueDelay, 200);
                }
                await _logger.WriteErrorAsync("MonitoringDepositTransactions", "Execute", "", ex);
            }
        }
    }
}
