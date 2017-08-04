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
    // there could be only one asmin contract
    public class DepositAdminContractEntity : TableEntity, IDepositAdminContract
    {
        public static string GenerateParitionKey()
        {
            return "DepositAdminContract";
        }

        public static string GenerateRowKey()
        {
            return "CurrentAdmin";
        }

        public string ContractAddress { get; set; }
        public string OwnerAddress { get; set; }

        public static DepositAdminContractEntity Create(IDepositAdminContract depositContract)
        {
            return new DepositAdminContractEntity
            {
                PartitionKey = GenerateParitionKey(),
                RowKey = GenerateRowKey(),
                ContractAddress = depositContract.ContractAddress,
                OwnerAddress = depositContract.OwnerAddress,
            };
        }
    }

    public class DepositAdminContractRepository : IDepositAdminContractRepository
    {
        private readonly INoSQLTableStorage<DepositAdminContractEntity> _table;

        public DepositAdminContractRepository(INoSQLTableStorage<DepositAdminContractEntity> table)
        {
            _table = table;
        }

        public async Task SaveAsync(IDepositAdminContract depositContract)
        {
            var entity = DepositAdminContractEntity.Create(depositContract);

            await _table.InsertOrReplaceAsync(entity);
        }

        public async Task<IDepositAdminContract> GetAsync()
        {
            var contract = await _table.GetDataAsync(DepositAdminContractEntity.GenerateParitionKey(), DepositAdminContractEntity.GenerateRowKey());

            return contract;
        }
    }
}
