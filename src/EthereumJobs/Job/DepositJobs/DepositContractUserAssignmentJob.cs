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
using Core;
using Lykke.JobTriggers.Triggers.Bindings;
namespace EthereumJobs.Job
{
    public class DepositContractUserAssignmentJob
    { 
        private readonly ILog _logger;
        private readonly IDepositContractUserAssignmentQueueService _depositContractUserAssignmentQueueService;
        private readonly IBaseSettings _settings;
        private readonly ICoinRepository _coinRepository;
        private readonly IDepositContractService _depositContractService;

        public DepositContractUserAssignmentJob(IBaseSettings settings,
            ILog logger,
            IDepositContractUserAssignmentQueueService depositContractUserAssignmentQueueService,
            ICoinRepository coinRepository,
            IDepositContractService depositContractService
            )
        {
            _depositContractService = depositContractService;
            _settings = settings;
            _logger = logger;
            _depositContractUserAssignmentQueueService = depositContractUserAssignmentQueueService;
            _coinRepository = coinRepository;
        }

        [QueueTrigger(Constants.DepositContractsAssignmentQueue, 100, true)]
        public async Task Execute(DepositContractUserAssignment assignmentRequest, QueueTriggeringContext context)
        {
            try
            {
                string assignedUser = await _depositContractService.GetUserAddressForDepositContract(assignmentRequest.DepositContractAddress);

                if (string.IsNullOrEmpty(assignedUser) || assignedUser == Constants.EmptyEthereumAddress)
                {
                    await _depositContractUserAssignmentQueueService.CompleteAssignment(assignmentRequest);
                }
                else
                {
                    await _logger.WriteInfoAsync("TransferContractUserAssignmentJob", "Execute", $"{assignmentRequest.DepositContractAddress}", 
                        $"Skipp assignment, current user {assignedUser}",DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message != assignmentRequest.LastError)
                    await _logger.WriteWarningAsync("TransferContractUserAssignmentJob", "Execute", 
                        $"TransferContractAddress: [{assignmentRequest.DepositContractAddress}]", "");

                assignmentRequest.LastError = ex.Message;

                if (assignmentRequest.DequeueCount >= 4)
                {
                    context.MoveMessageToPoison();
                }
                else
                {
                    assignmentRequest.DequeueCount++;
                    context.MoveMessageToEnd();
                    context.SetCountQueueBasedDelay(_settings.MaxQueueDelay, 200);
                }
                await _logger.WriteErrorAsync("TransferContractUserAssignmentJob", "Execute", "", ex);
            }
        }
    }
}
