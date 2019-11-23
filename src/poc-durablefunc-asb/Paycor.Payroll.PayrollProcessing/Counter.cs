using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Paycor.Payroll.PayrollProcessing
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Counter
    {
        [JsonProperty("value")]
        public int Value { get; set; }

        public void Increment()
        {
            this.Value += 1;
        }

        public Task<int> Get()
        {
            return Task.FromResult(this.Value);
        }

        public Task Reset()
        {
            this.Value = 0;
            return Task.CompletedTask;
        }

        [FunctionName(nameof(Counter))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<Counter>();
    }
}
