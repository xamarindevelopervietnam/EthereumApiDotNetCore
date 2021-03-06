﻿using Lykke.Service.EthereumCore.Core;
using Lykke.Service.EthereumCore.Core.Repositories;
using Lykke.Service.EthereumCore.Core.Settings;
using Nethereum.Web3;
using Lykke.Service.EthereumCore.Services.New.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AzureStorage.Queue;
using EthereumSamuraiApiCaller;
using EthereumSamuraiApiCaller.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Lykke.Service.EthereumCore.Services.Coins.Models;
using Lykke.Service.EthereumCore.Services.PrivateWallet;

namespace Lykke.Service.EthereumCore.Services.New
{
    public interface ITransactionEventsService
    {
        Task IndexCashinEventsForAdapter(string coinAdapterAddress, string deployedTransactionHash);
        Task IndexCashinEventsForErc20Deposits();
        Task MonitorNewEvents(string coinAdapterAddress);
        Task<ICashinEvent> GetCashinEvent(string transactionHash);
        Task IndexEventsForTransaction(string coinAdapterAddress, string trHash);
        Task<BigInteger?> IndexCashinEventsForErc20TransactionHashAsync(string transactionHash);
    }

    public class TransactionEventsService : ITransactionEventsService
    {
        private const string Erc20HotWalletMarker = "ERC20_HOTWALLET";


        private readonly Web3 _web3;
        private readonly IBaseSettings _baseSettings;
        private readonly IQueueFactory _queueFactory;
        private readonly IQueueExt _cashinQueue;
        private readonly IQueueExt _cointTransactionQueue;
        private readonly IEthereumIndexerService _ethereumIndexerService;
        private readonly ICoinRepository _coinRepository;
        private readonly ICashinEventRepository _cashinEventRepository;
        private readonly IBlockSyncedRepository _blockSyncedRepository;
        private readonly AppSettings _settingsWrapper;
        private readonly IEthereumSamuraiApi _indexerApi;
        private readonly IErc20DepositContractService _depositContractService;

        public TransactionEventsService(Web3 web3,
            IBaseSettings baseSettings,
            ICoinRepository coinRepository,
            ICashinEventRepository cashinEventRepository,
            IBlockSyncedRepository blockSyncedRepository,
            IQueueFactory queueFactory,
            AppSettings settingsWrapper,
            IEthereumSamuraiApi indexerApi,
            IErc20DepositContractService depositContractService,
            IEthereumIndexerService ethereumIndexerService)
        {
            _cashinEventRepository = cashinEventRepository;
            _coinRepository = coinRepository;
            _web3 = web3;
            _blockSyncedRepository = blockSyncedRepository;
            _baseSettings = baseSettings;
            _queueFactory = queueFactory;
            _settingsWrapper = settingsWrapper;
            _indexerApi = indexerApi;
            _depositContractService = depositContractService;
            _cashinQueue = _queueFactory.Build(Constants.CashinCompletedEventsQueue);
            _cointTransactionQueue = _queueFactory.Build(Constants.HotWalletTransactionMonitoringQueue);
            _ethereumIndexerService = ethereumIndexerService;
        }

        public async Task IndexCashinEventsForAdapter(string coinAdapterAddress, string deployedTransactionHash)
        {
            var lastBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var contract = _web3.Eth.GetContract(_baseSettings.CoinAbi, coinAdapterAddress);
            var coinCashInEvent = contract.GetEvent("CoinCashIn");
            BigInteger lastSynced = await GetLastSyncedBlockNumber(coinAdapterAddress);
            var tranaction = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(deployedTransactionHash);
            BigInteger contractDeployBlockNumber = tranaction.BlockNumber;
            BigInteger indexStartBlock = lastSynced > contractDeployBlockNumber ? lastSynced : contractDeployBlockNumber;
            int scanRange = 1000;

            for (BigInteger from = indexStartBlock; from < lastBlock; from += scanRange + 1)
            {
                BigInteger to = from + scanRange;
                to = to < lastBlock ? to : lastBlock;
                await IndexEventsInRange(coinAdapterAddress, coinCashInEvent, from, to);
            }
        }

