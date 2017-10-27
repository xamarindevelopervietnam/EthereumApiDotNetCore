﻿using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Common.Log;
using Core;
using Core.Repositories;
using Core.Settings;
using Lykke.JobTriggers.Triggers.Attributes;
using Nethereum.Util;
using Services;
using Services.Erc20;
using AzureStorage.Queue;
using Core.Notifiers;
using Newtonsoft.Json;

namespace EthereumJobs.Job.DepositJobs
{
    public class MonitoringDepositContractsJob
    {
        private readonly ICoinRepository                    _coinRepository;
        private readonly IDepositContractRepository         _depositContractRepository;
        private readonly IDepositContractService            _depositContractService;
        private readonly IDepositContractTransactionService _depositContractTransactionService;
        private readonly IErc20BalanceService               _erc20BalanceService;
        private readonly IErc20ContractRepository           _erc20ContractRepository;
        private readonly IEthereumTransactionService        _ethereumTransactionService;
        private readonly ILog                               _logger;
        private readonly IPaymentService                    _paymentService;
        private readonly IBaseSettings                      _settings;
        private readonly IUserDepositWalletRepository       _userDepositWalletRepository;
        private readonly IQueueExt                          _queueUserAssignment;
        private readonly ISlackNotifier                     _slackNotifier;
        private readonly IUserAssignmentFailRepository      _userAssignmentFailRepository;

        public MonitoringDepositContractsJob(
            ICoinRepository coinRepository,
            IDepositContractRepository depositContractRepository,
            IDepositContractService depositContractService,
            IDepositContractTransactionService depositContractTransactionService,
            IErc20BalanceService erc20BalanceService,
            IErc20ContractRepository erc20ContractRepository,
            IEthereumTransactionService ethereumTransactionService,
            ILog logger,
            IPaymentService paymentService,
            IBaseSettings settings,
            IUserDepositWalletRepository userDepositWalletRepository,
            IQueueFactory queueFactory,
            ISlackNotifier slackNotifier,
            IUserAssignmentFailRepository userAssignmentFailRepository)
        {
            _ethereumTransactionService        = ethereumTransactionService;
            _settings                          = settings;
            _depositContractRepository         = depositContractRepository;
            _logger                            = logger;
            _paymentService                    = paymentService;
            _depositContractService            = depositContractService;
            _userDepositWalletRepository       = userDepositWalletRepository;
            _depositContractTransactionService = depositContractTransactionService;
            _erc20ContractRepository           = erc20ContractRepository;
            _erc20BalanceService               = erc20BalanceService;
            _coinRepository                    = coinRepository;
            _queueUserAssignment               = queueFactory.Build(Constants.DepositContractsAssignmentQueue);
            _slackNotifier                     = slackNotifier;
            _userAssignmentFailRepository      = userAssignmentFailRepository;
        }

