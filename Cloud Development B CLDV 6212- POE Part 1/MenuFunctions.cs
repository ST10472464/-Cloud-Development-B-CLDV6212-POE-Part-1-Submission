using Azure;
using Azure.Data.Tables;
using Cloud_Development_B_CLDV_6212__POE_Part_1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CoffeeNChill.Functions
{
    // (Microsoft Learn, 2026) (C# Azure Functions in the isolated worker model)
    public class MenuFunctions
    {
        private readonly ILogger<MenuFunctions> _logger;
        private readonly string _connectionString;
        private const string TableName = "MenuItems";

        public MenuFunctions(ILogger<MenuFunctions> logger)
        {
            _logger = logger;
            _connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";
        }

        [Function("CreateMenuItem")]
        public async Task<HttpResponseData> CreateMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "menu")] HttpRequestData req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var menuItem = JsonSerializer.Deserialize<MenuItem>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (menuItem == null || string.IsNullOrEmpty(menuItem.PartitionKey) || string.IsNullOrEmpty(menuItem.RowKey))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid data. PartitionKey (Category) and RowKey (ID) are required.");
                return badResponse;
            }

            // (Microsoft Learn, 2026) (Azure Tables client library)
            var tableClient = new TableClient(_connectionString, TableName);
            await tableClient.CreateIfNotExistsAsync();
            await tableClient.AddEntityAsync(menuItem);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(menuItem);
            return response;
        }

        // (Microsoft Learn, 2026)
        [Function("GetAllMenuItems")]
        public async Task<HttpResponseData> GetAllMenuItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu")] HttpRequestData req)
        {
            var tableClient = new TableClient(_connectionString, TableName);
            await tableClient.CreateIfNotExistsAsync();

            var menuItems = new List<MenuItem>();
            await foreach (var item in tableClient.QueryAsync<MenuItem>())
            {
                menuItems.Add(item);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(menuItems);
            return response;
        }

        // (Microsoft Learn, 2026)
        [Function("GetMenuItemsByCategory")]
        public async Task<HttpResponseData> GetMenuItemsByCategory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "menu/category/{category}")] HttpRequestData req,
            string category)
        {
            var tableClient = new TableClient(_connectionString, TableName);
            await tableClient.CreateIfNotExistsAsync();

            var menuItems = new List<MenuItem>();
            string filter = $"PartitionKey eq '{category}'";

            await foreach (var item in tableClient.QueryAsync<MenuItem>(filter: filter))
            {
                menuItems.Add(item);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(menuItems);
            return response;
        }

        // (Microsoft Learn, 2026) 
        [Function("UpdateMenuItem")]
        public async Task<HttpResponseData> UpdateMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "menu/{category}/{id}")] HttpRequestData req,
            string category, string id)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updatedData = JsonSerializer.Deserialize<MenuItem>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var tableClient = new TableClient(_connectionString, TableName);
            await tableClient.CreateIfNotExistsAsync();

            try
            {
                var existingItem = await tableClient.GetEntityAsync<MenuItem>(category, id);

                if (updatedData != null)
                {
                    existingItem.Value.Price = updatedData.Price;
                    existingItem.Value.IsAvailable = updatedData.IsAvailable;
                    if (!string.IsNullOrEmpty(updatedData.Name)) existingItem.Value.Name = updatedData.Name;
                    if (!string.IsNullOrEmpty(updatedData.Description)) existingItem.Value.Description = updatedData.Description;

                    await tableClient.UpdateEntityAsync(existingItem.Value, existingItem.Value.ETag, TableUpdateMode.Replace);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(existingItem.Value);
                return response;
            }
            catch (RequestFailedException)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Menu item with category '{category}' and ID '{id}' not found.");
                return notFoundResponse;
            }
        }

        // (Microsoft Learn, 2026)
        [Function("DeleteMenuItem")]
        public async Task<HttpResponseData> DeleteMenuItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "menu/{category}/{id}")] HttpRequestData req,
            string category, string id)
        {
            var tableClient = new TableClient(_connectionString, TableName);
            await tableClient.CreateIfNotExistsAsync();

            try
            {
                await tableClient.DeleteEntityAsync(category, id);
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Menu item '{id}' deleted successfully.");
                return response;
            }
            catch (RequestFailedException)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"Menu item with category '{category}' and ID '{id}' not found.");
                return notFoundResponse;
            }
        }
    }
}