using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Services;
using Common.Log;
using Common;
using Lykke.JobTriggers.Triggers.Attributes;
using Core;
using Lykke.JobTriggers.Triggers.Bindings;
using Core.Settings;

namespace EthereumJobs.Job
{
    //Cashin
    public class DepositTransactionQueueJob
    {
        private readonly IDepositContractTransactionService _depositContractTransactionService;
        private readonly ILog _log;
        private readonly IBaseSettings _settings;

        public DepositTransactionQueueJob(IDepositContractTransactionService contractTransferTransactionService, ILog log, IBaseSettings settings)
        {
            _depositContractTransactionService = contractTransferTransactionService;
            _log = log;
            _settings = settings;
        }

        [QueueTrigger(Constants.ContractDepositQueue, 100, true)]
        public async Task Execute(DepositContractTransaction contractTransferTr, QueueTriggeringContext context)
        {
            try
            {
                await _depositContractTransactionService.TransferToCoinAdapterContract(contractTransferTr);
            }
            catch (Exception ex)
            {
                if (ex.Message != contractTransferTr.LastError)
                    await _log.WriteWarningAsync("MonitoringCoinTransactionJob", 
                        "Execute", $"ContractAddress: [{contractTransferTr.DepositContractAddress}]", "");

                contractTransferTr.LastError = ex.Message;

                if (contractTransferTr.DequeueCount >= _settings.MaxDequeueCount)
                {
                    context.MoveMessageToPoison();
                }
                else
                {
                    contractTransferTr.DequeueCount++;
                    context.MoveMessageToEnd(contractTransferTr.ToJson());
                    context.SetCountQueueBasedDelay(_settings.MaxQueueDelay, 200);
                }
                await _log.WriteErrorAsync("TransferTransactionQueueJob", "TransferTransactionQueue", "", ex);
            }
        }
    }
}
