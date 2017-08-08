using BusinessModels.Erc20;
using EthereumSamuraiApiCaller;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Services.Erc20
{
    public interface IErc20BalanceService
    {
        Task<IEnumerable<AddressTokenBalance>> GetBalancesForAddress(string address, IEnumerable<string> erc20TokenAddresses);
    }

    public class Erc20BalanceService : IErc20BalanceService
    {
        private readonly IEthereumSamuraiApi _ethereumSamuraiApi;

        public Erc20BalanceService(IEthereumSamuraiApi ethereumSamuraiApi)
        {
            _ethereumSamuraiApi = ethereumSamuraiApi;
        }

        public async Task<IEnumerable<AddressTokenBalance>> GetBalancesForAddress(string address, IEnumerable<string> erc20TokenAddresses)
        {
            //TODO: Recieve all balances for supported tokens;
            return null;
        }
    }
}
