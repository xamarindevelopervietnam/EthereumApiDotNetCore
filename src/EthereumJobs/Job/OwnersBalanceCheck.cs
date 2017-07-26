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
using Services.New;
using Core.Notifiers;
using System.Linq;

namespace EthereumJobs.Job
{
    public class OwnersBalanceCheck
    {
        private readonly ILog _logger;
        private readonly ICoinRepository _coinRepository;
        private readonly TransferContractPoolService _transferContractPoolService;
        private readonly IOwnerService _ownerService;
        private readonly IPaymentService _paymentService;
        private readonly IBaseSettings _settings;
        private DateTime _lastWarningSentTime;
        private readonly ISlackNotifier _slackNotifier;

        public OwnersBalanceCheck(IBaseSettings settings,
            ILog logger,
            IOwnerService ownerService,
            IPaymentService paymentService,
            ISlackNotifier slackNotifier
            )
        {
            _slackNotifier = slackNotifier;
            _settings = settings;
            _paymentService = paymentService;
            _ownerService = ownerService;
            _logger = logger;
        }

        [TimerTrigger("0.00:05:00")]
        public async Task Execute()
        {
            List<IOwner> allOwners = (await _ownerService.GetAll()).ToList();
            allOwners.Add(new Owner() { Address = _settings.EthereumMainAccount });

            foreach (var owner in allOwners)
            {
                await InternalBalanceCheck(owner.Address);
            }
        }

        private async Task InternalBalanceCheck(string address)
        {
            try
            {
                var balance = await _paymentService.GetUserContractBalance(address);
                if (balance < _settings.MainAccountMinBalance)
                {
                    if ((DateTime.UtcNow - _lastWarningSentTime).TotalHours > 1)
                    {
                        string message = $"Main account {address} balance is less that {_settings.MainAccountMinBalance} ETH !";

                        await _logger.WriteWarningAsync("OwnersBalanceCheck", "InternalBalanceCheck", "", message);
                        await _slackNotifier.FinanceWarningAsync(message);

                        _lastWarningSentTime = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception e)
            {
                await _logger.WriteErrorAsync("OwnersBalanceCheck", "InternalBalanceCheck", "", e);
            }
        }
    }
}
