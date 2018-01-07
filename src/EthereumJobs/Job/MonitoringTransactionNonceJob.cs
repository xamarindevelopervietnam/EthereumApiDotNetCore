using System;
using System.Threading.Tasks;
using Services.Coins;
using Common.Log;
using Common;
using Lykke.JobTriggers.Triggers.Attributes;
using Core;
using Services.Coins.Models;
using Lykke.JobTriggers.Triggers.Bindings;
using Core.Settings;
using Core.Notifiers;
using Core.Repositories;
using Services;
using Services.New.Models;
using System.Numerics;
using Core.Exceptions;
using AzureStorage.Queue;
using Newtonsoft.Json;
using EdjCase.JsonRpc.Client;
using Core.Messages.Nonce;

namespace EthereumJobs.Job
{
    public class MonitoringTransactionNonceJob
    {
        private readonly ILog _log;
        private readonly IQueueFactory _queueFactory;
        private readonly IEthereumTransactionService _ethereumTransactionService;
        private readonly ICoinTransactionService _coinTransactionService;
        private readonly TimeSpan _broadcastMonitoringPeriodSeconds;
        private readonly IBaseSettings _settings;

        public MonitoringTransactionNonceJob(
            ILog log,
            IBaseSettings settings,
            IQueueFactory queueFactory,
            IEthereumTransactionService ethereumTransactionService,
            ICoinTransactionService coinTransactionService)
        {
            _settings = settings;
            _log = log;
            _queueFactory = queueFactory;
            _ethereumTransactionService = ethereumTransactionService;
            _coinTransactionService = coinTransactionService;
            _broadcastMonitoringPeriodSeconds = TimeSpan.FromSeconds(_settings.BroadcastMonitoringPeriodSeconds);
        }

        [QueueTrigger(Constants.TransactionMonitoringNonceQueue, 100, true)]
        public async Task Execute(TransactionNonceMonitoringMessage transaction, QueueTriggeringContext context)
        {
            ICoinTransaction coinTransaction = null;
            try
            {
                bool isTransactionInMemoryPool = await _ethereumTransactionService.IsTransactionInPool(transaction.TransactionHash);
                if (isTransactionInMemoryPool)
                {
                    SendMessageToTheQueueEnd(context, transaction, 100, "Transaction is in memory pool");
                    return;
                }

                coinTransaction = await _coinTransactionService.ProcessTransaction(new CoinTransactionMessage()
                {
                    TransactionHash = transaction.TransactionHash
                });
            }
            catch (Exception ex)
            {
                if (ex.Message != transaction.LastError)
                    await _log.WriteWarningAsync("MonitoringCoinTransactionJob", "Execute", $"TrHash: [{transaction.TransactionHash}]", "");

                SendMessageToTheQueueEnd(context, transaction, 200, ex.Message);

                await _log.WriteErrorAsync("MonitoringCoinTransactionJob", "Execute", "", ex);
                return;
            }

            if ((coinTransaction == null || coinTransaction.ConfirmationLevel == 0) &&
                (DateTime.UtcNow - transaction.PutDateTime > _broadcastMonitoringPeriodSeconds))
            {
            }
        }

        private void SendMessageToTheQueueEnd(QueueTriggeringContext context, TransactionNonceMonitoringMessage transaction, int delay, string error = "")
        {
            transaction.DequeueCount++;
            transaction.LastError = string.IsNullOrEmpty(error) ? transaction.LastError : error;
            context.MoveMessageToEnd(transaction.ToJson());
            context.SetCountQueueBasedDelay(_settings.MaxQueueDelay, delay);
        }
    }
}