        [TimerTrigger("0.00:03:00")]
        public async Task Execute()
        {
            var supportedTokens = (await _erc20ContractRepository.GetAllAsync())
                .Select(x => x.TokenAddress)
                .ToList();

            var tokenAdapters = (await _coinRepository.GetAll())
                .Where(x => !string.IsNullOrEmpty(x.ExternalTokenAddress))
                .ToDictionary(x => x.ExternalTokenAddress);

            await _depositContractRepository.ProcessAllAsync(async depositContract =>
            {
                try
                {
                    string depositContractAddress = depositContract.ContractAddress;
                    //Check that transfer contract assigned to user
                    if (!string.IsNullOrEmpty(depositContract.UserAddress))
                    {
                        // Check, if assignment completed
                        var userAddress = await _depositContractService.GetUserAddressForDepositContract(depositContractAddress);
                        var userAddressIsNullOrEmpty = string.IsNullOrEmpty(userAddress) || userAddress == Constants.EmptyEthereumAddress;

                        if (!userAddressIsNullOrEmpty || await CheckIfAssignmentCompleted(depositContract))
                        {
                            // Check erc20 tokens balances
                            var tokenBalances = await _erc20BalanceService.GetBalancesForAddress(depositContract.ContractAddress, supportedTokens);
                            if (tokenBalances != null)
                            {
                                foreach (var tokenBalance in tokenBalances.Where(x => x.Balance > 0))
                                {
                                    tokenAdapters.TryGetValue(tokenBalance.Erc20TokenAddress, out ICoin tokenAdapter);

                                    if (tokenAdapter != null)
                                    {
                                        await CheckBalance(depositContract, tokenAdapter.AdapterAddress, tokenBalance.Balance);
                                    }
                                    else
                                    {
                                        await LogInfo($"There is no adapter for erc20 token {tokenBalance.Erc20TokenAddress}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            await UpdateUserAssignmentFail(depositContractAddress, depositContract.UserAddress);
                            var errorMessage = $"User assignment was not completed for {depositContract.UserAddress} (contractAddress::{depositContract.ContractAddress}, trHash: {depositContract.AssignmentHash})";

                            await LogError(new Exception(errorMessage));
                        }

                        //Error here
                        var userAddressForLegacyAdapter = await _depositContractService.GetUserAddressForLegacyAdapterAsync(depositContractAddress);
                        var userAddressForLegacyAdapterIsNullOrEmpty = string.IsNullOrEmpty(userAddressForLegacyAdapter)
                        || userAddressForLegacyAdapter == Constants.EmptyEthereumAddress;
                        if (!string.IsNullOrEmpty(userAddressForLegacyAdapter) || userAddress != Constants.EmptyEthereumAddress)
                        {
                            // Check ethereum balance
                            await CheckBalance
                           (
                               depositContract,
                               _settings.EthereumAdapterAddress,
                               await _paymentService.GetAddressBalanceInWei(depositContract.ContractAddress)
                           );
                        }
                        else
                        {
                            await UpdateUserAssignmentFail(depositContractAddress, depositContract.UserAddress);
                            var errorMessage = $"(Legacy ETH adapter)User assignment was not completed for {depositContract.UserAddress} (contractAddress::{depositContract.ContractAddress}, trHash: {depositContract.LegacyEthAdapterAssignmentHash})";

                            await LogError(new Exception(errorMessage));
                        }
                    }
                }
                catch (Exception e)
                {
                    await LogError(e);
                }
            });
        }

        private async Task<bool> CheckIfAssignmentCompleted(IDepositContract depositContract)
        {
            return await CheckIfAssignmentCompleted(depositContract.AssignmentHash);
        }

        private async Task<bool> CheckIfAssignmentCompleted(string assignmentTrHash)
        {
            var assignmentCompleted = false;

            if (!string.IsNullOrEmpty(assignmentTrHash))
            {
                assignmentCompleted = await _ethereumTransactionService.IsTransactionExecuted
                (
                    assignmentTrHash,
                    Constants.GasForCoinTransaction
                );
            }

            return assignmentCompleted;
        }

        public async Task UpdateUserAssignmentFail(string contractAddress, string userAddress)
        {
            var canBeRestoredInternally = !string.IsNullOrEmpty(userAddress) && userAddress != Constants.EmptyEthereumAddress;

            var userAssignmentFail = await _userAssignmentFailRepository.GetAsync(contractAddress);

            if (userAssignmentFail == null)
            {
                userAssignmentFail = new UserAssignmentFail()
                {
                    CanBeRestoredInternally = canBeRestoredInternally,
                    ContractAddress = contractAddress,
                    FailCount = 0
                };
            }

            if (userAssignmentFail.FailCount == Constants.AttempsBeforeUserReassign)
            {
                if (canBeRestoredInternally)
                {
                    var message = new DepositContractUserAssignment()
                    {
                        DepositContractAddress = contractAddress,
                        UserAddress = userAddress,
                    };

                    await _queueUserAssignment.PutRawMessageAsync(JsonConvert.SerializeObject(message));
                    userAssignmentFail.FailCount = 0;
                }
                else
                {
                    await _slackNotifier.ErrorAsync($"DepositContract - {contractAddress}, UserAddress - {userAddress} can't be restored internally");

                    return;
                }
            }
            else
            {
                userAssignmentFail.FailCount++;
            }

            await _userAssignmentFailRepository.SaveAsync(userAssignmentFail);
        }

        private async Task CheckBalance(IDepositContract depositContract, string adapterAddress, BigInteger balance)
        {
            var wallet = await GetUserDepositWallet(depositContract, adapterAddress);

            if (string.IsNullOrEmpty(wallet?.LastBalance) || wallet.LastBalance == "0")
            {
                await _userDepositWalletRepository.ReplaceAsync
                (
                    new UserDepositWallet
                    {
                        LastBalance = balance.ToString(),
                        CoinAdapterAddress = adapterAddress,
                        DepositContractAddress = depositContract.ContractAddress,
                        UserAddress = depositContract.UserAddress,
                        UpdateDate = DateTime.UtcNow
                    }
                );

                await _depositContractTransactionService.PutContractTransferTransaction
                (
                    new DepositContractTransaction
                    {
                        Amount = balance.ToString(), //-V3086
                        UserAddress = depositContract.UserAddress,
                        DepositContractAddress = depositContract.ContractAddress,
                        CoinAdapterAddress = adapterAddress,
                        CreateDt = DateTime.UtcNow
                    }
                );

                await LogInfo
                (
                    $"Balance on deposit contract address - {depositContract.ContractAddress} " +
                    $"for adapter contract {adapterAddress} is {balance} " +
                    $"transfer belongs to user {depositContract.UserAddress}"
                );
            }
        }

        private async Task<IUserDepositWallet> GetUserDepositWallet(IDepositContract depositContract, string adapterAddress)
        {
            return await _userDepositWalletRepository.GetUserContractAsync
            (
                depositContract.UserAddress,
                depositContract.ContractAddress,
                adapterAddress
            );
        }

        private async Task LogError(Exception e)
        {
            await _logger.WriteErrorAsync
            (
                "MonitoringDepositContracts",
                "Execute",
                "",
                e,
                DateTime.UtcNow
            );
        }

        private async Task LogInfo(string message)
        {
            await _logger.WriteInfoAsync
            (
                "MonitoringDepositContracts",
                "Execute",
                "",
                message,
                DateTime.UtcNow
            );
        }

        private async Task LogWarning(string message)
        {
            await _logger.WriteWarningAsync
            (
                "MonitoringDepositContracts",
                "Execute",
                "",
                message,
                DateTime.UtcNow
            );
        }
    }
}