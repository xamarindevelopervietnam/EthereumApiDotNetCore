using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Repositories
{
    public interface ITransactionInfo
    {
        string TransactionHash { get; set; }
        DateTime Date { get; set; }
        string From { get; set; }
        string Nonce { get; set; }
    }

    public class TransactionInfo : ITransactionInfo
    {
        public string TransactionHash { get; set; }
        public DateTime Date { get; set; }
        public string From { get; set; }
        public string Nonce { get; set; }
    }

    public interface ITransactionInfoRepository
    {
        Task SaveAsync(ITransactionInfo transferContract);
        Task<ITransactionInfo> GetAsync(string txHash);
    }
}
