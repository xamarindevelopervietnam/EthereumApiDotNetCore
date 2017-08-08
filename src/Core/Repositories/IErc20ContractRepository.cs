using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Repositories
{
    //Store supported erc20 tokens
    public interface IErc20Contract
    {
        string TokenName { get; set; }
        string TokenAddress { get; set; }
    }

    public class Erc20Contract : IErc20Contract
    {
        public string TokenName    { get; set; }
        public string TokenAddress { get; set; }
    }

    public interface IErc20ContractRepository
    {
        Task ProcessAllAsync(Func<IEnumerable<IErc20Contract>, Task> processAction);
        Task<IErc20Contract> GetAsync(string address);
        Task InsertOrReplaceAsync(IErc20Contract erc20Contract);
        Task<IEnumerable<IErc20Contract>> GetAllAsync();
    }
}
