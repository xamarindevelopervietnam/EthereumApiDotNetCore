using Common.Log;
using Core;
using Core.Repositories;
using Core.Settings;
using Lykke.JobTriggers.Triggers.Attributes;
using Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace EthereumJobs.Job
{
    public class DepositPoolRenewJob
    {
        private readonly ILog _logger;
        private readonly IBaseSettings _baseSettings;
        private readonly ICoinRepository _coinRepository;
        private readonly IDepositContractQueueService _depositContractQueueService;

        public DepositPoolRenewJob(ILog logger, ICoinRepository coinRepository, IBaseSettings baseSettings,
            IDepositContractQueueService depositContractQueueService)
        {
            _logger = logger;
            _coinRepository = coinRepository;
            _baseSettings = baseSettings;
            _depositContractQueueService = depositContractQueueService;
        }

        public IDepositContractQueueService DepositContractQueueService => _depositContractQueueService;

        [TimerTrigger("1.00:00:00")]
        public async Task Execute()
        {
            await _logger.WriteInfoAsync("DepositPoolRenewJob", "Execute", "", "DepositPoolRenewJob has been started ", DateTime.UtcNow);
            try
            {
                var count = await _depositContractQueueService.Count();

                for (int i = 0; i < count; i++)
                {
                    var contract = await _depositContractQueueService.GetContract();
                    if (contract == null)
                        return;
                    await _depositContractQueueService.PushContract(contract);
                }

                await _logger.WriteInfoAsync("DepositPoolRenewJob", "Execute", "", $"DepositPoolRenewJob has been finished for {count} contracts", DateTime.UtcNow);
            }
            catch (Exception e)
            {
                await _logger.WriteErrorAsync("DepositPoolRenewJob", "Execute", "", e);
            }
        }
    }
}
