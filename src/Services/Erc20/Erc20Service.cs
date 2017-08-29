using BusinessModels.Erc20;
using Core.Exceptions;
using Core.Repositories;
using EthereumSamuraiApiCaller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Erc20
{
    public interface IErc20Service
    {
        Task<IEnumerable<IErc20Contract>> GetAllAsync();
        Task AddSupportedErc20TokenAsync(IErc20Contract token);
        Task DeleteAsync(string address);
    }

    public class Erc20Service : IErc20Service
    {
        private readonly IErc20ContractRepository _erc20ContractRepository;

        public Erc20Service(IErc20ContractRepository erc20ContractRepository)
        {
            _erc20ContractRepository = erc20ContractRepository;
        }

        public async Task<IEnumerable<IErc20Contract>> GetAllAsync()
        {
            IEnumerable<IErc20Contract> all = await _erc20ContractRepository.GetAllAsync();

            return all;
        }

        public async Task AddSupportedErc20TokenAsync(IErc20Contract token)
        {
            IErc20Contract existingContract = await _erc20ContractRepository.GetAsync(token.TokenAddress);
            if (existingContract != null)
            {
                throw new ClientSideException(ExceptionType.EntityAlreadyExists, "Erc20 token is already supported");
            }

            await _erc20ContractRepository.InsertOrReplaceAsync(token);
        }

        public async Task DeleteAsync(string address)
        {
            await _erc20ContractRepository.DeleteAsync(address);
        }
    }
}
