using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Core.Exceptions;
using EthereumApi.Models;
using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Coins;
using Common.Log;
using EthereumApiSelfHosted.Models;
using Core.Repositories;
using System.Numerics;
using Nethereum.Util;
using Services.Erc20;

namespace EthereumApi.Controllers
{
    //ForAdminOnly
    [Route("api/erc20Token")]
    [Produces("application/json")]
    public class Erc20TokenController : Controller
    {
        private readonly IErc20Service _erc20Service;
        private readonly ILog _logger;
        private readonly AddressUtil _addressUtil;

        public Erc20TokenController(IErc20Service erc20Service, ILog logger)
        {
            _addressUtil = new AddressUtil();
            _erc20Service = erc20Service;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ListResult<Erc20Result>), 200)]
        public async Task<IActionResult> GetAllTokens()
        {
            IEnumerable<IErc20Contract> all = await _erc20Service.GetAllAsync();
            IEnumerable<Erc20Result> result = all.Select(x => new Erc20Result()
            {
                TokenAddress = x.TokenAddress,
                TokenName = x.TokenName
            });

            return Ok(new ListResult<Erc20Result>()
            {
                Data = result
            });
        }

        [Route("create")]
        [HttpPost]
        [ProducesResponseType(typeof(void), 200)]
        public async Task<IActionResult> CreateCoinAdapter([FromBody]CreateErc20TokenModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IErc20Contract token = new Erc20Contract()
            {
                TokenAddress = model.Address,
                TokenName = model.Name
            };

            await _erc20Service.AddSupportedErc20TokenAsync(token);

            return Ok();
        }

        [Route("{address}")]
        [HttpPost]
        [ProducesResponseType(typeof(void), 200)]
        public async Task<IActionResult> CreateCoinAdapter([FromRoute] string address)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _erc20Service.DeleteAsync(address);

            return Ok();
        }
    }
}
