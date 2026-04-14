using System.Net;

namespace Object_Detection_ASP.NETMVC.Services
{
    public class ObjectDetectionApiException : Exception
    {
        public ObjectDetectionApiException(
            string message,
            HttpStatusCode? statusCode = null,
            string? responseContent = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
        }

        public HttpStatusCode? StatusCode { get; }

        public string? ResponseContent { get; }
    }
}
