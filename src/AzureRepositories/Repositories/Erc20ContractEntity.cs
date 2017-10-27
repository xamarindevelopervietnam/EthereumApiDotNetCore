using Core.Repositories;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureRepositories.Repositories
{
    public class Erc20ContractEntity : TableEntity, IErc20Contract
    {
        public const string Key = "ERC20";



        public string TokenAddress { get; set; }

        public string TokenName { get; set; }



        public static Erc20ContractEntity CreateEntity(IErc20Contract contract)
        {
            return new Erc20ContractEntity
            {
                PartitionKey = Key,
                RowKey       = contract.TokenAddress,
                TokenAddress = contract.TokenAddress,
                TokenName    = contract.TokenName
            };
        }
    }
}