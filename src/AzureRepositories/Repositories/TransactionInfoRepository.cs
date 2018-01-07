using AzureStorage;
using Core.Repositories;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AzureRepositories.Repositories
{
    #region Entities

    public class TransactionInfoEntity : TableEntity, ITransactionInfo
    {
        public const string Key = "TransactionInfo";
        public string TransactionHash
        {
            get
            {
                return RowKey;
            }
            set
            {
                RowKey = value;
            }
        }

        public DateTime Date { get; set; }
        public string From { get; set; }
        public string Nonce { get; set; }

        public static TransactionInfoEntity CreateEntity(ITransactionInfo transaction)
        {
            return new TransactionInfoEntity()
            {
                PartitionKey = Key,
                RowKey = transaction.TransactionHash,
                Nonce = transaction.Nonce,
                Date = transaction.Date,
                From = transaction.From
            };
        }
    }


    #endregion

    public class TransactionInfoRepository : ITransactionInfoRepository
    {
        private readonly INoSQLTableStorage<TransactionInfoEntity> _table;

        public TransactionInfoRepository(INoSQLTableStorage<TransactionInfoEntity> tableOpId)
        {
            _table = tableOpId;
        }

        public async Task<ITransactionInfo> GetAsync(string txHash)
        {
            var entity = await _table.GetDataAsync(TransactionInfoEntity.Key, txHash);

            return entity;
        }

        public async Task SaveAsync(ITransactionInfo transferContract)
        {
            var entity = TransactionInfoEntity.CreateEntity(transferContract);

            await _table.InsertOrReplaceAsync(entity);
        }
    }
}
