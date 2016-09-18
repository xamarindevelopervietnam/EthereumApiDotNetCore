﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Core.Settings;
using EthereumJobs;
using Microsoft.Extensions.DependencyInjection;
using EthereumJobs.Config;

namespace JobRunner
{
    public class Program
    {
        public static void Main(string[] args)
        {
			Console.Clear();
	        Console.Title = "Ethereum Web Job - Ver. " + Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion;

			var settings = GetSettings();
			if (settings == null)
			{
				Console.WriteLine("Press any key to exit...");
				Console.ReadKey();
				return;
			}

			try
			{
				CheckSettings(settings);
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				Console.WriteLine("Press any key to exit...");
				Console.ReadKey();
				return;
			}

			Console.WriteLine("Settings checked!");
			
			try
			{
				var app = new JobApp();
				app.Run(settings);
			}
			catch (Exception e)
			{
				Console.WriteLine("cannot start jobs! Exception: " + e.Message);
				Console.WriteLine("Press any key to exit...");
				Console.ReadKey();
				return;
			}

			Console.WriteLine("Web job started");
			Console.WriteLine("Utc time: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

			Console.WriteLine("Press 'q' to quit.");

			while (Console.ReadLine() != "q") continue;
		}

		static BaseSettings GetSettings()
		{
			var settingsData = ReadSettingsFile();

			if (string.IsNullOrWhiteSpace(settingsData))
			{
				Console.WriteLine("Please, provide generalsettings.json file");
				return null;
			}

			BaseSettings settings = GeneralSettingsReader.ReadSettingsFromData<BaseSettings>(settingsData);

			return settings;
		}

		static string ReadSettingsFile()
		{
			try
			{
#if DEBUG
				return File.ReadAllText(@"..\..\settings\generalsettings.json");
#else
				return File.ReadAllText("generalsettings.json");
#endif
			}
			catch (Exception e)
			{
				return null;
			}
		}

		static void CheckSettings(BaseSettings settings)
		{
			if (string.IsNullOrWhiteSpace(settings.EthereumMainAccount))
				throw new Exception("EthereumMainAccount is missing");
			if (string.IsNullOrWhiteSpace(settings.EthereumMainAccountPassword))
				throw new Exception("EthereumMainAccountPassword is missing");
			if (string.IsNullOrWhiteSpace(settings.EthereumMainContractAddress))
				throw new Exception("EthereumMainContractAddress is missing");
			if (string.IsNullOrWhiteSpace(settings.EthereumPrivateAccount))
				throw new Exception("EthereumPrivateAccount is missing");
			if (string.IsNullOrWhiteSpace(settings.EthereumUrl))
				throw new Exception("EthereumUrl is missing");

			if (string.IsNullOrWhiteSpace(settings.Db?.DataConnString))
				throw new Exception("DataConnString is missing");
			if (string.IsNullOrWhiteSpace(settings.Db?.LogsConnString))
				throw new Exception("LogsConnString is missing");
			if (string.IsNullOrWhiteSpace(settings.Db?.ExchangeQueueConnString))
				throw new Exception("ExchangeQueueConnString is missing");
			if (string.IsNullOrWhiteSpace(settings.Db?.EthereumNotificationsConnString))
				throw new Exception("EthereumNotificationsConnString is missing");

			if (string.IsNullOrWhiteSpace(settings.MainContract?.Abi))
				throw new Exception("MainContract abi is invalid");
			if (string.IsNullOrWhiteSpace(settings.MainContract?.ByteCode))
				throw new Exception("MainContract bytecode is invalid");

			if (string.IsNullOrWhiteSpace(settings.UserContract?.Abi))
				throw new Exception("UserContract abi is invalid");
			if (string.IsNullOrWhiteSpace(settings.UserContract?.ByteCode))
				throw new Exception("UserContract bytecode is invalid");
		}
	}
}
