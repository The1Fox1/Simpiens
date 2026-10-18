using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simpiens.Diagnostics;
using Simpiens.Hosting;

namespace Simpiens.Host
{
    /// <summary>
    /// The development console host (Epic CORE C10) on the Generic Host: Ctrl+C cancels the command between ticks. Each
    /// run composes its own simulation (HeadlessSimulation), so runs in one process are independent.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException)
            {
                Console.Error.WriteLine($"{e.Message}\n\n{Commands.Usage}");
                return 64;
            }

            ConsoleSimLogSink.Install();
            var logging = SimLogSettings.FromEnvironment();
            if (global::System.Environment.GetEnvironmentVariable("SIMPIENS_LOG") == null && Array.IndexOf(args, "-simlog") < 0)
            {
                logging.ConsoleMinimumLevel = LogLevel.Warning; // a soak's sales and bounties would bury the results
            }
            SimLog.Configure(logging);

            var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = Array.Empty<string>() });
            builder.Logging.ClearProviders(); // the simulation logs through SimLog; standard output is the command's
            builder.Services.Configure<ConsoleLifetimeOptions>(o => o.SuppressStatusMessages = true);
            builder.Services.AddSingleton(options);
            builder.Services.AddHostedService<CommandService>();
            using var host = builder.Build();
            try
            {
                await host.RunAsync();
            }
            finally
            {
                SimLog.Shutdown();
            }
            return global::System.Environment.ExitCode;
        }
    }

    /// <summary>Runs the command once, off the host's startup path, then stops the application.</summary>
    internal sealed class CommandService : BackgroundService
    {
        private readonly Options _options;
        private readonly IHostApplicationLifetime _lifetime;

        public CommandService(Options options, IHostApplicationLifetime lifetime)
        {
            _options = options;
            _lifetime = lifetime;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() =>
        {
            try
            {
                global::System.Environment.ExitCode = Commands.Run(_options, stoppingToken);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine($"{e.Message}\n\n{Commands.Usage}");
                global::System.Environment.ExitCode = 64;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                global::System.Environment.ExitCode = 1;
            }
            finally
            {
                _lifetime.StopApplication();
            }
        }, CancellationToken.None);
    }
}
