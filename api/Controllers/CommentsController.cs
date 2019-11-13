namespace AwsDotnetCsharp.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Amazon.DynamoDBv2;
    using Amazon.DynamoDBv2.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using AwsDotnetCsharp.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    [Route("api/[controller]")]
    public class CommentsController : Controller
    {
        private readonly IAmazonSQS sqs;
        private readonly ILogger<CommentsController> logger;
        private readonly IAmazonDynamoDB dynamoDB;

        public CommentsController(IAmazonSQS sqs, IAmazonDynamoDB dynamoDB, ILogger<CommentsController> logger)
        {
            this.sqs = sqs;
            this.logger = logger;
            this.dynamoDB = dynamoDB;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var item = new List<string>{
                "id",
                "comment",
                "language"
            };

            var scanResponse = await dynamoDB.ScanAsync(Environment.GetEnvironmentVariable("DBTableName"), item);

            var comments = scanResponse.Items.Select((x) => new CommentsGetResponse
            {
                Id = x["id"].S,
                Comment = x["comment"].S,
                Language = x.TryGetValue("language", out var language) ? language.S : ""
            });

            return Ok(comments);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody]CommentsPostRequest request)
        {
            var id = Guid.NewGuid().ToString();

            var item = new Dictionary<string, AttributeValue>{
                { "id", new AttributeValue(id)},
                { "comment", new AttributeValue(request.Comment) }
            };

            var putItemResponse = await dynamoDB.PutItemAsync(Environment.GetEnvironmentVariable("DBTableName"), item);

            if (putItemResponse.HttpStatusCode != System.Net.HttpStatusCode.OK)
                return BadRequest();

            var sqsMessageRequest = new SendMessageRequest
            {
                QueueUrl = Environment.GetEnvironmentVariable("QueueUrl"),
                MessageBody = JsonConvert.SerializeObject(new CommentsQueueRequest
                {
                    Id = id,
                    Comment = request.Comment
                })
            };

            var sqsMessageResponse = await sqs.SendMessageAsync(sqsMessageRequest);

            return Ok();
        }
    }
}