using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Core.Repositories;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using AzureStorage;
using AzureStorage.Tables.Templates.Index;

namespace AzureRepositories.Repositories
{
    public class DepositContractEntity : TableEntity, IDepositContract
    {
        public static string GenerateParitionKey()
        {
            return "DepositContract";
        }
        public string ContractAddress
        {
            get { return this.RowKey; }
            set { this.RowKey = value; }
        }

        public string UserAddress { get; set; }
        public string EthAdapterAddress { get; set; }
        public string AssignmentHash { get; set; }
        public string LegacyEthAdapterAssignmentHash { get; set; }

        public static DepositContractEntity Create(IDepositContract depositContract)
        {
            return new DepositContractEntity
            {
                PartitionKey = GenerateParitionKey(),
                EthAdapterAddress = depositContract.EthAdapterAddress,
                UserAddress = depositContract.UserAddress,
                ContractAddress = depositContract.ContractAddress,
                AssignmentHash = depositContract.AssignmentHash,
                LegacyEthAdapterAssignmentHash = depositContract.LegacyEthAdapterAssignmentHash,
            };
        }
    }

    public class DepositContractRepository : IDepositContractRepository
    {
        private readonly INoSQLTableStorage<DepositContractEntity> _table;
        private readonly INoSQLTableStorage<AzureIndex> _userContractIndex;
        private const string _indexPartition = "UserContractIndex";

        public DepositContractRepository(INoSQLTableStorage<DepositContractEntity> table, INoSQLTableStorage<AzureIndex> userContractIndex)
        {
            _table = table;
            _userContractIndex = userContractIndex;
        }
        public async Task SaveAsync(IDepositContract depositContract)
        {
            var entity = DepositContractEntity.Create(depositContract);

            await _table.InsertOrReplaceAsync(entity);
            if (!string.IsNullOrEmpty(entity.UserAddress))
            {
                var index = new AzureIndex(_indexPartition,
                entity.UserAddress, entity);

                await _userContractIndex.InsertOrReplaceAsync(index);
            }
        }

        public async Task<IDepositContract> GetByAddressAsync(string depositContractAddress)
        {
            var contract = await _table.GetDataAsync(DepositContractEntity.GenerateParitionKey(), depositContractAddress);

            return contract;
        }

        public async Task ProcessAllAsync(Func<IDepositContract, Task> processAction)
        {
            await _table.GetDataByChunksAsync(DepositContractEntity.GenerateParitionKey(), async (items) =>
            {
                foreach (var item in items)
                {
                    await processAction(item);
                }
            });
        }

        public async Task<IDepositContract> GetByUserAsync(string userAddress)
        {
            var index = await _userContractIndex.GetDataAsync(_indexPartition,
                userAddress);

            if (index == null)
            {
                return null;
            }

            IDepositContract result = await _table.GetDataAsync(index);

            return result;
        }
    }
}
