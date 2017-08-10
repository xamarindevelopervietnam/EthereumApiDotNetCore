using Core;
using Core.Exceptions;
using Core.Repositories;
using Core.Settings;
using Core.Utils;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Services
{
    public interface IDepositContractService
    {
        Task<string> CreateDepositContractTrHashWithoutUserAsync();

        Task<string> GetUserAddressForDepositContract(string depositContractAddress);

        Task<string> SetUserAddressForDepositContract(string userAddress, string depositContractAddress);

        Task<IDepositContract> GetDepositContract(string userAddress);
        Task<BigInteger> GetBalance(string depositContractAddress, string coinAdapterAddress);
        Task<string> RecievePaymentFromTransferContract(string depositContractAddress, string coinAdapterAddress, BigInteger balance);
    }

    public class DepositContractService : IDepositContractService
    {
        private readonly ICoinRepository _coinRepository;
        private readonly IContractService _contractService;
        private readonly IBaseSettings _settings;
        private readonly IDepositContractRepository _depositContractRepository;
        private readonly ITransferContractQueueServiceFactory _transferContractQueueServiceFactory;
        private readonly ITransferContractUserAssignmentQueueService _transferContractUserAssignmentQueueService;
        private readonly Web3 _web3;
        private readonly IPaymentService _paymentService;
        private readonly IErcInterfaceService _ercInterfaceService;

        public DepositContractService(IContractService contractService,
            IDepositContractRepository depositContractRepository,
            IBaseSettings settings,
            ITransferContractQueueServiceFactory transferContractQueueServiceFactory,
            ITransferContractUserAssignmentQueueService transferContractUserAssignmentQueueService,
            IPaymentService paymentService,
            Web3 web3)
        {
            _paymentService = paymentService;
            _web3 = web3;
            _contractService = contractService;
            _depositContractRepository = depositContractRepository;
            _settings = settings;
            _transferContractQueueServiceFactory = transferContractQueueServiceFactory;
            _transferContractUserAssignmentQueueService = transferContractUserAssignmentQueueService;
        }

        public async Task<string> CreateDepositContractTrHashWithoutUserAsync()
        {
            string transactionHash;

            transactionHash =
                   await _contractService.CreateContractWithoutBlockchainAcceptance(_settings.DepositContract.Abi,
                   _settings.DepositContract.ByteCode, _settings.DepositAdminContract.Address);

            return transactionHash;
        }

        public async Task<string> GetUserAddressForDepositContract(string depositContractAddress)
        {
            IDepositContract depositContract = await _depositContractRepository.GetByAddressAsync(depositContractAddress);

            if (depositContract == null)
            {
                throw new ClientSideException(ExceptionType.WrongParams, $"Transfer contract with address {depositContractAddress} does not exist");
            }

            string adminAbi = _settings.DepositAdminContract.Abi;

            var contract = _web3.Eth.GetContract(adminAbi, _settings.DepositAdminContract.Address);
            var function = contract.GetFunction("getDepositContractUser");
            string userAddress =
                await function.CallAsync<string>(depositContractAddress);
            //depositContract.UserAddress = userAddress;
            //await _depositContractRepository.SaveAsync(depositContract);

            return userAddress;
        }

        public async Task<string> SetUserAddressForDepositContract(string userAddress, string depositContractAddress)
        {
            IDepositContract depositContract = await _depositContractRepository.GetByAddressAsync(depositContractAddress);

            if (depositContract == null)
            {
                throw new ClientSideException(ExceptionType.WrongParams, $"Transfer contract with address {depositContractAddress} does not exist");
            }

            string adminAbi = _settings.DepositAdminContract.Abi;

            var contract = _web3.Eth.GetContract(adminAbi, _settings.DepositAdminContract.Address);
            var function = contract.GetFunction("addDepositContractUser");
            //function setTransferAddressUser(address userAddress, address transferAddress) onlyowner{
            string transaction =
                await function.SendTransactionAsync(_settings.EthereumMainAccount, depositContractAddress, userAddress);
            depositContract.UserAddress = userAddress;

            await _depositContractRepository.SaveAsync(depositContract);

            return transaction;
        }

        public async Task<IDepositContract> GetDepositContract(string userAddress)
        {
            IDepositContract contract = await _depositContractRepository.GetByUserAsync(userAddress);

            return contract;
        }

        public async Task<BigInteger> GetBalance(string depositContractAddress, string coinAdapterAddress)
        {
            ICoin coinDb = await _coinRepository.GetCoinByAddress(coinAdapterAddress);
            BigInteger balance;
            if (coinDb.ContainsEth)
            {
                balance = await _paymentService.GetAddressBalanceInWei(depositContractAddress);
            }
            else
            {
                balance = await _ercInterfaceService.GetBalanceForExternalTokenAsync(depositContractAddress, coinDb.ExternalTokenAddress);
            }

            return balance;
        }

        public async Task<string> RecievePaymentFromTransferContract(string depositContractAddress, string coinAdapterAddress, BigInteger balance)
        {
            ICoin coinDb = await _coinRepository.GetCoinByAddress(coinAdapterAddress);

            if (!coinDb.BlockchainDepositEnabled)
                throw new Exception("Coin must be payable");

            Contract contract = _web3.Eth.GetContract(_settings.DepositContract.Abi, depositContractAddress);
            Function cashinFunction;
            object[] cashinParameters;
            string transactionHash;
            //take a look at depositContract.sol to understand how cashin works
            if (coinDb.ContainsEth)
            {
                if (coinAdapterAddress.ToLower() == _settings.EthereumAdapterAddress.ToLower())
                {
                    cashinFunction = contract.GetFunction("cashin");
                    cashinParameters = new object[] { _settings.EthereumAdapterAddress, balance };
                }
                else
                {
                    cashinFunction = contract.GetFunction("cashinEth");
                    cashinParameters = new object[] { coinAdapterAddress, balance };
                }
            }
            else
            {
                string erc20TokenAddress = coinDb.ExternalTokenAddress;
                cashinFunction = contract.GetFunction("cashinTokens");
                cashinParameters = new object[] { erc20TokenAddress, coinAdapterAddress, balance };
            }

            transactionHash = await cashinFunction.SendTransactionAsync(_settings.EthereumMainAccount,
                    new HexBigInteger(Constants.GasForCoinTransaction), new HexBigInteger(0), cashinParameters);

            return transactionHash;
        }
    }
}
