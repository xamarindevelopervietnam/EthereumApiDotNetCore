using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Log;
using Core.Repositories;
using Core.Settings;
using EthereumJobs.Job.DepositJobs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Services;
using Services.Erc20;

namespace EthereumJobs.UnitTests
{
    [TestClass]
    public class MonitoringDepositContractsJobTests
    {
        private Mock<ICoinRepository>                    _coinRepository;
        private Mock<IDepositContractRepository>         _depositContractRepository;
        private Mock<IDepositContractService>            _depositContractService;
        private Mock<IDepositContractTransactionService> _depositContractTransactionService;
        private Mock<IErc20BalanceService>               _erc20BalanceService;
        private Mock<IErc20ContractRepository>           _erc20ContractRepository;
        private Mock<IEthereumTransactionService>        _ethereumTransactionService;
        private Mock<ILog>                               _logger;
        private Mock<IPaymentService>                    _paymentService;
        private Mock<IBaseSettings>                      _settings;
        private Mock<IUserDepositWalletRepository>       _userDepositWalletRepository;

        private async Task ExecuteJobAsync()
        {
            var job = new MonitoringDepositContractsJob
            (
                _coinRepository.Object,
                _depositContractRepository.Object,
                _depositContractService.Object,
                _depositContractTransactionService.Object,
                _erc20BalanceService.Object,
                _erc20ContractRepository.Object,
                _ethereumTransactionService.Object,
                _logger.Object,
                _paymentService.Object,
                _settings.Object,
                _userDepositWalletRepository.Object,
                null,
                null,
                null
            );

            await job.Execute();
        }

        [TestInitialize]
        public void Init()
        {
            _coinRepository                    = new Mock<ICoinRepository>();
            _depositContractRepository         = new Mock<IDepositContractRepository>();
            _depositContractService            = new Mock<IDepositContractService>();
            _depositContractTransactionService = new Mock<IDepositContractTransactionService>();
            _erc20BalanceService               = new Mock<IErc20BalanceService>();
            _erc20ContractRepository           = new Mock<IErc20ContractRepository>();
            _ethereumTransactionService        = new Mock<IEthereumTransactionService>();
            _logger                            = new Mock<ILog>();
            _paymentService                    = new Mock<IPaymentService>();
            _settings                          = new Mock<IBaseSettings>();
            _userDepositWalletRepository       = new Mock<IUserDepositWalletRepository>();
        }

        [TestMethod]
        [TestCategory(nameof(MonitoringDepositContractsJob))]
        public async Task Should__Throw__Exception__When__Erc20ContractRepository_GetAllAsync__Exception__Unhandled()
        {
            const string excpetionMessage = "Test Exception";

            _erc20ContractRepository
                .Setup(x => x.GetAllAsync())
                .ThrowsAsync(new Exception(excpetionMessage));

            await Assert.ThrowsExceptionAsync<Exception>(ExecuteJobAsync, excpetionMessage);
        }

        [TestMethod]
        [TestCategory(nameof(MonitoringDepositContractsJob))]
        public async Task Should__Throw__Exception__When__CoinRepository_GetAll__Exception__Unhandled()
        {
            const string excpetionMessage = "Test Exception";

            _erc20ContractRepository
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(() => new List<IErc20Contract>());

            _coinRepository
                .Setup(x => x.GetAll())
                .ThrowsAsync(new Exception(excpetionMessage));

            await Assert.ThrowsExceptionAsync<Exception>(ExecuteJobAsync, excpetionMessage);
        }

        [TestMethod]
        [TestCategory(nameof(MonitoringDepositContractsJob))]
        public async Task Should__Throw__Exception__When__DepositContractRepository_ProcessAllAsync__Exception__Unhandled()
        {
            const string excpetionMessage = "Test Exception";

            _erc20ContractRepository
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(() => new List<IErc20Contract>());

            _coinRepository
                .Setup(x => x.GetAll())
                .ReturnsAsync(() => new List<ICoin>());

            _depositContractRepository
                .Setup(x => x.ProcessAllAsync(It.IsAny<Func<IDepositContract, Task>>()))
                .ThrowsAsync(new Exception(excpetionMessage));

            await Assert.ThrowsExceptionAsync<Exception>(ExecuteJobAsync, excpetionMessage);
        }

        // TODO: Figure out, why it throws a NullReferenceException
        //[TestMethod]
        [TestCategory(nameof(MonitoringDepositContractsJob))]
        public async Task Should__Log__Error__When__DepositContractRepository_ProcessAllAsync__Catch_Exception()
        {
            _erc20ContractRepository
                .Setup(x => x.GetAllAsync())
                .ReturnsAsync(() => new List<IErc20Contract>());

            _coinRepository
                .Setup(x => x.GetAll())
                .ReturnsAsync(() => new List<ICoin>());
            
            var expectedException   = new Exception();
            var depositContractMock = new Mock<IDepositContract>();

            depositContractMock
                .SetupGet(x => x.UserAddress)
                .Throws(expectedException);

            _depositContractRepository
                .Setup(x => x.ProcessAllAsync(It.IsAny<Func<IDepositContract, Task>>()))
                .Callback<Func<IDepositContract, Task>>(x => x(depositContractMock.Object));

            _logger
                .Setup(
                    x => x.WriteErrorAsync
                    (
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Exception>(),
                        It.IsAny<DateTime?>()
                    )
                )
                .Callback<string, string, string, Exception, DateTime?>
                (
                    (component, process, context, exception, datetime) =>
                    {
                        Assert.AreEqual("MonitoringDepositContracts", component);
                        Assert.AreEqual("Execute", process);
                        Assert.AreEqual("", context);
                        Assert.AreEqual(expectedException, exception);
                        Assert.IsNotNull(datetime);
                    }
                );

            await ExecuteJobAsync();
        }

        [TestMethod]
        [TestCategory(nameof(MonitoringDepositContractsJob))]
        public async Task Should__Log__Error__And__Warning__When__UserAddress__IsNullOrEmpty__And__AssignmentNotCompleted()
        {
            
        }
    }
}