using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Paycor.Payroll.PayrollProcessing
{
    public partial class PayrunProcessing
    {
        private readonly ILogger _log;
        private readonly ISettings _settings;
        private readonly IDatabase _database;
        private readonly Random _random = new Random(Guid.NewGuid().GetHashCode());

        const string PostPayrun = nameof(PostPayrun);
        const string PayrunPosted = nameof(PayrunPosted);

        const string ProcessCaps = nameof(ProcessCaps);
        const string CapsProcessed = nameof(CapsProcessed);

        const string DistributePayrun = nameof(DistributePayrun);        
        const string PayrunDistributed = nameof(PayrunDistributed);

        public PayrunProcessing(ILogger<PayrunProcessing> log, ISettings settings, IDatabase database)
        {
            _log = log;
            _settings = settings;
            _database = database;
        }

        [FunctionName(nameof(PayrunProcessing_HttpStart))]
        public async Task<HttpResponseMessage> PayrunProcessing_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [DurableClient]IDurableClient starter,
            ILogger log)
        {
            var clientId = (_random.Next(1, 100) % 5 + 1).ToString();
            var payrunId = _database.StringIncrement($"client::{clientId}");
            var request = new Request { ClientId = clientId, PayrunId = payrunId };

            string instanceId = await starter.StartNewAsync(nameof(PayrunProcessing), request);
            log.LogWarning($"Started orchestration for instance: {instanceId}, client:  {clientId}, payrun:  {payrunId}.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName(nameof(PayrunProcessing))]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var request = context.GetInput<Request>();

            await context.CallActivityAsync(nameof(PayrunProcessing_SendOrderedCommand), new Tuple<string, string, Request>(PostPayrun, context.InstanceId, request));
            var response1 = await context.WaitForExternalEvent<Request>(PayrunPosted);
            
            await context.CallActivityAsync(nameof(PayrunProcessing_SendOrderedCommand), new Tuple<string, string, Request>(ProcessCaps, context.InstanceId, response1));
            var response2 = await context.WaitForExternalEvent<Request>(CapsProcessed);

            await context.CallActivityAsync(nameof(PayrunProcessing_SendOrderedCommand), new Tuple<string, string, Request>(DistributePayrun, context.InstanceId, response2));
            var response3 = await context.WaitForExternalEvent<Request>(PayrunDistributed);

            return;
        }

        [FunctionName(nameof(PayrunProcessing_SendOrderedCommand))]
        public async Task PayrunProcessing_SendOrderedCommand([ActivityTrigger] Tuple<string, string, Request> input)
        {
            var queueName = input.Item1;
            var instanceId = input.Item2;
            var request = input.Item3;
          
            var client = new QueueClient(_settings.ServiceBusConnectionString, queueName);
            var body = JsonConvert.SerializeObject(request.PayrunId);
            await client.SendAsync(new Message { CorrelationId = instanceId, SessionId = request.ClientId, Body = Encoding.UTF8.GetBytes(body) });
            await client.CloseAsync();

            return;
        }

        [FunctionName(nameof(PayrunProcessing_HandlePostPayrun))]
        public async Task PayrunProcessing_HandlePostPayrun(
            [ServiceBusTrigger(PostPayrun, Connection = "ServiceBusConnectionString", IsSessionsEnabled = true)] Message message, 
            IMessageSession messageSession,
            [DurableClient] IDurableOrchestrationClient client)
        {
            var instanceId = message.CorrelationId;
            var clientId = messageSession.SessionId;
            var payrunId = JsonConvert.DeserializeObject<long>(Encoding.UTF8.GetString(message.Body));
            
            try
            {
                var request = new Request { ClientId = clientId, PayrunId = payrunId };
                _log.LogWarning($"Posting payrun for instance:  {instanceId}, client:  {request.ClientId},  payrun:  {request.PayrunId}.");
                await client.RaiseEventAsync(instanceId, PayrunPosted, request);
            } catch
            {
                //Instance does not exist anymore
            }
        }

        [FunctionName(nameof(PayrunProcessing_HandleProcessCaps))]
        public async Task PayrunProcessing_HandleProcessCaps(
            [ServiceBusTrigger(ProcessCaps, Connection = "ServiceBusConnectionString", IsSessionsEnabled = true)] Message message,
            IMessageSession messageSession,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var instanceId = message.CorrelationId;
            var clientId = messageSession.SessionId;
            var payrunId = JsonConvert.DeserializeObject<long>(Encoding.UTF8.GetString(message.Body));

            try
            {
                var request = new Request { ClientId = clientId, PayrunId = payrunId };
                _log.LogWarning($"Processing caps for instance:  {instanceId}, client:  {request.ClientId},  payrun:  {request.PayrunId}.");
                await client.RaiseEventAsync(instanceId, CapsProcessed, request);
            }
            catch
            {
                //Instance does not exist anymore
            }
        }

        [FunctionName(nameof(PayrunProcessing_HandleDistributePayrun))]
        public async Task PayrunProcessing_HandleDistributePayrun(
            [ServiceBusTrigger(DistributePayrun, Connection = "ServiceBusConnectionString", IsSessionsEnabled = true)] Message message,
            IMessageSession messageSession,
            [DurableClient] IDurableOrchestrationClient client,
            ILogger log)
        {
            var instanceId = message.CorrelationId;
            var clientId = messageSession.SessionId;
            var payrunId = JsonConvert.DeserializeObject<long>(Encoding.UTF8.GetString(message.Body));

            try
            {
                var request = new Request { ClientId = clientId, PayrunId = payrunId };
                _log.LogWarning($"Distributing payrun for instance:  {instanceId}, client:  {request.ClientId},  payrun:  {request.PayrunId}.");
                await client.RaiseEventAsync(instanceId, PayrunDistributed, request);
            }
            catch
            {
                //Instance does not exist anymore
            }
        }
    }
}