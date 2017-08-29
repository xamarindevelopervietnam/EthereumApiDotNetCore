using System;
using System.Threading.Tasks;
using Common.Log;
using Core.Notifiers;
using Core.Settings;
using Lykke.JobTriggers.Triggers.Attributes;
using Services;
using Services.New;

namespace EthereumJobs.Job
{
    public class OwnersBalanceCheckJob
    {
        private readonly ILog                        _logger;
        private readonly IOwnerService               _ownerService;
        private readonly IPaymentService             _paymentService;
        private readonly IBaseSettings               _settings;
        private readonly ISlackNotifier              _slackNotifier;

        private DateTime _lastWarningSentTime;



        public OwnersBalanceCheckJob(
            IBaseSettings settings,
            ILog logger,
            IOwnerService ownerService,
            IPaymentService paymentService,
            ISlackNotifier slackNotifier)
        {
            _logger         = logger;
            _ownerService   = ownerService;
            _paymentService = paymentService;
            _settings       = settings;
            _slackNotifier  = slackNotifier;
        }



        [TimerTrigger("0.00:05:00")]
        public async Task Execute()
        {
            foreach (var owner in await _ownerService.GetAll())
            {
                await InternalBalanceCheck(owner.Address);
            }

            await InternalBalanceCheck(_settings.EthereumMainAccount);
        }

        private async Task InternalBalanceCheck(string address)
        {
            try
            {
                var balance = await _paymentService.GetUserContractBalance(address);
                if (balance < _settings.MainAccountMinBalance && (DateTime.UtcNow - _lastWarningSentTime).TotalHours > 1)
                {
                    var message = $"Main account {address} balance is less that {_settings.MainAccountMinBalance} ETH !";

                    await _logger.WriteWarningAsync
                    (
                        "OwnersBalanceCheck",
                        "InternalBalanceCheck",
                        "",
                        message
                    );

                    await _slackNotifier.FinanceWarningAsync(message);

                    _lastWarningSentTime = DateTime.UtcNow;
                }
                    
            }
            catch (Exception e)
            {
                await _logger.WriteErrorAsync
                (
                    "OwnersBalanceCheck",
                    "InternalBalanceCheck",
                    "",
                    e
                );
            }
        }
    }
}