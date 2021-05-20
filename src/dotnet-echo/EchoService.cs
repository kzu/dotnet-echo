using System.Threading.Tasks;
using Grpc.Core;

namespace DotNet
{
    class EchoService : chamber.chamberBase
    {
        public override Task<message> echo(message request, ServerCallContext context) 
            => Task.FromResult(request);
    }
}
