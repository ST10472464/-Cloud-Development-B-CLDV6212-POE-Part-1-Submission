using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Cloud_Development_B_CLDV_6212__POE_Part_1;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace CoffeeNChill.Functions
{
    public class OrderFunctions
    {
        private readonly ILogger<OrderFunctions> _logger;
        private readonly string _connectionString;
        private const string OrdersTableName = "Orders";
        private const string QueueName = "order-processing-queue";
        private const string PoisonQueueName = "order-processing-queue-poison";

        public OrderFunctions(ILogger<OrderFunctions> logger)
        {
            _logger = logger;
            _connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "UseDevelopmentStorage=true";
        }

        [Function("QueueOrder")]
        public async Task<HttpResponseData> QueueOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders/queue")] HttpRequestData req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var order = JsonSerializer.Deserialize<Order>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (order == null || string.IsNullOrEmpty(order.OrderId) || string.IsNullOrEmpty(order.CustomerName))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid order data. OrderId and CustomerName are required.");
                return badResponse;
            }

            if (order.SelectedItemSKUs == null || order.SelectedItemSKUs.Count == 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("At least one item SKU is required.");
                return badResponse;
            }

            if (order.TotalPrice <= 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("TotalPrice must be greater than zero.");
                return badResponse;
            }

            if (order.OrderTimestamp == default)
                order.OrderTimestamp = DateTime.UtcNow;

            var queueClient = new QueueClient(_connectionString, QueueName);
            await queueClient.CreateIfNotExistsAsync();

            string message = JsonSerializer.Serialize(order);
            string base64Message = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(message));
            await queueClient.SendMessageAsync(base64Message);

            _logger.LogInformation("Order {OrderId} queued for {CustomerName}", order.OrderId, order.CustomerName);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { message = "Order queued successfully", orderId = order.OrderId });
            return response;
        }

        [Function("ProcessOrderQueue")]
        public async Task ProcessOrderQueue(
            [QueueTrigger("order-processing-queue", Connection = "AzureWebJobsStorage")] string queueMessage,
            FunctionContext context)
        {
            Order? order = null;
            try
            {
                string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(queueMessage));
                order = JsonSerializer.Deserialize<Order>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize queue message, moving to poison queue");
                var poisonClient = new QueueClient(_connectionString, PoisonQueueName);
                await poisonClient.CreateIfNotExistsAsync();
                await poisonClient.SendMessageAsync(queueMessage);
                return;
            }

            if (order == null || string.IsNullOrEmpty(order.OrderId))
            {
                _logger.LogError("Invalid order payload in queue message");
                var poisonClient = new QueueClient(_connectionString, PoisonQueueName);
                await poisonClient.CreateIfNotExistsAsync();
                await poisonClient.SendMessageAsync(queueMessage);
                return;
            }

            _logger.LogInformation("Processing order {OrderId} for {CustomerName}", order.OrderId, order.CustomerName);

            var tableClient = new TableClient(_connectionString, OrdersTableName);
            await tableClient.CreateIfNotExistsAsync();

            try
            {
                var orderEntity = new OrderEntity
                {
                    PartitionKey = order.OrderTimestamp.ToString("yyyy-MM-dd"),
                    RowKey = order.OrderId,
                    CustomerName = order.CustomerName,
                    SelectedItemSKUs = string.Join(",", order.SelectedItemSKUs),
                    TotalPrice = order.TotalPrice,
                    Status = "Received",
                    OrderTimestamp = order.OrderTimestamp
                };

                await tableClient.AddEntityAsync(orderEntity);
                _logger.LogInformation("Order {OrderId} status: Received", order.OrderId);

                await Task.Delay(2000);
                orderEntity.Status = "Preparing";
                await tableClient.UpdateEntityAsync(orderEntity, orderEntity.ETag, TableUpdateMode.Replace);
                _logger.LogInformation("Order {OrderId} status: Preparing", order.OrderId);

                await Task.Delay(3000);
                orderEntity.Status = "Ready";
                await tableClient.UpdateEntityAsync(orderEntity, orderEntity.ETag, TableUpdateMode.Replace);
                _logger.LogInformation("Order {OrderId} status: Ready", order.OrderId);

                await Task.Delay(2000);
                orderEntity.Status = "Collected";
                await tableClient.UpdateEntityAsync(orderEntity, orderEntity.ETag, TableUpdateMode.Replace);
                _logger.LogInformation("Order {OrderId} status: Collected", order.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order {OrderId}, moving to poison queue", order.OrderId);
                var poisonClient = new QueueClient(_connectionString, PoisonQueueName);
                await poisonClient.CreateIfNotExistsAsync();
                await poisonClient.SendMessageAsync(queueMessage);
            }
        }

        [Function("GetAllOrders")]
        public async Task<HttpResponseData> GetAllOrders(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req)
        {
            var tableClient = new TableClient(_connectionString, OrdersTableName);
            await tableClient.CreateIfNotExistsAsync();

            var orders = new List<OrderEntity>();
            await foreach (var entity in tableClient.QueryAsync<OrderEntity>())
            {
                orders.Add(entity);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(orders);
            return response;
        }

        [Function("GetOrdersByDate")]
        public async Task<HttpResponseData> GetOrdersByDate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{date}")] HttpRequestData req,
            string date)
        {
            var tableClient = new TableClient(_connectionString, OrdersTableName);
            await tableClient.CreateIfNotExistsAsync();

            var orders = new List<OrderEntity>();
            string filter = $"PartitionKey eq '{date}'";

            await foreach (var entity in tableClient.QueryAsync<OrderEntity>(filter: filter))
            {
                orders.Add(entity);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(orders);
            return response;
        }
    }
}
