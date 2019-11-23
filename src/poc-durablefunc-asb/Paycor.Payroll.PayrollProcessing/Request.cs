namespace Paycor.Payroll.PayrollProcessing
{
    public partial class PayrunProcessing
    {
        /*
        [FunctionName(nameof(PayrunProcessingInOrder))]
        public async Task PayrunProcessingInOrder(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [DurableClient] IDurableOrchestrationClient client)
        {
            var request = context.GetInput<Request>();
            await ExecutePostPayrun(context.InstanceId, request);
            await ExecuteProcessCaps(context.InstanceId, request);
            await ExecuteDistributePayrun(context.InstanceId, request);

            await client.RaiseEventAsync(context.InstanceId, PayrunPosted, request);
        }
        */

        public class Request
        {
            public string ClientId { get; set; }
            public long PayrunId { get; set; }
        }
    }
}