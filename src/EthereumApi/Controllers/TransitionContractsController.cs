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
using Nethereum.Util;

namespace EthereumApi.Controllers
{
    [Route("api/transition")]
    [Produces("application/json")]
    public class TransitionContractsController : Controller
    {
        private readonly ILog _logger;
        private readonly ITransferContractService _transferContractService;
        private readonly AddressUtil _addressUtil;
        private readonly IDepositContractService _depositContractService;

        public TransitionContractsController(ITransferContractService transferContractService, ILog logger, IDepositContractService depositContractService)
        {
            _depositContractService = depositContractService;
            _addressUtil = new AddressUtil();
            _transferContractService = transferContractService;
            _logger = logger;
        }

        [Route("create")]
        [HttpPost]
        [ProducesResponseType(typeof(RegisterResponse), 200)]
        [ProducesResponseType(typeof(ApiException), 400)]
        [ProducesResponseType(typeof(ApiException), 500)]
        public async Task<IActionResult> CreateTransferContract([FromBody]CreateTransitionContractModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            string contractAddress =await _depositContractService.AssignDepositContractToUserAsync(
                _addressUtil.ConvertToChecksumAddress(model.UserAddress));

            return Ok(new RegisterResponse
            {
                Contract = contractAddress
            });
        }

        [Route("depositContractAddress/{userAddress}")]
        [HttpGet]
        [ProducesResponseType(typeof(RegisterResponse), 200)]
        [ProducesResponseType(typeof(ApiException), 400)]
        [ProducesResponseType(typeof(void), 404)]
        [ProducesResponseType(typeof(ApiException), 500)]
        public async Task<IActionResult> GetAddress(string userAddress)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IDepositContract contract = await _depositContractService.GetDepositContract(_addressUtil.ConvertToChecksumAddress(userAddress));

            if (contract == null)
            {
                return NotFound();
            }

            return Ok(new RegisterResponse
            {
                Contract = contract.ContractAddress
            });
        }

        [Route("contractAddress/{userAddress}/{coinAdapterAddress}")]
        [HttpGet]
        [ProducesResponseType(typeof(RegisterResponse), 200)]
        [ProducesResponseType(typeof(ApiException), 400)]
        [ProducesResponseType(typeof(void), 404)]
        [ProducesResponseType(typeof(ApiException), 500)]
        public async Task<IActionResult> GetAddress(string userAddress, string coinAdapterAddress)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ITransferContract contract = await _transferContractService.GetTransferContract(_addressUtil.ConvertToChecksumAddress(userAddress),
                coinAdapterAddress);

            if (contract == null)
            {
                return NotFound();
            }

            return Ok(new RegisterResponse
            {
                Contract = contract.ContractAddress
            });
        }
    }
}
