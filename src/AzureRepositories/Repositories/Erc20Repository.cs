using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core;
using Core.Repositories;
using Microsoft.WindowsAzure.Storage.Table;
using AzureStorage;
using AzureStorage.Tables.Templates.Index;

namespace AzureRepositories.Repositories
{
    public class Erc20ContractEntity : TableEntity, IErc20Contract
    {
        public const string Key = "ERC20";

        public string TokenName { get; set; }
        public string TokenAddress { get; set; }

        public static Erc20ContractEntity CreateEntity(IErc20Contract contract)
        {
            return new Erc20ContractEntity
            {
                PartitionKey = Erc20ContractEntity.Key,
                RowKey = contract.TokenAddress,
                TokenAddress = contract.TokenAddress,
                TokenName = contract.TokenName,
            };
        }
    }

    public class Erc20ContractRepository : IErc20ContractRepository
    {
        private readonly INoSQLTableStorage<Erc20ContractEntity> _table;

        public Erc20ContractRepository(INoSQLTableStorage<Erc20ContractEntity> table)
        {
            _table = table;
        }

        public async Task DeleteAsync(string address)
        {
            await _table.DeleteIfExistAsync(Erc20ContractEntity.Key, address);
        }

        public async Task<IEnumerable<IErc20Contract>> GetAllAsync()
        {
            IEnumerable<IErc20Contract> all = await _table.GetDataAsync(Erc20ContractEntity.Key);

            return all;
        }

        public async Task<IErc20Contract> GetAsync(string address)
        {
            IErc20Contract erc20Contract = await _table.GetDataAsync(Erc20ContractEntity.Key, address);

            return erc20Contract;
        }

        public async Task InsertOrReplaceAsync(IErc20Contract erc20Contract)
        {
            var entity = Erc20ContractEntity.CreateEntity(erc20Contract);
            await _table.InsertOrReplaceAsync(entity);
        }

        public async Task ProcessAllAsync(Func<IEnumerable<IErc20Contract>, Task> processAction)
        {
            await _table.GetDataByChunksAsync(async (item) =>
            {
                await processAction(item);
            });
        }
    }
}
