using Grpc.Core;

namespace Devlooped
{
    class EchoService : chamber.chamberBase
    {
        public override Task<message> echo(message request, ServerCallContext context)
            => Task.FromResult(request);
    }
}
