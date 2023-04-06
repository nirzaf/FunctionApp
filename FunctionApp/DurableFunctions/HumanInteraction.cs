using System;
using System.IO;
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
    public class HumanInteraction
    {
        [FunctionName("HumanInteraction")]
        public async Task<IActionResult> HumanInteraction_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync("ApprovalWorkflow", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("ApprovalWorkflow")]
        public static async Task Run([OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            await context.CallActivityAsync("RequestApproval", "Email");
            using (var timeoutCts = new CancellationTokenSource())
            {
                // One day to respond
                DateTime dueTime = context.CurrentUtcDateTime.AddHours(24);

                // Timer
                Task durableTimeout = context.CreateTimer(dueTime, timeoutCts.Token);

                // Wait for Human interaction
                Task<bool> approvalEvent = context.WaitForExternalEvent<bool>("ApprovalEvent");

                // Escalate in no human interaction done
                if (approvalEvent == await Task.WhenAny(approvalEvent, durableTimeout))
                {
                    timeoutCts.Cancel();
                    await context.CallActivityAsync("ProcessApproval", approvalEvent.Result);
                }
                else
                {
                    await context.CallActivityAsync("Escalate", "Escalated!");
                }
            }
        }

        [FunctionName("RequestApproval")]
        public static void RequestApproval([ActivityTrigger] string message, ILogger log)
        {
            log.LogInformation(string.Format("{0} Send to Manager!", message));
            return;
        }

        [FunctionName("ProcessApproval")]
        public static void ProcessApproval([ActivityTrigger] bool isApproved, ILogger log)
        {
            if (isApproved)
            {
                log.LogInformation("Approved!");
            }
            else
            {
                log.LogInformation("Rejected!");
            }
        }

        [FunctionName("Escalate")]
        public static void Escalate([ActivityTrigger] string message, ILogger log)
        {
            log.LogInformation(message);
        }

        [FunctionName("RaiseApprovalEvent")]
        public static async Task ApprovalEvent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await StreamToStringAsync(req);

            log.LogInformation($"To raise event, Orchestration ID = '{instanceId}'.");
            bool isApproved = true;
            await starter.RaiseEventAsync(instanceId, "ApprovalEvent", isApproved);
        }

        private static async Task<string> StreamToStringAsync(HttpRequest request)
        {
            using (var sr = new StreamReader(request.Body))
            {
                return await sr.ReadToEndAsync();
            }
        }
    }
}