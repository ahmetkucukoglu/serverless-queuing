[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace AwsDotnetCsharp
{
    using Amazon.Lambda.Core;
    using Amazon.Lambda.SQSEvents;
    using System;
    using Amazon.Comprehend;
    using Amazon.Comprehend.Model;
    using System.Net;
    using System.Linq;
    using Newtonsoft.Json;
    using System.Threading.Tasks;
    using Amazon.DynamoDBv2;
    using Amazon.DynamoDBv2.DocumentModel;
    using AwsDotnetCsharp.Models;

    public class ConsumerHandler
    {
        public async Task ReceiveQueue(SQSEvent input, ILambdaContext context)
        {
            var messageBody = JsonConvert.DeserializeObject<CommentsQueueRequest>(input.Records[0].Body);

            //DETECT LANGUAGE

            var comprehendClient = new AmazonComprehendClient(Amazon.RegionEndpoint.EUCentral1);

            var detectDominantLanguageRequest = new DetectDominantLanguageRequest
            {
                Text = messageBody.Comment
            };

            var detectDominantLanguageResponse = await comprehendClient.DetectDominantLanguageAsync(detectDominantLanguageRequest);

            if (detectDominantLanguageResponse.HttpStatusCode != HttpStatusCode.OK)
                throw new Exception("NOT FOUND");            

            var languageResponse = detectDominantLanguageResponse.Languages.OrderByDescending((x) => x.Score).FirstOrDefault();

            //UPDATE DOCUMENT

            var dynamoDBClient = new AmazonDynamoDBClient();
            var commentsCatalog = Table.LoadTable(dynamoDBClient, Environment.GetEnvironmentVariable("DBTableName"));

            var commentDocument = new Document();
            commentDocument["id"] = messageBody.Id;
            commentDocument["language"] = languageResponse.LanguageCode;

            var updatedComment = await commentsCatalog.UpdateItemAsync(commentDocument);
        }
    }
}
