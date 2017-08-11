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
    public class MonitoringDepositContractsJob
    {
        private readonly ILog _logger;
        private readonly IPaymentService _paymentService;
        private readonly IDepositContractRepository _depositContractRepository;
        private readonly IBaseSettings _settings;
        private readonly IErcInterfaceService _ercInterfaceService;
        private readonly IUserPaymentRepository _userPaymentRepository;
        private readonly IDepositContractService _depositContractService;
        private readonly IUserDepositWalletRepository _userDepositWalletRepository;
        private readonly IDepositContractTransactionService _depositContractTransactionService;
        private readonly IEthereumTransactionService _ethereumTransactionService;
        private readonly AddressUtil _util;
        private readonly ITransferContractUserAssignmentQueueService _transferContractUserAssignmentQueueService;
        private readonly IErc20ContractRepository _erc20ContractRepository;
        private readonly IErc20BalanceService _erc20BalanceService;
        private readonly ICoinRepository _coinRepository;

        public MonitoringDepositContractsJob(IBaseSettings settings,
            IErcInterfaceService ercInterfaceService,
            IDepositContractRepository depositContractRepository,
            ILog logger,
            IPaymentService paymentService,
            IUserPaymentRepository userPaymentRepository,
            IDepositContractService depositContractService,
            IUserDepositWalletRepository userDepositWalletRepository,
            IDepositContractTransactionService depositContractTransactionService,
            IEthereumTransactionService ethereumTransactionService,
            IErc20ContractRepository erc20ContractRepository,
            IErc20BalanceService erc20BalanceService,
            ICoinRepository coinRepository
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
            _depositContractTransactionService = depositContractTransactionService;
            _erc20ContractRepository = erc20ContractRepository;
            _erc20BalanceService = erc20BalanceService;
            _coinRepository = coinRepository;
        }

        [TimerTrigger("0.00:03:00")]
        public async Task Execute()
        {
            IEnumerable<IErc20Contract> supportedTokens = await _erc20ContractRepository.GetAllAsync();
            IEnumerable<ICoin> allAdapters = await _coinRepository.GetAll();
            IEnumerable<string> supportedTokenAddresses = supportedTokens.Select(x => x.TokenAddress);
            Dictionary<string, ICoin> tokenAdapterDict = allAdapters.Where(x => !string.IsNullOrEmpty(x.ExternalTokenAddress))
                .ToDictionary(x => x.ExternalTokenAddress);
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

                        #region CheckErc20Balance

                        IEnumerable<AddressTokenBalance> addressBalances = 
                        await _erc20BalanceService.GetBalancesForAddress(item.ContractAddress, supportedTokenAddresses);
                        IEnumerable<AddressTokenBalance> addressBalancesWithTokens = addressBalances.Where(x => x.Balance > 0);
                        foreach (var addressBalance in addressBalancesWithTokens)
                        {
                            BigInteger balance = addressBalance.Balance;

                            if (balance > 0)
                            {
                                ICoin coinAdapter = null;
                                tokenAdapterDict.TryGetValue(addressBalance.Erc20TokenAddress, out coinAdapter);
                                if (coinAdapter == null)
                                {
                                    await _logger.WriteInfoAsync("MonitoringDepositContracts", "Execute", "",
                                        $"There is no adapter for erc20 Token {addressBalance.Erc20TokenAddress}", DateTime.UtcNow);

                                    continue;
                                }

                                string coinAdapterAddress = coinAdapter.AdapterAddress;
                                IUserDepositWallet wallet = await _userDepositWalletRepository.
                                GetUserContractAsync(item.UserAddress, item.ContractAddress, coinAdapterAddress);
                                if (wallet == null ||
                                    string.IsNullOrEmpty(wallet.LastBalance) ||
                                    wallet.LastBalance == "0")
                                {
                                    await _userDepositWalletRepository.ReplaceAsync(new UserDepositWallet()
                                    {
                                        LastBalance = balance.ToString(),
                                        CoinAdapterAddress = coinAdapterAddress,
                                        DepositContractAddress = item.ContractAddress,
                                        UserAddress = item.UserAddress,
                                        UpdateDate = DateTime.UtcNow
                                    });

                                    await _depositContractTransactionService.PutContractTransferTransaction(new DepositContractTransaction()
                                    {
                                        Amount = balance.ToString(),
                                        UserAddress = item.UserAddress,
                                        DepositContractAddress = item.ContractAddress,
                                        CoinAdapterAddress = coinAdapterAddress,
                                        CreateDt = DateTime.UtcNow
                                    });

                                    await _logger.WriteInfoAsync("MonitoringDepositContracts", "Execute", "", $"Balance on deposit contract address - {item.ContractAddress}" +
                                        $" for adapter contract {coinAdapterAddress} is {balance}" +
                                        $" transfer belongs to user {item.UserAddress}", DateTime.UtcNow);
                                }
                            }
                        }

                        #endregion

                        #region CheckEthBalance

                        string ethereumAdapterAddress = _settings.EthereumAdapterAddress;
                        IUserDepositWallet ethWallet = await _userDepositWalletRepository.
                                GetUserContractAsync(item.UserAddress, item.ContractAddress, ethereumAdapterAddress);
                        if (ethWallet == null ||
                            string.IsNullOrEmpty(ethWallet.LastBalance) ||
                            ethWallet.LastBalance == "0")
                        {
                            var ethBalance = _paymentService.GetAddressBalanceInWei(item.ContractAddress);
                            await _userDepositWalletRepository.ReplaceAsync(new UserDepositWallet()
                            {
                                LastBalance = ethBalance.ToString(),
                                CoinAdapterAddress = ethereumAdapterAddress,
                                DepositContractAddress = item.ContractAddress,
                                UserAddress = item.UserAddress,
                                UpdateDate = DateTime.UtcNow
                            });

                            await _depositContractTransactionService.PutContractTransferTransaction(new DepositContractTransaction()
                            {
                                Amount = ethBalance.ToString(),
                                UserAddress = item.UserAddress,
                                DepositContractAddress = item.ContractAddress,
                                CoinAdapterAddress = ethereumAdapterAddress,
                                CreateDt = DateTime.UtcNow
                            });

                            await _logger.WriteInfoAsync("MonitoringDepositContracts", "Execute", "", $"Balance on deposit contract address - {item.ContractAddress}" +
                                $" for adapter contract {ethereumAdapterAddress} is {ethBalance}" +
                                $" transfer belongs to user {item.UserAddress}", DateTime.UtcNow);
                        }
                    }

                    #endregion
                }
                catch (Exception e)
                {
                    await _logger.WriteErrorAsync("MonitoringDepositContracts", "Execute", "", e, DateTime.UtcNow);
                }
            });
        }
    }
}
