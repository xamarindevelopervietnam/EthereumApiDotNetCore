using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core;
using Core.Repositories;
using Core.Settings;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using Services.Coins;
using AzureStorage.Queue;
using Common.Log;
using System.Numerics;
using Core.Utils;

namespace Services
{
    public class DepositContractTransaction : QueueMessageBase
    {
        public string DepositContractAddress { get; set; }
        public string UserAddress { get; set; }
        public string CoinAdapterAddress { get; set; }
        public string Amount { get; set; }
        public DateTime CreateDt { get; set; }
    }

    public interface IDepositContractTransactionService
    {
        Task PutContractTransferTransaction(DepositContractTransaction tr);
        Task TransferToCoinAdapterContract(DepositContractTransaction contractTransferTr);
    }

    public class DepositContractTransactionService : IDepositContractTransactionService
    {
        private readonly ILog _logger;
        private readonly IBaseSettings _baseSettings;
        private readonly IQueueExt _queue;
        private readonly IDepositContractRepository _depositContractRepository;
        private readonly IDepositContractService _depositContractService;
        private readonly IUserDepositWalletRepository _userDepositWalletRepository;
        private readonly IUserPaymentHistoryRepository _userPaymentHistoryRepository;
        private readonly ICoinTransactionService _cointTransactionService;
        private readonly ICoinTransactionRepository _coinTransactionRepository;
        private readonly ICoinEventService _coinEventService;
        private readonly IEventTraceRepository _eventTraceRepository;

        public DepositContractTransactionService(Func<string, IQueueExt> queueFactory,
            ILog logger,
            IExchangeContractService coinContractService,
            IBaseSettings baseSettings,
            IDepositContractRepository depositContractRepository,
            IDepositContractService depositContractService,
            IUserDepositWalletRepository userDepositWalletRepository,
            IUserPaymentHistoryRepository userPaymentHistoryRepository,
            ICoinTransactionService cointTransactionService,
            ICoinTransactionRepository coinTransactionRepository,
            ICoinEventService coinEventService,
            IEventTraceRepository eventTraceRepository)
        {
            _eventTraceRepository = eventTraceRepository;
            _logger = logger;
            _baseSettings = baseSettings;
            _queue = queueFactory(Constants.ContractTransferQueue);
            _depositContractRepository = depositContractRepository;
            _depositContractService = depositContractService;
            _userDepositWalletRepository = userDepositWalletRepository;
            _userPaymentHistoryRepository = userPaymentHistoryRepository;
            _cointTransactionService = cointTransactionService;
            _coinTransactionRepository = coinTransactionRepository;
            _coinEventService = coinEventService;
        }

        public async Task PutContractTransferTransaction(DepositContractTransaction tr)
        {
            await _queue.PutRawMessageAsync(JsonConvert.SerializeObject(tr));
        }

        public async Task TransferToCoinAdapterContract(DepositContractTransaction contractTransferTr)
        {
            try
            {
                string coinAdapterAddress = contractTransferTr.CoinAdapterAddress;
                string depositContractAddress = contractTransferTr.DepositContractAddress;
                IDepositContract depositContract = await _depositContractRepository.GetByAddressAsync(depositContractAddress);
                BigInteger balance = await _depositContractService.GetBalance(depositContractAddress, coinAdapterAddress);

                if (balance == 0)
                {
                    await UpdateUserDepositWallet(contractTransferTr);
                    await _logger.WriteInfoAsync("DepositContractTransactionService", "TransferToCoinContract", "", 
                        $"Can't cashin: there is no funds on the transfer contract {contractTransferTr.DepositContractAddress}", DateTime.UtcNow);

                    return;
                }

                var opId = $"InternalOperation-{Guid.NewGuid().ToString()}";
                var transactionHash = await _depositContractService.RecievePaymentFromTransferContract(depositContractAddress,
                    coinAdapterAddress, balance);
                await _coinEventService.PublishEvent(new CoinEvent(opId, 
                    transactionHash, contractTransferTr.DepositContractAddress, contractTransferTr.UserAddress,
                    balance.ToString(), CoinEventType.CashinStarted, coinAdapterAddress));
                await _eventTraceRepository.InsertAsync(new EventTrace()
                {
                    Note = $"First Cashin appearance {transactionHash} put in {Constants.TransactionMonitoringQueue}",
                    OperationId = opId,
                    TraceDate = DateTime.UtcNow
                });
                await _userPaymentHistoryRepository.SaveAsync(new UserPaymentHistory()
                {
                    Amount = balance.ToString(),
                    ToAddress = depositContractAddress,
                    AdapterAddress = coinAdapterAddress,
                    CreatedDate = DateTime.UtcNow,
                    Note = $"Cashin from transfer contract {depositContractAddress}",
                    TransactionHash = transactionHash,
                    UserAddress = contractTransferTr.UserAddress
                });

                await _logger.WriteInfoAsync("DepositContractTransactionService", "TransferToCoinContract", "",
                    $"Transfered {balance} from transfer contract to \"{contractTransferTr.CoinAdapterAddress}\" by transaction \"{transactionHash}\". Receiver = {contractTransferTr.UserAddress}");
            }
            catch (Exception e)
            {
                await _logger.WriteErrorAsync("DepositContractTransactionService", "TransferToCoinContract",
                            $"{contractTransferTr.DepositContractAddress} - {contractTransferTr.CoinAdapterAddress} - {contractTransferTr.Amount}", e);
                throw;
            }
        }

        private async Task UpdateUserDepositWallet(DepositContractTransaction contractTransferTr)
        {
            await _userDepositWalletRepository.ReplaceAsync(new UserDepositWallet()
            {
                LastBalance = "",
                DepositContractAddress = contractTransferTr.DepositContractAddress,
                CoinAdapterAddress = contractTransferTr.CoinAdapterAddress,
                UpdateDate = DateTime.UtcNow,
                UserAddress = contractTransferTr.UserAddress
            });
        }
    }
}
