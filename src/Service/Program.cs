using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NetDaemon;
using Serilog;

namespace Service
{
    internal class Program
    {
        private const string HassioConfigPath = "/data/options.json";

        public static async Task Main(string[] args)
        {
            try
            {
                Log.Logger = SerilogConfigurator.Configure().CreateLogger();

                await Host.CreateDefaultBuilder(args)
                    .UseSerilog(Log.Logger)
                    .UseNetDaemon()
                    .Build()
                    .RunAsync();
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Failed to start host...");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}