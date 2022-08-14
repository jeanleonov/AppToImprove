using AppToImprove.Configuration;
using AppToImprove.Engine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using System;
using System.Net.Http;

namespace AppToImprove
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var weatherForecastUrl = Configuration.GetValue<string>(
                ConfigurationConstants.Properties.WeatherForecastUrl)
                ?? "http://localhost:5000";

            services.AddHttpClient(ConfigurationConstants.HttpClients.ForecastProvider,
                c => c.BaseAddress = new Uri(weatherForecastUrl))
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            services.AddMemoryCache();

            services.AddSingleton<IWeatherForecastService, WeatherForecastService>();

            services.AddMvcCore();
        }

        static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            var delay = Backoff.DecorrelatedJitterBackoffV2(
                medianFirstRetryDelay: TimeSpan.FromSeconds(1),
                retryCount: 5);

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(delay);
        }

        static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(builder =>
            {
                builder.MapControllers();
            });
        }
    }
}