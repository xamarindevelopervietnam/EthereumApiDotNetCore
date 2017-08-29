using System;
using System.IO;
using EthereumApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.PlatformAbstractions;

namespace ApiRunner
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = $"Ethereum Self-hosted API - Ver. {PlatformServices.Default.Application.ApplicationVersion}";

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            Console.WriteLine("Web Server is running");
            Console.WriteLine($"Utc time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

            host.Run();
        }
    }
}