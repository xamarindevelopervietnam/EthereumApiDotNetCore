﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Core.Exceptions;
using Core.Log;
using EthereumApi.Models;
using Microsoft.AspNetCore.Mvc;
using Services;
using Services.Coins;

namespace EthereumApi.Controllers
{
	[Route("api/coin")]
	[Produces("application/json")]
	public class CoinController : Controller
	{
		private readonly ICoinContractService _coinContractService;
		private readonly ILog _logger;

		public CoinController(ICoinContractService coinContractService, ILog logger)
		{
			_coinContractService = coinContractService;
			_logger = logger;
		}

		[Route("swap")]
		[HttpPost]
		[Produces(typeof(TransactionResponse))]
		public async Task<IActionResult> Swap([FromBody]SwapModel model)
		{
			if (!ModelState.IsValid)
				throw new BackendException(BackendExceptionType.MissingRequiredParams);

			await Log("Swap", "Begin Process", model);

			var transaction = await _coinContractService.Swap(model.Id, model.ClientA, model.ClientB, model.CoinA, model.CoinB,
				model.AmountA, model.AmountB, model.SignA, model.SignB);

			await Log("Swap", "End Process", model, transaction);

			return Ok(new TransactionResponse { TransactionHash = transaction });
		}

		[Route("cashout")]
		[HttpPost]
		[Produces(typeof(TransactionResponse))]
		public async Task<IActionResult> Cashout([FromBody]CashoutModel model)
		{
			if (!ModelState.IsValid)
				throw new BackendException(BackendExceptionType.MissingRequiredParams);

			await Log("Cashout", "Begin Process", model);

			var transaction = await _coinContractService.CashOut(model.Id, model.Coin, model.Client, model.To, model.Amount, model.Sign);

			await Log("Cashout", "End Process", model, transaction);

			return Ok(new TransactionResponse { TransactionHash = transaction });
		}

		[Route("cashin")]
		[HttpPost]
		[Produces(typeof(TransactionResponse))]
		public async Task<IActionResult> Cashin([FromBody]CashInModel model)
		{
			if (!ModelState.IsValid)
				throw new BackendException(BackendExceptionType.MissingRequiredParams);

			await Log("Cashin", "Begin Process", model);

			var transaction = await _coinContractService.CashIn(model.Id, model.Coin, model.Receiver, model.Amount);

			await Log("Cashin", "End Process", model, transaction);

			return Ok(new TransactionResponse { TransactionHash = transaction });
		}

		private async Task Log(string method, string status, object model, string transaction = "")
		{
			var properties = model.GetType().GetTypeInfo().GetProperties();
			var builder = new StringBuilder();
			foreach (var prop in properties)
				builder.Append($"{prop.Name}: [{prop.GetValue(model)}], ");

			if (!string.IsNullOrWhiteSpace(transaction))
				builder.Append($"Transaction: [{transaction}]");

			await _logger.WriteInfo("CoinController", method, status, builder.ToString());
		}
	}
}