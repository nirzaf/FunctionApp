using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace FunctionApp.Triggers
{
    public class QueueTrigger
    {
        [FunctionName("QueueTrigger")]
        public void Run([Microsoft.Azure.Functions.Worker.QueueTrigger("localqueue", Connection = "AzureWebJobsStorage")]string myQueueItem, ILogger log)
        {
            log.LogInformation("C# Queue trigger function processed: {MyQueueItem}", myQueueItem);
        }
    }
}
