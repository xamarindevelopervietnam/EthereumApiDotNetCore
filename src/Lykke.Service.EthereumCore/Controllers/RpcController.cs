﻿using Lykke.Service.EthereumCore.BusinessModels;
using Lykke.Service.EthereumCore.Core.Exceptions;
using Lykke.Service.EthereumCore.Models;
using Lykke.Service.EthereumCore.Models.Models;
using Lykke.Service.EthereumCore.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Lykke.Service.EthereumCore.Services;
using Lykke.Service.EthereumCore.Services.PrivateWallet;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Service.EthereumCore.Controllers
{
    [Route("api/rpc")]
    [Produces("application/json")]
    public class RpcController : Controller
    {
        private readonly IEthereumIndexerService _ethereumIndexerService;
        private readonly IWeb3 _web3;

        public RpcController(IEthereumIndexerService ethereumIndexerService, IWeb3 web3)
        {
            _web3 = web3;
            _ethereumIndexerService = ethereumIndexerService;
        }

        [HttpGet("getBalance/{address}")]
        [ProducesResponseType(typeof(BalanceModel), 200)]
        [ProducesResponseType(typeof(ApiException), 400)]
        [ProducesResponseType(typeof(ApiException), 500)]
        public async Task<IActionResult> GetBalanceAsync([FromRoute] string address)
        {
            if (!ModelState.IsValid)
            {
                throw new ClientSideException(ExceptionType.WrongParams, JsonConvert.SerializeObject(ModelState.Errors()));
            }

            BigInteger balance = await _ethereumIndexerService.GetEthBalance(address);

            return Ok(new BalanceModel() {
                Amount = balance.ToString()
            });
        }

        [HttpGet("getNetworkGasPrice")]
        [ProducesResponseType(typeof(BalanceModel), 200)]
        [ProducesResponseType(typeof(ApiException), 400)]
        [ProducesResponseType(typeof(ApiException), 500)]
        public async Task<IActionResult> GetNetworkGasPrice()
        {
            BigInteger currentGasPriceHex = await _web3.Eth.GasPrice.SendRequestAsync();

            return Ok(new BalanceModel()
            {
                Amount = currentGasPriceHex.ToString()
            });
        }
    }
}
