using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureStorage;
using Core.Repositories;

namespace AzureRepositories.Repositories
{
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
            var all = await _table.GetDataAsync(Erc20ContractEntity.Key);

            return all;
        }

        public async Task<IErc20Contract> GetAsync(string address)
        {
            var erc20Contract = await _table.GetDataAsync(Erc20ContractEntity.Key, address);

            return erc20Contract;
        }

        public async Task InsertOrReplaceAsync(IErc20Contract erc20Contract)
        {
            var entity = Erc20ContractEntity.CreateEntity(erc20Contract);

            await _table.InsertOrReplaceAsync(entity);
        }

        public async Task ProcessAllAsync(Func<IEnumerable<IErc20Contract>, Task> processAction)
        {
            await _table.GetDataByChunksAsync(async item => { await processAction(item); });
        }
    }
}