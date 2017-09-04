using Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Services;
using AzureRepositories;
using EthereumJobs.Job;
using Lykke.JobTriggers.Abstractions;
using AzureRepositories.Notifiers;
using Common.Log;
using EthereumJobs.Job.DepositJobs;
using RabbitMQ;

namespace EthereumJobs.Config
{
    public static class RegisterDependency
    {
        public static void InitJobDependencies(this IServiceCollection collection, IBaseSettings settings, ISlackNotificationSettings slackNotificationSettings)
        {
            collection.AddSingleton(settings);

            collection.RegisterAzureLogs(settings, "Job");
            collection.RegisterAzureStorages(settings, slackNotificationSettings);
            collection.RegisterAzureQueues(settings, slackNotificationSettings);

            collection.RegisterServices();
            var provider = collection.BuildServiceProvider();

            collection.RegisterRabbitQueue(settings, provider.GetService<ILog>());
            collection.AddTransient<IPoisionQueueNotifier, SlackNotifier>();
            collection.AddSingleton(new Lykke.MonitoringServiceApiCaller.MonitoringServiceFacade(settings.MonitoringServiceUrl));
            RegisterJobs(collection);
        }

        public static void RegisterJobs(IServiceCollection collection)
        {
            #region CoreJobs

            collection.AddSingleton<MonitoringJob>();
            collection.AddSingleton<PingContractsJob>();
            collection.AddSingleton<MonitoringCoinTransactionJob>();
            collection.AddSingleton<MonitoringOperationJob>();
            collection.AddSingleton<CashinIndexingJob>();
            collection.AddSingleton<OwnersBalanceCheckJob>();
            collection.AddSingleton<CoinEventResubmittJob>();

            #endregion

            #region DepositJobs

            collection.AddSingleton<DepositContractPoolJob>();
            collection.AddSingleton<DepositContractUserAssignmentJob>();
            collection.AddSingleton<DepositPoolRenewJob>();
            collection.AddSingleton<MonitoringDepositContractsJob>();
            collection.AddSingleton<MonitoringDepositTransactionsJob>();

            #endregion

            #region TransferJobs

            collection.AddSingleton<MonitoringTransferContracts>();
            collection.AddSingleton<MonitoringTransferTransactions>();
            collection.AddSingleton<TransferContractUserAssignmentJob>();
            collection.AddSingleton<PoolRenewJob>();
            collection.AddSingleton<TransferTransactionQueueJob>();

            #endregion

        }
    }
}
