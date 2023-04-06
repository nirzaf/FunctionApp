using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace FunctionApp.DurableFunctions
{
    public static class FanOutFanIn
    {
        [FunctionName("FanOutFanIn")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var parallelTasks = new List<Task<int>>();

            // Get a list of 5 work items to process in parallel.
            object[] workBatch = await context.CallActivityAsync<object[]>("F1", null);
            for (int i = 0; i < workBatch.Length; i++)
            {
                Task<int> task = context.CallActivityAsync<int>("F2", workBatch[i]);
                parallelTasks.Add(task);
            }

            await Task.WhenAll(parallelTasks);

            // Sum all 5 outputs and send to F3 to print
            int sum = parallelTasks.Sum(t => t.Result);
            return await context.CallActivityAsync<string>("F3", sum);
        }

        [FunctionName("F1")]
        public static object[] F1([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to F1.");
            return new object[5];
        }

        [FunctionName("F2")]
        public static int F2([ActivityTrigger] object workBatch, ILogger log)
        {
            log.LogInformation($"Saying hello to {workBatch}.");
            return 1;
        }

        [FunctionName("F3")]
        public static string F3([ActivityTrigger] int sum, ILogger log)
        {
            log.LogInformation($"Sum of all {sum}.");
            return $"Total count {sum}.";
        }
    }
}