        public async Task<BigInteger?> IndexCashinEventsForErc20TransactionHashAsync(string transactionHash)
        {
            BigInteger result = 0;
            var transaction = await _ethereumIndexerService.GetTransactionAsync(transactionHash);

            if (transaction == null)
                return null;

            if (transaction.ErcTransfer != null)
            {
                //only one transfer could appear in deposit transaction
                foreach (var item in transaction.ErcTransfer)
                {
                    if (item.To?.ToLower() != _settingsWrapper.Ethereum.HotwalletAddress?.ToLower())
                        continue;

                    await _cashinEventRepository.InsertAsync(new CashinEvent
                    {
                        CoinAdapterAddress = Erc20HotWalletMarker,
                        Amount = item.Value,
                        TransactionHash = item.TransactionHash,
                        UserAddress = item.From,
                        ContractAddress = item.ContractAddress
                    });

                    BigInteger.TryParse(item.Value, out result);
                }
            }

            return result;
        }

        public async Task IndexCashinEventsForErc20Deposits()
        {
            var indexerStatusResponse = await _indexerApi.ApiSystemIsAliveGetWithHttpMessagesAsync();
            if (indexerStatusResponse.Response.IsSuccessStatusCode)
            {
                var responseContent = await indexerStatusResponse.Response.Content.ReadAsStringAsync();
                var indexerStatus = JObject.Parse(responseContent);
                var lastIndexedBlock = BigInteger.Parse(indexerStatus["blockchainTip"].Value<string>());
                var lastSyncedBlock = await GetLastSyncedBlockNumber(Erc20HotWalletMarker);

                while (++lastSyncedBlock <= lastIndexedBlock - _baseSettings.Level2TransactionConfirmation)
                {
                    var transfersResponse = await _indexerApi.ApiErc20TransferHistoryGetErc20TransfersPostAsync
                    (
                        new GetErc20TransferHistoryRequest
                        {
                            AssetHolder = _settingsWrapper.Ethereum.HotwalletAddress?.ToLower(),
                            BlockNumber = (long)lastSyncedBlock,
                        }
                    );

                    switch (transfersResponse)
                    {
                        case IEnumerable<Erc20TransferHistoryResponse> transfers:

                            foreach (var transfer in transfers)
                            {
                                // Ignore transfers from not deposit contract addresses
                                if (!await _depositContractService.ContainsAsync(transfer.FromProperty))
                                {
                                    continue;
                                }

                                var coinTransactionMessage = new CoinTransactionMessage
                                {
                                    TransactionHash = transfer.TransactionHash
                                };

                                await _cashinEventRepository.InsertAsync(new CashinEvent
                                {
                                    CoinAdapterAddress = Erc20HotWalletMarker,
                                    Amount = transfer.TransferAmount,
                                    TransactionHash = transfer.TransactionHash,
                                    UserAddress = transfer.FromProperty,
                                    ContractAddress = transfer.Contract
                                });

                                await _cointTransactionQueue.PutRawMessageAsync(JsonConvert.SerializeObject(coinTransactionMessage));
                            }

                            break;
                        case ApiException exception:
                            throw new Exception($"Ethereum indexer responded with error: {exception.Error.Message}");
                        default:
                            throw new Exception($"Ethereum indexer returned unexpected response");
                    }

                    await _blockSyncedRepository.InsertAsync(new BlockSynced
                    {
                        BlockNumber = lastSyncedBlock.ToString(),
                        CoinAdapterAddress = Erc20HotWalletMarker
                    });
                }
            }
            else
            {
                throw new Exception("Can not obtain ethereum indexer status.");
            }
        }

