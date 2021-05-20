using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace DotNet
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services) => services.AddGrpc();

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();
            app.Use(next => async context =>
            {
                if (context.GetEndpoint() != null)
                    await next(context);
                else
                    await EchoAsync(context);
            });
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<EchoService>());
        }

        static async Task EchoAsync(HttpContext http)
        {
            var cancellation = http.RequestAborted;
            AnsiConsole.MarkupLine($"[grey]      User-Agent: {http.Request.Headers["User-Agent"]}[/]");

            http.Response.ContentType = http.Request.ContentType;
            http.Response.ContentLength = http.Request.ContentLength;

            ReadResult result;
            do
            {
                result = await http.Request.BodyReader.ReadAsync(cancellation);
                http.Request.BodyReader.AdvanceTo(result.Buffer.Start, result.Buffer.End);

                if (!result.IsCanceled)
                    await http.Response.BodyWriter.WriteAsync(result.Buffer.ToArray());

            } while (!result.IsCanceled && !result.IsCompleted);

            await http.Request.BodyReader.CompleteAsync();
            await http.Response.BodyWriter.CompleteAsync();
        }
    }
}
