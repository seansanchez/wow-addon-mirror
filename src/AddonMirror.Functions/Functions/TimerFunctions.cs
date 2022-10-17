using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AddonMirror.Functions;

public class TimerFunctions
{
    [FunctionName("TimerFunctions")]
    public void Run([TimerTrigger(Constants.CronTriggers.Every1Minute)] TimerInfo myTimer, ILogger log)
    {
        log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
    }
}
