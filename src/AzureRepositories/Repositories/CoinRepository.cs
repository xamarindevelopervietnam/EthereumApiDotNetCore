using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureStorage;
using AzureStorage.Tables.Templates.Index;
using Core.Repositories;

namespace AzureRepositories.Repositories
{
    public class CoinRepository : ICoinRepository
    {
        private const string AddressIndexName = "AddressIndex";



        private readonly INoSQLTableStorage<AzureIndex> _addressIndex;
        private readonly INoSQLTableStorage<CoinEntity> _table;

        

        public CoinRepository(
            INoSQLTableStorage<CoinEntity> table,
            INoSQLTableStorage<AzureIndex> addressIndex)
        {
            _addressIndex = addressIndex;
            _table        = table;
        }

        public async Task<IEnumerable<ICoin>> GetAll()
        {
            var all = await _table.GetDataAsync(CoinEntity.Key);

            return all;
        }

        public async Task<ICoin> GetCoin(string coinId)
        {
            var coin = await _table.GetDataAsync(CoinEntity.Key, coinId);

            if (coin == null)
            {
                throw new Exception("Unknown coin name - " + coinId);
            }
            
            return coin;
        }

        public async Task<ICoin> GetCoinByAddress(string coinAddress)
        {
            var index = await _addressIndex.GetDataAsync(AddressIndexName, coinAddress);

            if (index == null)
            {
                return null;
            }
                
            var coin = await _table.GetDataAsync(index);

            return coin;
        }

        public async Task InsertOrReplace(ICoin coin)
        {
            var entity = CoinEntity.CreateCoinEntity(coin);
            var index  = AzureIndex.Create(AddressIndexName, coin.AdapterAddress, entity);

            await _table.InsertOrReplaceAsync(entity);
            await _addressIndex.InsertAsync(index);
        }

        public async Task ProcessAllAsync(Func<IEnumerable<ICoin>, Task> processAction)
        {
            async Task ChunksFunction(IEnumerable<CoinEntity> items)
            {
                await processAction(items);
            }

            await _table.GetDataByChunksAsync(ChunksFunction);
        }
    }
}