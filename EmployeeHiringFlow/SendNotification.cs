using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EmployeeHiringFlow
{
    public static class SendNotification
    {
        [FunctionName("SendNotification")]
        [return: SendGrid(ApiKey = "SendGridAttributeApiKey", From = "SenderEmail@org.com")]
        public static SendGridMessage Run(
            [QueueTrigger("email-notifications", Connection = "AzureWebJobsStorage")]
            Notification  notification, 
            ILogger log)
        {
            log.LogWarning($"Sending notification {JsonConvert.SerializeObject(notification)}");

            SendGridMessage message = new SendGridMessage()
            {
                Subject = $"{notification.Subject}"
            };
            message.AddTo(notification.To);
            message.AddContent("text/plain", $"{notification.Message}");
            return message;
        }
    }
    public class Notification
    {
        public string Subject { get; set; }
        public string Message { get; set; }
        public string To { get; set; }
    }
}
