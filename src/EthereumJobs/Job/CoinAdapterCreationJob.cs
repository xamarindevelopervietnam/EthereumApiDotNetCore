using System;
using System.Threading.Tasks;
using Common.Log;
using Lykke.JobTriggers.Triggers.Attributes;
using Core;
using Core.Settings;
using Services;
using Core.Messages;
using RabbitMQ;
using Lykke.Job.EthereumCore.Contracts.Events;

namespace EthereumJobs.Job
{
    public class CoinAdapterCreationJob
    {
        private readonly ILog _log;
        private readonly IRabbitQueuePublisher _rabbitQueuePublisher;
        private readonly IAssetContractService _assetContractService;
        private readonly IBaseSettings _settings;

        public CoinAdapterCreationJob(ILog log, 
            IBaseSettings settings, 
            IAssetContractService assetContractService,
            IRabbitQueuePublisher rabbitQueuePublisher) 
        {
            _assetContractService = assetContractService;
            _settings             = settings;
            _log                  = log;
            _rabbitQueuePublisher = rabbitQueuePublisher;
        }

        [QueueTrigger(Constants.CoinAdapterCreateQueue)]
        public async Task Execute(CoinAdapterCreationMessage coinAdapterCreationMessage)
        {
            try
            {
                string adapterAddress = await _assetContractService.CreateCoinAdapterAsync(coinAdapterCreationMessage);
                coinAdapterCreationMessage.AdapterAddress = adapterAddress;
                CoinAdapterCreationEvent creationEvent = new CoinAdapterCreationEvent(
                    coinAdapterCreationMessage.AdapterAddress,
                    coinAdapterCreationMessage.Blockchain,
                    coinAdapterCreationMessage.BlockchainDepositEnabled,
                    coinAdapterCreationMessage.ContainsEth,
                    coinAdapterCreationMessage.DeployedTransactionHash,
                    coinAdapterCreationMessage.ExternalTokenAddress,
                    coinAdapterCreationMessage.Id,
                    coinAdapterCreationMessage.Multiplier,
                    coinAdapterCreationMessage.Name
                    );

                await _rabbitQueuePublisher.PublshEvent<CoinAdapterCreationEvent>(creationEvent);
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync("MonitoringCoinTransactionJob", "Execute", "", ex);
                return;
            }
        }
    }
}
