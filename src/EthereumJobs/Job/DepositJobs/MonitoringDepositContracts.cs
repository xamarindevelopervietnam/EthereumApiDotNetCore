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
using Nethereum.Util;
using System.Collections.Generic;
using Services.Erc20;
using System.Linq;
using BusinessModels.Erc20;

namespace EthereumJobs.Job
{
    public class MonitoringDepositContracts
    {
        private readonly ILog _logger;
        private readonly IPaymentService _paymentService;
        private readonly IDepositContractRepository _depositContractRepository;
        private readonly IBaseSettings _settings;
        private readonly IErcInterfaceService _ercInterfaceService;
        private readonly IUserPaymentRepository _userPaymentRepository;
        private readonly IDepositContractService _depositContractService;
        private readonly IUserDepositWalletRepository _userDepositWalletRepository;
        private readonly ITransferContractTransactionService _transferContractTransactionService;
        private readonly IEthereumTransactionService _ethereumTransactionService;
        private readonly AddressUtil _util;
        private readonly ITransferContractUserAssignmentQueueService _transferContractUserAssignmentQueueService;
        private readonly IErc20ContractRepository _erc20ContractRepository;
        private readonly IErc20BalanceService _erc20BalanceService;

        public MonitoringDepositContracts(IBaseSettings settings,
            IErcInterfaceService ercInterfaceService,
            IDepositContractRepository depositContractRepository,
            ILog logger,
            IPaymentService paymentService,
            IUserPaymentRepository userPaymentRepository,
            IDepositContractService depositContractService,
            IUserDepositWalletRepository userDepositWalletRepository,
            ITransferContractTransactionService transferContractTransactionService,
            IEthereumTransactionService ethereumTransactionService,
            IErc20ContractRepository erc20ContractRepository,
            IErc20BalanceService erc20BalanceService
            )
        {
            _util = new AddressUtil();
            _ethereumTransactionService = ethereumTransactionService;
            _ercInterfaceService = ercInterfaceService;
            _settings = settings;
            _depositContractRepository = depositContractRepository;
            _logger = logger;
            _paymentService = paymentService;
            _userPaymentRepository = userPaymentRepository;
            _depositContractService = depositContractService;
            _userDepositWalletRepository = userDepositWalletRepository;
            _transferContractTransactionService = transferContractTransactionService;
            _erc20ContractRepository = erc20ContractRepository;
            _erc20BalanceService = erc20BalanceService;
        }

        [TimerTrigger("0.00:03:00")]
        public async Task Execute()
        {
            IEnumerable<IErc20Contract> supportedTokens = await _erc20ContractRepository.GetAllAsync();
            IEnumerable<string> supportedTokenAddresses = supportedTokens.Select(x => x.TokenAddress);
            await _depositContractRepository.ProcessAllAsync(async (item) =>
            {
                try
                {
                    //Check that transfer contract assigned to user
                    if (!string.IsNullOrEmpty(item.UserAddress))
                    {
                        var userAddress = await _depositContractService.GetUserAddressForDepositContract(item.ContractAddress);
                        if (string.IsNullOrEmpty(userAddress) || userAddress == Constants.EmptyEthereumAddress)
                        {
                            bool assignmentCompleted = false;
                            if (!string.IsNullOrEmpty(item.AssignmentHash))
                            {
                                assignmentCompleted = await _ethereumTransactionService.IsTransactionExecuted(item.AssignmentHash, Constants.GasForCoinTransaction);
                            }
                            if (!assignmentCompleted)
                            {
                                await _logger.WriteWarningAsync("MonitoringDepositContracts", "Execute", $"User assignment was not completed for {item.UserAddress} " +
                                    $"(contractAddress::{ item.ContractAddress}, trHash: { item.AssignmentHash})", "", DateTime.UtcNow);
                                throw new Exception($"User assignment was not completed for {item.UserAddress} (contractAddress::{item.ContractAddress}, trHash: {item.AssignmentHash})");
                            }
                        }

                        IEnumerable<AddressTokenBalance> addressBalances = await _erc20BalanceService.GetBalancesForAddress(item.ContractAddress, supportedTokenAddresses);
                        IEnumerable<AddressTokenBalance> addressBalancesWithTokens = addressBalances.Where(x => x.Balance > 0);
                        foreach (var addressBalance in addressBalancesWithTokens)
                        {
                            IUserDepositWallet wallet = await _userDepositWalletRepository.GetUserContractAsync(item.UserAddress, item.ContractAddress, item.c);
                            if (wallet == null ||
                                string.IsNullOrEmpty(wallet.LastBalance) ||
                                wallet.LastBalance == "0")
                            {
                                BigInteger balance = await addressBalance.Balance;

                                if (balance > 0)
                                {
                                    await _userDepositWalletRepository.ReplaceAsync(new UserTransferWallet()
                                    {
                                        LastBalance = balance.ToString(),
                                        TransferContractAddress = item.ContractAddress,
                                        UserAddress = item.UserAddress,
                                        UpdateDate = DateTime.UtcNow
                                    });

                                    await _transferContractTransactionService.PutContractTransferTransaction(new TransferContractTransaction()
                                    {
                                        Amount = balance.ToString(),
                                        UserAddress = item.UserAddress,
                                        CoinAdapterAddress = item.CoinAdapterAddress,
                                        ContractAddress = item.ContractAddress,
                                        CreateDt = DateTime.UtcNow
                                    });

                                    string currency = item.ContainsEth ? "Wei" : "Tokens";
                                    await _logger.WriteInfoAsync("MonitoringDepositContracts", "Execute", "", $"Balance on deposit contract address - {item.ContractAddress}" +
                                        $" for adapter contract {item.CoinAdapterAddress} is {balance} ({currency})" +
                                        $" transfer belongs to user {item.UserAddress}", DateTime.UtcNow);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    await _logger.WriteErrorAsync(nameof(MonitoringDepositContracts), nameof(Execute), "", e, DateTime.UtcNow);
                }
            });
        }
    }
}
