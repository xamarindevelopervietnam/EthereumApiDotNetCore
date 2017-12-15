using AzureRepositories;
using AzureStorage.Tables;
using BusinessModels;
using Common;
using Common.Log;
using Core.Settings;
using EthereumSamuraiApiCaller;
using EthereumSamuraiApiCaller.Models;
using Lykke.Service.EthereumCore.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Web3;
using RabbitMQ;
using Services;
using Services.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace CashinReportGenerator
{
    class Program
    {
        public class ExternalTransactionModel
        {
            public string ClientId { get; set; }

            public string SenderAddress { get; set; }

            public string RecieverAddress { get; set; }

            public string EthAmount { get; set; }

            public DateTime DateTimeUtc { get; set; }

            public string TransactionHash { get; set; }

            public string RecieverType { get; set; } //Trade, PrivateWallet
        }


        static void Main(string[] args)
        {
            IServiceProvider ServiceProvider;
            string clientPersonalInfoConnString = args[0];
            string ethAssetId = args[1];
            var configurationBuilder = new ConfigurationBuilder()
           .SetBasePath(Directory.GetCurrentDirectory())
           .AddJsonFile("appsettings.json").AddEnvironmentVariables();
            var configuration = configurationBuilder.Build();

            var settings = GetCurrentSettings();

            IServiceCollection collection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            collection.AddSingleton<IBaseSettings>(settings.EthereumCore);
            collection.AddSingleton<ISlackNotificationSettings>(settings.SlackNotifications);

            RegisterReposExt.RegisterAzureLogs(collection, settings.EthereumCore, "");
            RegisterReposExt.RegisterAzureQueues(collection, settings.EthereumCore, settings.SlackNotifications);
            RegisterReposExt.RegisterAzureStorages(collection, settings.EthereumCore, settings.SlackNotifications);
            ServiceProvider = collection.BuildServiceProvider();
            RegisterRabbitQueueEx.RegisterRabbitQueue(collection, settings.EthereumCore, ServiceProvider.GetService<ILog>());
            RegisterDependency.RegisterServices(collection);
            ServiceProvider = collection.BuildServiceProvider();

            var log = ServiceProvider.GetService<ILog>();
            var bcnRepositoryReader = new BcnClientCredentialsRepository(
                    new AzureTableStorage<BcnCredentialsRecordEntity>(clientPersonalInfoConnString,
                        "BcnClientCredentials", log));
            var privateWalletsReader = new PrivateWalletsRepository(
                    new AzureTableStorage<PrivateWalletEntity>(clientPersonalInfoConnString,
                        "PrivateWallets", log));
            var samuraiApi = ServiceProvider.GetService<IEthereumSamuraiApi>();
            var ethPrecision = BigInteger.Pow(10, 18);

            try
            {
                using (var streamWriter = new StreamWriter("csvReport"))
                using (var csvWriter = new CsvHelper.CsvWriter(streamWriter, false))
                {
                    csvWriter.WriteHeader<ExternalTransactionModel>();
                    csvWriter.NextRecord();

                    #region PrivateWallets

                    try
                    {
                        privateWalletsReader.ProcessAllAsync(async (wallet) =>
                        {
                            if (wallet.BlockchainType == Lykke.Service.Assets.Client.Models.Blockchain.Ethereum)
                            {
                                await RetryPolicy.ExecuteAsync(async () =>
                                {
                                    String address = wallet.WalletAddress;

                                    int count = 1000;
                                    int start = 0;
                                    Console.WriteLine($"Asking Samurai about {address}");
                                    IEnumerable<AddressHistoryModel> batchRead = null;

                                    do
                                    {
                                        batchRead = await GetAddressHistory(samuraiApi, new AddressTransaction()
                                        {
                                            Address = wallet.WalletAddress,
                                            Count = count,
                                            Start = start,
                                        });

                                        foreach (var item in batchRead)
                                        {
                                            if (item.From.ToLower() == address.ToLower())
                                            {
                                                continue;
                                            }

                                            //var amount = BigInteger.Parse(item.Value);
                                            ExternalTransactionModel model = new ExternalTransactionModel()
                                            {
                                                ClientId = wallet.ClientId,
                                                DateTimeUtc = item.BlockTimeUtc,
                                                EthAmount = ConvertFromContract(item.Value, 18, 18).ToString(),
                                                RecieverAddress = item.To,
                                                SenderAddress = item.From,
                                                RecieverType = "PrivateWallet",
                                                TransactionHash = item.TransactionHash
                                            };

                                            csvWriter.WriteRecord<ExternalTransactionModel>(model);
                                            csvWriter.NextRecord();

                                            Console.WriteLine($"Written ${model.ToJson()}");
                                        }

                                        Console.WriteLine($"Requested {address} - {start} - {count}");

                                        start += count;
                                    }
                                    while (batchRead != null && batchRead.Count() != 0);

                                }, 3, 100);
                            }
                            else
                            {
                                Console.WriteLine($"Skipping {wallet.WalletAddress}");
                            }
                        }).Wait();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Completly broken {e.Message} - {e.StackTrace}");
                    }

                    Console.WriteLine("Completed for privateWallets");

                    #endregion

                    #region bcn

                    Console.WriteLine("Started for TradeWWallet");

                    try
                    {
                        bcnRepositoryReader.ProcessAllAsync(async (wallet) =>
                        {
                            if (wallet.AssetId == ethAssetId)
                            {
                                await RetryPolicy.ExecuteAsync(async () =>
                                {
                                    String address = wallet.AssetAddress;

                                    int count = 1000;
                                    int start = 0;
                                    Console.WriteLine($"Asking Samurai about {address}");
                                    IEnumerable<AddressHistoryModel> batchRead = null;

                                    do
                                    {
                                        batchRead = await GetAddressHistory(samuraiApi, new AddressTransaction()
                                        {
                                            Address = address,
                                            Count = count,
                                            Start = start,
                                        });

                                        foreach (var item in batchRead)
                                        {
                                            if (item.From.ToLower() == address.ToLower()
                                            || item.Value == "0")
                                            {
                                                continue;
                                            }

                                            //var amount = BigInteger.Parse(item.Value);
                                            ExternalTransactionModel model = new ExternalTransactionModel()
                                            {
                                                ClientId = wallet.ClientId,
                                                DateTimeUtc = item.BlockTimeUtc,
                                                EthAmount = ConvertFromContract(item.Value, 18, 18).ToString(),
                                                RecieverAddress = item.To,
                                                SenderAddress = item.From,
                                                RecieverType = "TradeWallet",
                                                TransactionHash = item.TransactionHash
                                            };

                                            csvWriter.WriteRecord<ExternalTransactionModel>(model);
                                            csvWriter.NextRecord();

                                            Console.WriteLine($"Written ${model.ToJson()}");
                                        }

                                        Console.WriteLine($"Requested {address} - {start} - {count}");

                                        start += count;
                                    }
                                    while (batchRead != null && batchRead.Count() != 0);

                                }, 3, 100);
                            }
                            else
                            {
                                Console.WriteLine($"Skipping {wallet.AssetAddress}");
                            }
                        }).Wait();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Completly broken {e.Message} - {e.StackTrace}");
                    }

                    Console.WriteLine("Completed for bcn repo");

                    #endregion

                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error {e.Message} - {e.StackTrace}");
            }
        }

        static SettingsWrapper GetCurrentSettings()
        {
            FileInfo fi = new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location);
            var location = Path.Combine(fi.DirectoryName, "..", "..", "..");
            var builder = new ConfigurationBuilder()
                .SetBasePath(location)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            var configuration = builder.Build();
            var settings = GeneralSettingsReader.ReadGeneralSettings<SettingsWrapper>(configuration.GetConnectionString("ConnectionString"));

            return settings;
        }

        public static async Task<IEnumerable<AddressHistoryModel>> GetAddressHistory(IEthereumSamuraiApi ethereumSamuraiApi,
            AddressTransaction addressTransactions)
        {
            var historyResponseRaw = await ethereumSamuraiApi.ApiAddressHistoryByAddressGetAsync(
                addressTransactions.Address, null, null, addressTransactions.Start, addressTransactions.Count);
            var addressHistoryResponse = historyResponseRaw as FilteredAddressHistoryResponse;
            ThrowOnError(historyResponseRaw);
            int responseCount = addressHistoryResponse.History?.Count ?? 0;
            List<AddressHistoryModel> result = new List<AddressHistoryModel>(responseCount);

            foreach (var item in addressHistoryResponse.History)
            {
                result.Add(
                    new AddressHistoryModel()
                    {
                        MessageIndex = item.MessageIndex.Value,
                        TransactionIndexInBlock = item.TransactionIndex.Value,
                        BlockNumber = (ulong)item.BlockNumber.Value,
                        BlockTimestamp = (uint)item.BlockTimestamp.Value,
                        BlockTimeUtc = DateUtils.UnixTimeStampToDateTimeUtc(item.BlockTimestamp.Value),
                        From = item.FromProperty,
                        HasError = item.HasError.Value,
                        To = item.To,
                        TransactionHash = item.TransactionHash,
                        Value = item.Value,
                        GasPrice = item.GasPrice,
                        GasUsed = item.GasUsed
                    });
            }

            return result;
        }

        private static void ThrowOnError(object transactionResponse)
        {
            if (transactionResponse == null)
            {
                var exception = transactionResponse as ApiException;
                var errorMessage = exception?.Error?.Message ?? "Response is empty";

                throw new Exception(errorMessage);
            }
        }

        public static decimal ConvertFromContract(string amount, int multiplier, int accuracy)
        {
            if (accuracy > multiplier)
                throw new ArgumentException("accuracy > multiplier");

            multiplier -= accuracy;

            var val = BigInteger.Parse(amount);
            var res = (decimal)(val / BigInteger.Pow(10, multiplier));
            res /= (decimal)Math.Pow(10, accuracy);

            return res;
        }
    }
}
