﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Log;
using Core.Settings;
using Core.Timers;
using Core.Utils;
using Services;
using Services.Coins;

namespace EthereumJobs.Job
{
	public class ListenCoinContactsEvents : TimerPeriod
	{		
		private readonly ICoinContractService _coinContractService;
		private const int TimerPeriodSeconds = 5;

		private bool _recreateFilters = false;

		public ListenCoinContactsEvents(ILog log, ICoinContractService coinContractService)
			: base("ListenCoinContactsEvents", TimerPeriodSeconds * 1000, log)
		{			
			_coinContractService = coinContractService;
		}

		public override async Task Execute()
		{
			try
			{
				await _coinContractService.RetrieveEventLogs(_recreateFilters);
				_recreateFilters = false;
			}
			catch (Exception ex)
			{
				if (ex.IsNodeDown())
					_recreateFilters = true;
				throw;
			}
		}
	}
}
