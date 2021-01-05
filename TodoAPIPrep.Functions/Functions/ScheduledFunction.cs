using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using TodoAPIPrep.Common.Entities;

namespace TodoAPIPrep.Functions.Functions
{
    public static class ScheduledFunction
    {
        [FunctionName(nameof(ScheduledFunction))]
        public static async Task Run(
            [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer,
            [Table("todo", Connection = "AzureWebJobsStorage")] CloudTable todoTable,
            ILogger log)
        {
            log.LogInformation($"Deleting completed todos recieved at: {DateTime.Now}");
            string filter = TableQuery.GenerateFilterConditionForBool("IsCompleted", QueryComparisons.Equal, true);

            TableQuery<TodoEntity> query = new TableQuery<TodoEntity>().Where(filter);
            TableQuerySegment<TodoEntity> completedTodos = await todoTable.ExecuteQuerySegmentedAsync(query, null);
            int deleted = 0;
            foreach (TodoEntity todoEntity in completedTodos)
            {
                await todoTable.ExecuteAsync(TableOperation.Delete(todoEntity));
                deleted++;
            }
            log.LogInformation($"Deleted {deleted} items at {DateTime.Now}");
        }
    }
}
