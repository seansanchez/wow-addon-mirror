using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AddonMirror.Functions;

public class Function
{
    [FunctionName("Function")]
    public void Run([TimerTrigger(Constants.CronTriggers.Every1Minute)] TimerInfo myTimer, ILogger log)
    {
        log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
    }
}
