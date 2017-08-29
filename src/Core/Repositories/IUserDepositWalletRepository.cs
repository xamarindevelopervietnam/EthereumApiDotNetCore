using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Core.Repositories
{
    public interface IUserDepositWallet
    {
        string UserAddress { get; }
        string CoinAdapterAddress { get; }
        string DepositContractAddress { get; }
        DateTime UpdateDate { get; }
        string LastBalance { get; set; }
    }

    public class UserDepositWallet : IUserDepositWallet
    {
        public string UserAddress { get; set; }
        public string DepositContractAddress { get; set; }
        public DateTime UpdateDate { get; set; }
        public string LastBalance { get; set; }
        public string CoinAdapterAddress { get; set; }
    }

    public interface IUserDepositWalletRepository
    {
        Task SaveAsync(IUserDepositWallet wallet);
        Task ReplaceAsync(IUserDepositWallet wallet);
        Task DeleteAsync(string userAddress, string depositContractAddress, string coinAdapetAddress);
        Task<IUserDepositWallet> GetUserContractAsync(string userAddress, string depositContractAddress, string coinAdapetAddress);
        Task<IEnumerable<IUserDepositWallet>> GetAllAsync();
    }
}
