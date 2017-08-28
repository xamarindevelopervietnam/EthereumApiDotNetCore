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
        Task<string> AssignDepositContractToUserAsync(string userAddress);

        Task<string> CreateDepositContractTrHashWithoutUserAsync();

        Task<string> GetUserAddressForDepositContract(string depositContractAddress);
        Task<string> GetUserAddressForLegacyAdapterAsync(string depositContractAddress);

        Task<IDepositContract> GetDepositContract(string userAddress);
        Task<BigInteger> GetBalance(string depositContractAddress, string coinAdapterAddress);
        Task<BigInteger> GetBalanceOnAdapter(string adapterAddress, string clientAddress);
        Task<string> RecievePaymentFromTransferContract(string depositContractAddress, string coinAdapterAddress);
        Task<string> ChangeDepositAdminContract(string contractWithDependencyOnAdmin, string newAdminContract);
    }

    public class DepositContractService : IDepositContractService
    {
        private readonly ICoinRepository _coinRepository;
        private readonly IContractService _contractService;
        private readonly IBaseSettings _settings;
        private readonly IDepositContractRepository _depositContractRepository;
        private readonly IDepositContractUserAssignmentQueueService _depositContractUserAssignmentQueueService;
        private readonly Web3 _web3;
        private readonly IPaymentService _paymentService;
        private readonly IErcInterfaceService _ercInterfaceService;
        private readonly IDepositContractQueueService _depositContractQueueService;

        public DepositContractService(IContractService contractService,
            IDepositContractRepository depositContractRepository,
            IBaseSettings settings,
            IDepositContractUserAssignmentQueueService depositContractUserAssignmentQueueService,
            IPaymentService paymentService,
            Web3 web3,
            IDepositContractQueueService depositContractQueueService,
            ICoinRepository coinRepository,
            IErcInterfaceService ercInterfaceService)
        {
            _paymentService = paymentService;
            _web3 = web3;
            _contractService = contractService;
            _depositContractRepository = depositContractRepository;
            _settings = settings;
            _depositContractUserAssignmentQueueService = depositContractUserAssignmentQueueService;
            _depositContractQueueService = depositContractQueueService;
            _coinRepository = coinRepository;
            _ercInterfaceService = ercInterfaceService;
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
            IDepositContract depositContract = await GetDepositContractWithCheckAsync(depositContractAddress);

            string adminAbi = _settings.DepositAdminContract.Abi;

            var contract = _web3.Eth.GetContract(adminAbi, _settings.DepositAdminContract.Address);
            var function = contract.GetFunction("getDepositContractUser");
            string userAddress =
                await function.CallAsync<string>(depositContractAddress);

            return userAddress;
        }

        public async Task<string> GetUserAddressForLegacyAdapterAsync(string depositContractAddress)
        {
            IDepositContract depositContract = await _depositContractRepository.GetByAddressAsync(depositContractAddress);

            if (depositContract == null)
            {
                throw new ClientSideException(ExceptionType.WrongParams, $"Deposit contract with address {depositContractAddress} does not exist");
            }

            string ethAdapterAddress = depositContract.EthAdapterAddress;
            ICoin coin = await _coinRepository.GetCoinByAddress(ethAdapterAddress);

            if (coin == null)
            {
                throw new ClientSideException(ExceptionType.WrongParams, $"Coin with address {ethAdapterAddress} does not exist");
            }

            string coinAbi = _settings.CoinAbi;

            var contract = _web3.Eth.GetContract(coinAbi, ethAdapterAddress);
            var function = contract.GetFunction("getTransferAddressUser");
            string userAddress =
                await function.CallAsync<string>(depositContractAddress);

            return userAddress;
        }

        public async Task<string> AssignDepositContractToUserAsync(string userAddress)
        {
            IDepositContract depositContract = await _depositContractRepository.GetByUserAsync(userAddress);
            if (depositContract == null)
            {
                depositContract = await _depositContractQueueService.GetContract();
                depositContract.UserAddress = userAddress;
                await _depositContractRepository.SaveAsync(depositContract);
                await _depositContractUserAssignmentQueueService.PushContract(new DepositContractUserAssignment()
                {
                    DepositContractAddress = depositContract.ContractAddress,
                    UserAddress = userAddress
                });
            }

            return depositContract.ContractAddress;
        }

        private async Task<IDepositContract> GetDepositContractWithCheckAsync(string depositContractAddress)
        {
            IDepositContract depositContract = await _depositContractRepository.GetByAddressAsync(depositContractAddress);

            if (depositContract == null)
            {
                throw new ClientSideException(ExceptionType.WrongParams, $"Transfer contract with address {depositContractAddress} does not exist");
            }

            return depositContract;
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

        public async Task<BigInteger> GetBalanceOnAdapter(string adapterAddress, string clientAddress)
        {
            var coinAFromDb = await _coinRepository.GetCoinByAddress(adapterAddress);
            if (coinAFromDb == null)
            {
                throw new ClientSideException(ExceptionType.WrongParams, $"Coin adapter with {adapterAddress} address does not exist");
            }

            string abi = coinAFromDb.ContainsEth ? _settings.EthAdapterContract.Abi : _settings.TokenAdapterContract.Abi;
            var contract = _web3.Eth.GetContract(abi, coinAFromDb.AdapterAddress);
            var balance = contract.GetFunction("balanceOf");

            return await balance.CallAsync<BigInteger>(clientAddress);
        }

        public async Task<string> RecievePaymentFromTransferContract(string depositContractAddress, string coinAdapterAddress)
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
                    cashinParameters = new object[] { _settings.EthereumAdapterAddress };
                }
                else
                {
                    cashinFunction = contract.GetFunction("cashinEth");
                    cashinParameters = new object[] { coinAdapterAddress };
                }
            }
            else
            {
                string erc20TokenAddress = coinDb.ExternalTokenAddress;
                cashinFunction = contract.GetFunction("cashinTokens");
                cashinParameters = new object[] { erc20TokenAddress, coinAdapterAddress };
            }

            transactionHash = await cashinFunction.SendTransactionAsync(_settings.EthereumMainAccount,
                    new HexBigInteger(Constants.GasForCoinTransaction), new HexBigInteger(0), cashinParameters);

            return transactionHash;
        }

        public async Task<string> ChangeDepositAdminContract(string contractWithDependencyOnAdmin, string newAdminContract)
        {
            Contract contract = _web3.Eth.GetContract(_settings.DepositContract.Abi, contractWithDependencyOnAdmin);

            Function changeFunction = contract.GetFunction("changeDepositAdminContract");

            string trHash = await changeFunction.SendTransactionAsync(_settings.EthereumMainAccount, newAdminContract);

            return trHash;
        }
    }
}
