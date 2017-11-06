using System;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using Core.Repositories;
using EthereumApi.Models;
using EthereumApiSelfHosted.Models;
using Microsoft.AspNetCore.Mvc;
using Nethereum.Util;
using Services;

namespace EthereumApi.Controllers
{
    //ForAdminOnly
    [Route("api/coinAdapter")]
    [Produces("application/json")]
    public class СoinAdapterController : Controller
    {
        private readonly AddressUtil          _addressUtil;
        private readonly AssetContractService _assetContractService;
        private readonly ILog                 _logger;

        public СoinAdapterController(
            AssetContractService assetContractService,
            ILog logger)
        {
            _addressUtil          = new AddressUtil();
            _assetContractService = assetContractService;
            _logger               = logger;
        }

        [Route("create/common")]
        [HttpPost]
        [ProducesResponseType(typeof(EventResponse), 200)]
        public async Task<IActionResult> CreateCoinAdapter([FromBody] CreateAssetModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var id = Guid.NewGuid();
            var asset = new Coin
            {
                ExternalTokenAddress     = model.ExternalTokenAddress,
                ContainsEth              = model.ContainsEth,
                Blockchain               = model.Blockchain,
                BlockchainDepositEnabled = true,
                Id                       = id.ToString(),
                Multiplier               = model.Multiplier,
                Name                     = model.Name
            };

            await _assetContractService.EnqueueCoinContractCreationAsync(asset);

            return Ok(new EventResponse
            {
                EventId = id
            });
        }

        [Route("create/erc20")]
        [HttpPost]
        [ProducesResponseType(typeof(EventResponse), 200)]
        public async Task<IActionResult> CreateCoinAdapterWithErc20SupportAsync([FromBody] CreateCoinAdapterModelWithToken model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var id = Guid.NewGuid();
            var asset = new Coin
            {
                ExternalTokenAddress = model.ExternalTokenAddress,
                ContainsEth = false,
                Blockchain = "Ethereum",
                BlockchainDepositEnabled = true,
                Id = id.ToString(),
                Multiplier = 1,
                Name = model.TokenName,
            };

            await _assetContractService.EnqueueCoinContractCreationAsync(asset);

            return Ok(new EventResponse
            {
                EventId = id
            });
        }

        [Route("balance/{coinAdapterAddress}/{userAddress}")]
        [HttpGet]
        [ProducesResponseType(typeof(BalanceModel), 200)]
        [ProducesResponseType(typeof(ApiException), 400)]
        [ProducesResponseType(typeof(ApiException), 500)]
        public async Task<IActionResult> CreateCoinAdapter(
            [FromRoute] string coinAdapterAddress,
            [FromRoute] string userAddress)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var amount = await _assetContractService.GetBalance
            (
                coinAdapterAddress,
                _addressUtil.ConvertToChecksumAddress(userAddress)
            );

            return Ok(new BalanceModel
            {
                Amount = amount.ToString()
            });
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CoinResult), 200)]
        [ProducesResponseType(typeof(void), 404)]
        public async Task<IActionResult> GetAdapter(string id)
        {
            return await GetCoinAdapter(id, _assetContractService.GetById);
        }

        [HttpGet("address/{adapterAddress}")]
        [ProducesResponseType(typeof(CoinResult), 200)]
        [ProducesResponseType(typeof(void), 404)]
        public async Task<IActionResult> GetAdapterByAddress(string adapterAddress)
        {
            return await GetCoinAdapter(adapterAddress, _assetContractService.GetByAddress);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ListResult<CoinResult>), 200)]
        public async Task<IActionResult> GetAllAdapters()
        {
            var allAdapters = await _assetContractService.GetAll();
            var result      = allAdapters.Select(x => new CoinResult
            {
                AdapterAddress           = x.AdapterAddress,
                Blockchain               = x.Blockchain,
                BlockchainDepositEnabled = x.BlockchainDepositEnabled,
                ContainsEth              = x.ContainsEth,
                ExternalTokenAddress     = x.ExternalTokenAddress,
                Id                       = x.Id,
                Multiplier               = x.Multiplier,
                Name                     = x.Name
            });

            return Ok(new ListResult<CoinResult>
            {
                Data = result
            });
        }

        //method was created for integration convinience
        [HttpGet("exists/{adapterAddress}")]
        [ProducesResponseType(typeof(ExistsModel), 200)]
        public async Task<IActionResult> IsValidAddress(string adapterAddress)
        {
            var coin = await _assetContractService.GetByAddress(adapterAddress);

            return Ok(new ExistsModel
            {
                Exists = coin != null
            });
        }

        private async Task<IActionResult> GetCoinAdapter(string argument, Func<string, Task<ICoin>> recieveFunc)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return BadRequest("identifier is missing");
            }
            
            var coinAdapter = await recieveFunc(argument); //(id);

            if (coinAdapter == null)
            {
                return NotFound();
            }
            
            var result = new CoinResult
            {
                AdapterAddress           = coinAdapter.AdapterAddress,
                Blockchain               = coinAdapter.Blockchain,
                BlockchainDepositEnabled = coinAdapter.BlockchainDepositEnabled,
                ContainsEth              = coinAdapter.ContainsEth,
                ExternalTokenAddress     = coinAdapter.ExternalTokenAddress,
                Id                       = coinAdapter.Id,
                Multiplier               = coinAdapter.Multiplier,
                Name                     = coinAdapter.Name
            };

            return Ok(result);
        }
    }
}