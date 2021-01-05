using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using TodoAPIPrep.Common.Entities;
using TodoAPIPrep.Common.Models;
using TodoAPIPrep.Common.Responses;

namespace TodoAPIPrep.Functions.Functions
{
    public static class TodoApi
    {
        [FunctionName(nameof(CreateTodo))]
        public static async Task<IActionResult> CreateTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todo")] HttpRequest req,
            [Table("todo", Connection = "AzureWebJobsStorage")] CloudTable todoTable,
            ILogger log)
        {
            log.LogInformation("Recieved a new todo.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Todo todo = JsonConvert.DeserializeObject<Todo>(requestBody);

            if (string.IsNullOrEmpty(todo?.TaskDescription))
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "The request must have a TaskDescription"
                });
            }

            TodoEntity todoEntity = new TodoEntity
            {
                CreatedTime = DateTime.UtcNow,
                ETag = "*",
                IsCompleted = false,
                PartitionKey = "TODO",
                RowKey = Guid.NewGuid().ToString(),
                TaskDescription = todo.TaskDescription
            };

            try
            {
                TableOperation addOperation = TableOperation.Insert(todoEntity);
                await todoTable.ExecuteAsync(addOperation);
                string message = "New todo stored in table";
                log.LogInformation(message);

                return new OkObjectResult(new Response
                {
                    IsSuccess = true,
                    Message = message,
                    Result = todoEntity
                });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = ex.Message
                });
            }
        }

        [FunctionName(nameof(UpdateTodo))]
        public static async Task<IActionResult> UpdateTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todo/{id}")] HttpRequest req,
            [Table("todo", Connection = "AzureWebJobsStorage")] CloudTable todoTable,
            string id,
            ILogger log)
        {
            log.LogInformation($"Update for todo: {id}, received.");

            // Validate report id
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Todo todoRequest = JsonConvert.DeserializeObject<Todo>(requestBody);
            TableOperation findOperation = TableOperation.Retrieve<TodoEntity>("TODO", id);
            TableResult findResult = await todoTable.ExecuteAsync(findOperation);
            if (findResult.Result == null)
            {
                return new NotFoundObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Todo not found."
                });
            }

            // Update information for the todo
            TodoEntity todoEntity = (TodoEntity)findResult.Result;
            todoEntity.IsCompleted = (todoRequest?.IsCompleted) != null && todoRequest.IsCompleted;
            if (!string.IsNullOrEmpty(todoRequest?.TaskDescription))
            {
                todoEntity.TaskDescription = todoRequest.TaskDescription;
            }

            try
            {
                TableOperation replaceOperation = TableOperation.Replace(todoEntity);
                await todoTable.ExecuteAsync(replaceOperation);

                string message = $"Todo: {id}, updated in table";
                log.LogInformation(message);

                return new OkObjectResult(new Response
                {
                    IsSuccess = true,
                    Message = message,
                    Result = todoEntity
                });
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = ex.Message
                });
            }
        }

        [FunctionName(nameof(GetAllTodos))]
        public static async Task<IActionResult> GetAllTodos(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todo")] HttpRequest req,
            [Table("todo", Connection = "AzureWebJobsStorage")] CloudTable todoTable,
            ILogger log)
        {
            log.LogInformation("Get all todos received.");

            TableQuery<TodoEntity> query = new TableQuery<TodoEntity>();
            TableQuerySegment<TodoEntity> todos = await todoTable.ExecuteQuerySegmentedAsync(query, null);

            string message = "Retrieve all todos.";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = todos
            });
        }

        [FunctionName(nameof(GetTodoById))]
        public static IActionResult GetTodoById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todo/{id}")] HttpRequest req,
            [Table("todo", "TODO", "{id}")] TodoEntity todoEntity,
            ILogger log)
        {
            if (todoEntity == null)
            {
                return new NotFoundObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Todo not found."
                });
            }

            log.LogInformation($"Get for todo: {todoEntity.RowKey}, received.");

            // Send response
            string message = $"Todo: {todoEntity.RowKey}, retrieved.";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = todoEntity
            });
        }

        [FunctionName(nameof(DeleteTodo))]
        public static async Task<IActionResult> DeleteTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todo/{id}")] HttpRequest req,
            [Table("todo", "TODO", "{id}")] TodoEntity todoEntity,
            [Table("todo", Connection = "AzureWebJobsStorage")] CloudTable todoTable,
            ILogger log)
        {
            if (todoEntity == null)
            {
                return new NotFoundObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Todo not found."
                });
            }

            log.LogInformation($"Delete for todo: {todoEntity.RowKey}, received.");
            await todoTable.ExecuteAsync(TableOperation.Delete(todoEntity));

            // Send response
            string message = $"Todo: {todoEntity.RowKey}, deleted.";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = todoEntity
            });
        }
    }
}
