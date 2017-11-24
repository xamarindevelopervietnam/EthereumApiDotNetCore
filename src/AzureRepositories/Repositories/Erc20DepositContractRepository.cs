﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureStorage;
using Core.Repositories;
using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace AzureRepositories.Repositories
{
    public class Erc20DepositContractRepository : IErc20DepositContractRepository
    {
        private readonly INoSQLTableStorage<Erc20DepositContractEntity> _table;


        public Erc20DepositContractRepository(INoSQLTableStorage<Erc20DepositContractEntity>table)
        {
            _table = table;
        }


        public async Task AddOrReplace(IErc20DepositContract depositContract)
        {
            await _table.InsertOrReplaceAsync(new Erc20DepositContractEntity
            {
                ContractAddress = depositContract.ContractAddress,
                PartitionKey = GetParitionKey(),
                RowKey = GetRowKey(depositContract.UserAddress),
                UserAddress = depositContract.UserAddress,
            });
        }

        public async Task<IErc20DepositContract> Get(string userAddress)
        {
            var entity = await _table.GetDataAsync(GetParitionKey(), GetRowKey(userAddress));

            return entity;
        }

        public async Task<IEnumerable<IErc20DepositContract>> GetAll()
        {
            var entities = await _table.GetDataAsync(GetParitionKey());

            return entities;
        }

        public async Task ProcessAllAsync(Func<IErc20DepositContract, Task> processAction)
        {
            await _table.GetDataByChunksAsync(GetParitionKey(), async (items) =>
            {
                foreach (var item in items)
                {
                    await processAction(item);
                }
            });
        }

        private static string GetParitionKey()
            => "Erc20DepositContract";

        private static string GetRowKey(string userAddress)
            => userAddress;
    }

    public class Erc20DepositContractEntity : TableEntity, IErc20DepositContract
    {
        public string ContractAddress { get; set; }

        public string UserAddress { get; set; }
    }
}