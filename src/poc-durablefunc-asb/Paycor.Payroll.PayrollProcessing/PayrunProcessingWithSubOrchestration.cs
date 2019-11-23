using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using StackExchange.Redis;
using static Paycor.Payroll.PayrollProcessing.PayrunProcessing;

namespace Paycor.Payroll.PayrollProcessing
{
    public partial class PayrunProcessingWithSubOrchestration
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

        const string ProcessPayrunInOrder = nameof(ProcessPayrunInOrder);
        const string PayrunProcessedInOrder = nameof(PayrunProcessedInOrder);

        public PayrunProcessingWithSubOrchestration(ILogger<PayrunProcessingWithSubOrchestration> log, ISettings settings, IDatabase database)
        {
            _log = log;
            _settings = settings;
            _database = database;
        }

        [FunctionName(nameof(PayrunProcessingInOrder_HttpStart))]
        public async Task<HttpResponseMessage> PayrunProcessingInOrder_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableClient starter,
            ILogger log)
        {
            var clientId = (_random.Next(1, 100) % 5 + 1).ToString();
            var payrunId = _database.StringIncrement($"client::{clientId}");
            var request = new Request { ClientId = clientId, PayrunId = payrunId };

            string instanceId = await starter.StartNewAsync(nameof(RunOrchestrator), request);
            log.LogWarning($"Started orchestration for instance: {instanceId}, client:  {clientId}, payrun:  {payrunId}.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName(nameof(RunOrchestrator))]
        public async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var request = context.GetInput<Request>();

            await context.CallActivityAsync(nameof(PayrunProcessingWithSubOrchestration_SendOrderedCommand), new Tuple<string, string, Request>(ProcessPayrunInOrder, context.InstanceId, request));
            await context.WaitForExternalEvent(PayrunProcessedInOrder);
        }

        [FunctionName(nameof(PayrunProcessingWithSubOrchestration_SendOrderedCommand))]
        public async Task PayrunProcessingWithSubOrchestration_SendOrderedCommand([ActivityTrigger] Tuple<string, string, Request> input)
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

        [FunctionName(nameof(PayrunProcessingWithSubOrchestration_HandleProcessPayrunInOrder))]
        public async Task PayrunProcessingWithSubOrchestration_HandleProcessPayrunInOrder(
            [ServiceBusTrigger(ProcessPayrunInOrder, Connection = "ServiceBusConnectionString", IsSessionsEnabled = true)] Message message, 
            IMessageSession messageSession,
            [DurableClient] IDurableOrchestrationClient client)
        {
            var instanceId = message.CorrelationId;
            var clientId = messageSession.SessionId;
            var payrunId = JsonConvert.DeserializeObject<long>(Encoding.UTF8.GetString(message.Body));
            var request = new Request { ClientId = clientId, PayrunId = payrunId };

            PayrunProcessingWithSubOrchestration_HandlePostPayrun(instanceId, request);
            PayrunProcessingWithSubOrchestration_HandleProcessCaps(instanceId, request);
            PayrunProcessingWithSubOrchestration_HandleDistributePayrun(instanceId, request);

            await client.RaiseEventAsync(instanceId, PayrunProcessedInOrder, request);
        }

        public void PayrunProcessingWithSubOrchestration_HandlePostPayrun(string instanceId, Request request)
        {
            Thread.Sleep(TimeSpan.FromSeconds(5));
            _log.LogWarning($"Posting payrun for instance:  {instanceId}, client:  {request.ClientId},  payrun:  {request.PayrunId}.");
        }

        public void PayrunProcessingWithSubOrchestration_HandleProcessCaps(string instanceId, Request request)
        {
            Thread.Sleep(TimeSpan.FromSeconds(5));
            _log.LogWarning($"Processing caps for instance:  {instanceId}, client:  {request.ClientId},  payrun:  {request.PayrunId}.");
        }

        public void PayrunProcessingWithSubOrchestration_HandleDistributePayrun(string instanceId, Request request)
        {
            Thread.Sleep(TimeSpan.FromSeconds(5));
            _log.LogWarning($"Distributing payrun for instance:  {instanceId}, client:  {request.ClientId},  payrun:  {request.PayrunId}.");
        }
    }
}