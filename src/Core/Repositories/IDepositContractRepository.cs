using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Repositories
{
    public interface IDepositContract
    {
        string ContractAddress { get; set; }
        string UserAddress { get; set; }
        string EthAdapterAddress { get; set; }
        string AssignmentHash { get; set; }
    }

    public class DepositContract : IDepositContract
    {
        public string ContractAddress { get; set; }
        public string UserAddress { get; set; }
        public string EthAdapterAddress { get; set; }
        public string AssignmentHash { get; set; }
    }

    public interface IDepositContractRepository
    {
        Task SaveAsync(IDepositContract transferContract);
        Task<IDepositContract> GetByAddressAsync(string depositContractAddress);
        Task ProcessAllAsync(Func<IDepositContract, Task> processAction);
        Task<IDepositContract> GetByUserAsync(string userAddress);
    }
}
