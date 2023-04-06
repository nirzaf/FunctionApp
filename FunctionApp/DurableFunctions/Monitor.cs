using System.Collections.Concurrent;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionApp.DurableFunctions
{
    public class Monitor
    {
        
        private static readonly ConcurrentDictionary<string, string> State = new ConcurrentDictionary<string, string>();

        public class Job
        {
            public string JobId { get; set; }
            public DateTime ExpiryTime { get; set; }
        }


        [FunctionName("Monitor_HttpStart")]
        public async Task<IActionResult> Monitor_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string jobId = req.Query["JobId"];

            if (string.IsNullOrEmpty(jobId))
            {
                return new BadRequestObjectResult("Pass JobId parameter");
            }

            var expirTime = DateTime.UtcNow.AddSeconds(30);

            string instanceId = await starter.StartNewAsync(nameof(this.MonitorJobStatus), new Job()
            {
                JobId = jobId,
                ExpiryTime = expirTime,
            });

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }


        [FunctionName("MonitorJobStatus")]
        public async Task MonitorJobStatus(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var job = context.GetInput<Job>();
            int pollingInterval = 5;

            while (context.CurrentUtcDateTime < job.ExpiryTime)
            {
                var jobStatus = await context.CallActivityAsync<string>(
                    "GetJobStatus", job.JobId);
                if (jobStatus == "Completed")
                {
                    // Perform an action when a condition is met.
                    await context.CallActivityAsync(
                        "SendAlert", $"Job({job.JobId}) Completed.");
                    break;
                }

                // Orchestration sleeps until this time.
                var nextCheck = context.CurrentUtcDateTime.AddSeconds(
                    pollingInterval);
                await context.CreateTimer(nextCheck, CancellationToken.None);
            }
        }

        [FunctionName("SendAlert")]
        public void SendAlert([ActivityTrigger] string message, ILogger _log)
        {
            _log.LogInformation($"SendAlert = {message}");
        }

        [FunctionName(nameof(GetJobStatus))]
        public Task<string> GetJobStatus([ActivityTrigger] IDurableActivityContext context)
        {
            var jobId = context.GetInput<string>();

            var status = State.AddOrUpdate(jobId, "Scheduled", (key, oldValue) =>
            {
                switch (oldValue)
                {
                    case "Scheduled":
                        return "Running";
                    case "Running":
                        return "Completed";
                    default:
                        return "Failed";
                }
            });
            return Task.FromResult(status);
        }
    }
}