        public async Task MonitorNewEvents(string coinAdapterAddress)
        {
            var contract = _web3.Eth.GetContract(_baseSettings.CoinAbi, coinAdapterAddress);
            var coinCashInEvent = contract.GetEvent("CoinCashIn");
            var lastBlock = await GetLastSyncedBlockNumber(coinAdapterAddress);
            var lastRpcBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

            if (lastRpcBlock.Value == lastBlock)
            {
                return;
            }

            await IndexEventsInRange(coinAdapterAddress, coinCashInEvent, lastBlock, lastRpcBlock.Value);
        }

        public async Task<ICashinEvent> GetCashinEvent(string transactionHash)
        {
            ICashinEvent @event = await _cashinEventRepository.GetAsync(transactionHash);

            return @event;
        }

        public async Task IndexEventsForTransaction(string coinAdapterAddress, string trHash)
        {
            var contract = _web3.Eth.GetContract(_baseSettings.CoinAbi, coinAdapterAddress);
            var coinCashInEvent = contract.GetEvent("CoinCashIn");
            var transaction = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(trHash);
            var fromBlock = new Nethereum.RPC.Eth.DTOs.BlockParameter(new Nethereum.Hex.HexTypes.HexBigInteger(transaction.BlockNumber));
            var toBlock = new Nethereum.RPC.Eth.DTOs.BlockParameter(new Nethereum.Hex.HexTypes.HexBigInteger(transaction.BlockNumber.Value + 1));
            var filter = await coinCashInEvent.CreateFilterBlockRangeAsync(fromBlock, toBlock);
            var filterByCaller = await coinCashInEvent.GetAllChanges<CoinCashinEvent>(filter);

            filterByCaller.ForEach(async @event =>
            {
                string transactionHash = @event.Log.TransactionHash;
                CoinEventCashinCompletedMessage cashinTransactionMessage = new CoinEventCashinCompletedMessage()
                {
                    TransactionHash = transactionHash
                };

                await _cashinEventRepository.InsertAsync(new CashinEvent()
                {
                    CoinAdapterAddress = coinAdapterAddress,
                    Amount = @event.Event.Amount.ToString(),
                    TransactionHash = transactionHash,
                    UserAddress = @event.Event.Caller
                });
            });
        }

        private async Task<BigInteger> GetLastSyncedBlockNumber(string coinAdapterAddress)
        {
            string lastSyncedBlockNumber = (await _blockSyncedRepository.GetLastSyncedAsync(coinAdapterAddress))?.BlockNumber;
            BigInteger lastSynced;
            BigInteger.TryParse(lastSyncedBlockNumber, out lastSynced);

            return lastSynced;
        }

        private async Task IndexEventsInRange(string coinAdapterAddress, Nethereum.Contracts.Event coinCashInEvent, BigInteger from, BigInteger to)
        {
            var fromBlock = new Nethereum.RPC.Eth.DTOs.BlockParameter(new Nethereum.Hex.HexTypes.HexBigInteger(from));
            var toBlock = new Nethereum.RPC.Eth.DTOs.BlockParameter(new Nethereum.Hex.HexTypes.HexBigInteger(to));
            var filter = await coinCashInEvent.CreateFilterBlockRangeAsync(fromBlock, toBlock);
            var filterByCaller = await coinCashInEvent.GetAllChanges<CoinCashinEvent>(filter);

            filterByCaller.ForEach(async @event =>
            {
                string transactionHash = @event.Log.TransactionHash;
                CoinEventCashinCompletedMessage cashinTransactionMessage = new CoinEventCashinCompletedMessage()
                {
                    TransactionHash = transactionHash
                };

                await _cashinEventRepository.InsertAsync(new CashinEvent()
                {
                    CoinAdapterAddress = coinAdapterAddress,
                    Amount = @event.Event.Amount.ToString(),
                    TransactionHash = transactionHash,
                    UserAddress = @event.Event.Caller
                });

                await _cashinQueue.PutRawMessageAsync(Newtonsoft.Json.JsonConvert.SerializeObject(cashinTransactionMessage));
            });

            await _blockSyncedRepository.InsertAsync(new BlockSynced() { BlockNumber = to.ToString(), CoinAdapterAddress = coinAdapterAddress });
        }
    }
}
