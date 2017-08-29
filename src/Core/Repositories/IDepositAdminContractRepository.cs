using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Repositories
{
    public interface IDepositAdminContract
    {
        string ContractAddress { get; set; }
        string OwnerAddress { get; set; }
    }

    public class DepositAdminContract : IDepositAdminContract
    {
        public string ContractAddress { get; set; }
        public string OwnerAddress { get; set; }
    }

    public interface IDepositAdminContractRepository
    {
        Task SaveAsync(IDepositAdminContract contract);
        Task<IDepositAdminContract> GetAsync();
    }
}
