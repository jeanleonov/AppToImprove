using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AppToImprove.Tests
{
    /// <summary>
    /// Starts fake server which can be configured in integration tests.
    /// </summary>
    public class RemoteServerFixture
    {
        private static int _portShift = -1;
        private IWebHost _host;

        public RemoteServerFixture()
        {
            bool succeeded = false;

            for (var tryCount = 0; tryCount < 10; tryCount++)
            {
                Port = 36501 + Interlocked.Increment(ref _portShift);

                try
                {
                    var _host = new WebHostBuilder()
                        .UseUrls($"http://localhost:{Port}")
                        .UseKestrel()
                        .UseIISIntegration()
                        .Configure(app =>
                        {
                            app.Run(RequestHandler);
                        })
                        .Build();

                    _host.Start();

                    succeeded = true;
                }
                catch (IOException ex) when (ex.InnerException is AddressInUseException)
                {

                }
            }

            if (!succeeded)
                throw new InvalidOperationException("Cannot start a mock server.");
        }

        private Task RequestHandler(HttpContext context)
        {
            foreach (var handler in _handlers)
            {
                if (handler.Predicate(context.Request))
                {
                    return handler.Action(context.Response);
                }
            }

            context.Response.StatusCode = 404;
            return Task.CompletedTask;
        }

        List<(Predicate<HttpRequest> Predicate, Func<HttpResponse, Task> Action)> _handlers = new();

        public void Reset()
        {
            _handlers.Clear();
        }

        public void RegisterJsonResponse(string method, string path, string content)
        {
            _handlers.Add((
                req => string.Equals(method, req.Method, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(path, req.Path.Value, StringComparison.OrdinalIgnoreCase),
                resp =>
                {
                    resp.StatusCode = 200;
                    resp.ContentType = "application/json";
                    return resp.WriteAsync(content);
                }

            ));
        }

        public void RegisterJsonResponse(string method, string path, Stream content)
        {
            _handlers.Add((
                req => string.Equals(method, req.Method, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(path, req.Path.Value, StringComparison.OrdinalIgnoreCase),
                resp =>
                {
                    resp.StatusCode = 200;
                    resp.ContentType = "application/json";
                    return content.CopyToAsync(resp.Body);
                }
            ));
        }

        public int Port { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            _host?.Dispose();
        }
    }
}
