using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Core;
using Core.Repositories;
using Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.ABI.Encoders;
using Nethereum.ABI.Util;
using Services;
using Services.Coins;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using Core.Utils;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Services.Coins.Models;
using AzureStorage.Queue;
using Nethereum.Util;
using Nethereum.Signer;
using System.Diagnostics;

namespace Tests
{
    //Todo: put tests on separate tables
    //Warning: tests consumes ethereum on mainAccount. Run on testnet only!
    [TestClass]
    public class DepositAdminContractTest : BaseTest
    {
        private ITransferContractService _transferContractService;
        private IEthereumTransactionService _transactionService;
        private IBaseSettings _settings;
        private IDepositContractUserAssignmentQueueService _depositContractUserAssignmentQueueService;
        private IDepositContractService _depositContractService;

        [TestInitialize]
        public void Init()
        {
            _settings = Config.Services.GetService<IBaseSettings>();
            _transactionService = Config.Services.GetService<IEthereumTransactionService>();
            _transferContractService = Config.Services.GetService<ITransferContractService>();
            _depositContractUserAssignmentQueueService = Config.Services.GetService<IDepositContractUserAssignmentQueueService>();
            _depositContractService = Config.Services.GetService<IDepositContractService>();
        }

        #region TokenAdapter

        [TestMethod]
        public async Task TestTransferTokens()
        {
            string userAddress = _clientA;
            string depositAddress = "0x1e8e8ccbd9a7a8d82875054aa8342159d96356a9";

            string trHash = await _depositContractUserAssignmentQueueService.SetUserAddressForDepositContract(userAddress, depositAddress);
            while (!await _transactionService.IsTransactionExecuted(trHash, Constants.GasForCoinTransaction))
            {
                await Task.Delay(400);
            }

            string assignedUser = await _depositContractService.GetUserAddressForDepositContract(depositAddress);
            Assert.IsTrue(assignedUser.ToLower() == userAddress.ToLower());
        }

        #endregion
    }
}
