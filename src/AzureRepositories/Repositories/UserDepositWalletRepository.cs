using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Core.Repositories;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using AzureStorage;
using System.Numerics;

namespace AzureRepositories.Repositories
{
    public class UserDepositWalletEntity : TableEntity, IUserDepositWallet
    {
        public static string GenerateParitionKey(string userAddress)
        {
            return $"UserDepositWallet_{userAddress}";
        }

        public static string GenerateRowKey(string depositContractAddress, string coinAdapterAddress)
        {
            return $"{depositContractAddress}_{coinAdapterAddress}";
        }

        public string UserAddress { get; set; }

        public string DepositContractAddress { get; set; }

        public DateTime UpdateDate { get; set; }

        public string LastBalance { get; set; }

        public string CoinAdapterAddress { get; set; }

    public static UserDepositWalletEntity Create(IUserDepositWallet userDepositWallet)
        {
            string userAddress = userDepositWallet.UserAddress.ToLower();
            string depositAddress = userDepositWallet.DepositContractAddress.ToLower();
            string coinAdapterAddress = userDepositWallet.CoinAdapterAddress.ToLower();

            return new UserDepositWalletEntity
            {
                PartitionKey = GenerateParitionKey(userAddress),
                RowKey =  GenerateRowKey(depositAddress, coinAdapterAddress) ,
                UpdateDate = userDepositWallet.UpdateDate,
                UserAddress = userAddress,
                DepositContractAddress = depositAddress,
                LastBalance = userDepositWallet.LastBalance
            };
        }
    }

    public class UserDepositWalletRepository : IUserDepositWalletRepository
    {
        private readonly INoSQLTableStorage<UserDepositWalletEntity> _table;

        public UserDepositWalletRepository(INoSQLTableStorage<UserDepositWalletEntity> table)
        {
            _table = table;
        }

        public async Task DeleteAsync(string userAddress, string depositContractAddress, string coinAdapterAddress)
        {
            await _table.DeleteIfExistAsync(UserTransferWalletEntity.GenerateParitionKey(userAddress), UserDepositWalletEntity.GenerateRowKey(depositContractAddress, coinAdapterAddress));
        }

        public async Task<IEnumerable<IUserDepositWallet>> GetAllAsync()
        {
            return await _table.GetDataAsync((x) => true);
        }

        public async Task<IUserDepositWallet> GetUserContractAsync(string userAddress, string depositContractAddress, string coinAdapterAddress)
        {
            string lowerUserAddress = userAddress.ToLower();
            IUserDepositWallet wallet =
                await _table.GetDataAsync(UserDepositWalletEntity.GenerateParitionKey(lowerUserAddress), UserDepositWalletEntity.GenerateRowKey(depositContractAddress, coinAdapterAddress));

            return wallet;
        }

        public async Task ReplaceAsync(IUserDepositWallet wallet)
        {
            var entity = UserDepositWalletEntity.Create(wallet);

            await _table.InsertOrReplaceAsync(entity);
        }

        public async Task SaveAsync(IUserDepositWallet wallet)
        {
            var entity = UserDepositWalletEntity.Create(wallet);

            await _table.InsertAsync(entity);
        }
    }
}
