using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionApp.DurableFunctions
{
    public static class AsyncHttpApi
    {
        // Client
        [FunctionName("AsyncHttpApi_HttpTrigger")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync("AsyncHttpApi_OrchestrationTrigger", null);

            log.LogInformation($"HttpTrigger Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        // Orchestration
        [FunctionName("AsyncHttpApi_OrchestrationTrigger")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            try
            {
                var outputs = new List<string>();

                outputs.Add(await context.CallActivityAsync<string>("AsyncHttpApi_ActivityTrigger", "Tokyo"));
                outputs.Add(await context.CallActivityAsync<string>("AsyncHttpApi_ActivityTrigger", "Delhi"));
                outputs.Add(await context.CallActivityAsync<string>("AsyncHttpApi_ActivityTrigger", "London"));

                return outputs;
            }
            catch (Exception)
            {
                throw;
            }
        }

        [FunctionName("AsyncHttpApi_ActivityTrigger")]
        public static string SayHelloActivity([ActivityTrigger] IDurableActivityContext activityContext, ILogger log)
        {
            log.LogInformation($"ActivityTrigger Started.");

            Thread.Sleep(5000);

            string name = activityContext.GetInput<string>();
            return $"Hello {name}!";
        }
    }
}