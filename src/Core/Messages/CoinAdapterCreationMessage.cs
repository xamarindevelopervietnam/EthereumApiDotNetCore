using Core.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Messages
{
    public class CoinAdapterCreationMessage : ICoin
    {
        public string Blockchain              { get; set; }
        public string Id                      { get; set; }
        public string Name                    { get; set; }
        public string AdapterAddress          { get; set; }
        public string ExternalTokenAddress    { get; set; }
        public int Multiplier                 { get; set; }
        public bool BlockchainDepositEnabled  { get; set; }
        public bool ContainsEth               { get; set; }
        public string DeployedTransactionHash { get; set; }

        public static CoinAdapterCreationMessage CreateFromCoin(ICoin coin)
        {
            return new CoinAdapterCreationMessage()
            {
                 AdapterAddress           = coin.AdapterAddress,            
                 Blockchain               = coin.Blockchain,
                 BlockchainDepositEnabled = coin.BlockchainDepositEnabled,
                 ContainsEth              = coin.ContainsEth,
                 DeployedTransactionHash  = coin.DeployedTransactionHash,
                 ExternalTokenAddress     = coin.ExternalTokenAddress,
                 Id                       = coin.Id,
                 Multiplier               = coin.Multiplier,
                 Name                     = coin.Name,
            };
        }
    }
}
