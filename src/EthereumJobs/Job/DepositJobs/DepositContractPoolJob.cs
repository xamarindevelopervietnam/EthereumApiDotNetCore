using System.Threading.Tasks;
using Core.Repositories;
using Nethereum.Web3;
using Services;
using Common.Log;
using Core.Settings;
using System.Numerics;
using System;
using System.Collections.Generic;
using Common;
using Lykke.JobTriggers.Triggers.Attributes;
using System.Linq;

namespace EthereumJobs.Job
{
    public class DepositContractPoolJob
    {
        private readonly ILog _logger;
        private readonly ICoinRepository _coinRepository;
        private readonly DepositContractPoolService _depositContractPoolService;

        public DepositContractPoolJob(IBaseSettings settings,
            ILog logger,
            ICoinRepository coinRepository,
            DepositContractPoolService depositContractPoolService
            )
        {
            _logger = logger;
            _coinRepository = coinRepository;
            _depositContractPoolService = depositContractPoolService;
        }

        [TimerTrigger("0.00:01:00")]
        public async Task Execute()
        {
            await _depositContractPoolService.Execute();
        }
    }
}
