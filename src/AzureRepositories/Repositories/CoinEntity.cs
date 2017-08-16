using Core.Repositories;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureRepositories.Repositories
{
    public class CoinEntity : TableEntity, ICoin
    {
        public const string Key = "Asset";



        public string AdapterAddress { get; set; }

        public string Blockchain { get; set; }

        public bool BlockchainDepositEnabled { get; set; }

        public bool ContainsEth { get; set; }

        public string DeployedTransactionHash { get; set; }

        public string ExternalTokenAddress { get; set; }

        public string Id => RowKey;

        public int Multiplier { get; set; }

        public string Name { get; set; }



        public static CoinEntity CreateCoinEntity(ICoin coin)
        {
            return new CoinEntity
            {
                Name                     = coin.Name,
                AdapterAddress           = coin.AdapterAddress,
                RowKey                   = coin.Id,
                Multiplier               = coin.Multiplier,
                Blockchain               = coin.Blockchain,
                PartitionKey             = Key,
                BlockchainDepositEnabled = coin.BlockchainDepositEnabled,
                ContainsEth              = coin.ContainsEth,
                ExternalTokenAddress     = coin.ExternalTokenAddress,
                DeployedTransactionHash  = coin.DeployedTransactionHash
            };
        }
    }
}