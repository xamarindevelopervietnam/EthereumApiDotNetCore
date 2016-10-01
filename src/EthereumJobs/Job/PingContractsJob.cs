﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Log;
using Core.Timers;
using Services;

namespace EthereumJobs.Job
{
	public class PingContractsJob : TimerPeriod
	{
		private readonly ICoinContractService _coinContractService;
		private const int TimerPeriodSeconds = 60 * 60 * 24;

		public PingContractsJob(ICoinContractService coinContractService, ILog log) : base("PingContractsJob", TimerPeriodSeconds * 1000, log)
		{
			_coinContractService = coinContractService;
		}

		public async override Task Execute()
		{
			await _coinContractService.PingMainExchangeContract();
			//TODO : ping coin contracts
		}
	}
}
