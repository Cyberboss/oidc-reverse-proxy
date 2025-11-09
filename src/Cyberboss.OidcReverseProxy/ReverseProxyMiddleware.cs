using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Cyberboss.OidcReverseProxy
{
	public class ReverseProxyMiddleware : IDisposable
	{
		private readonly IConfiguration _configuration;

		private readonly HttpClient _httpClient;
		private readonly RequestDelegate _nextMiddleware;

		public ReverseProxyMiddleware(IConfiguration configuration, RequestDelegate nextMiddleware)
		{
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			_nextMiddleware = nextMiddleware ?? throw new ArgumentNullException(nameof(nextMiddleware));
			_httpClient = new HttpClient();
		}

		public void Dispose()
			=> _httpClient.Dispose();

		public async Task Invoke(HttpContext context)
		{
			if (context.User.Identity?.IsAuthenticated != true)
			{
				await this._nextMiddleware(context);
				return;
			}


			var targetUri = new Uri($"{_configuration.GetRequiredSection("TargetUrl").Get<string>()?.TrimEnd('/')}{context.Request.Path}");

			var targetRequestMessage = CreateTargetMessage(context, targetUri);

			using (var responseMessage = await _httpClient.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
			{
				context.Response.StatusCode = (int)responseMessage.StatusCode;

				CopyFromTargetResponseHeaders(context, responseMessage);

				await ProcessResponseContent(context, responseMessage);
			}
		}

		private async Task ProcessResponseContent(HttpContext context, HttpResponseMessage responseMessage)
		{
			var content = await responseMessage.Content.ReadAsByteArrayAsync();

			if (IsContentOfType(responseMessage, "text/html") || IsContentOfType(responseMessage, "text/javascript"))
			{
				var stringContent = Encoding.UTF8.GetString(content);
				var newContent = stringContent.Replace("https://www.google.com", "/google")
					.Replace("https://www.gstatic.com", "/googlestatic")
					.Replace("https://docs.google.com/forms", "/googleforms");
				await context.Response.WriteAsync(newContent, Encoding.UTF8);
			}
			else
			{
				await context.Response.Body.WriteAsync(content);
			}
		}

		private bool IsContentOfType(HttpResponseMessage responseMessage, string type)
		{
			var result = false;

			if (responseMessage.Content?.Headers?.ContentType != null)
			{
				result = responseMessage.Content.Headers.ContentType.MediaType == type;
			}

			return result;
		}

		private HttpRequestMessage CreateTargetMessage(HttpContext context, Uri targetUri)
		{
			var requestMessage = new HttpRequestMessage();
			CopyFromOriginalRequestContentAndHeaders(context, requestMessage);

			requestMessage.RequestUri = targetUri;
			requestMessage.Headers.Host = targetUri.Host;
			requestMessage.Method = new HttpMethod(context.Request.Method);

			return requestMessage;
		}

		private void CopyFromOriginalRequestContentAndHeaders(HttpContext context, HttpRequestMessage requestMessage)
		{
			var requestMethod = context.Request.Method;

			if (!HttpMethods.IsGet(requestMethod) &&
				!HttpMethods.IsHead(requestMethod) &&
				!HttpMethods.IsDelete(requestMethod) &&
				!HttpMethods.IsTrace(requestMethod))
			{
				var streamContent = new StreamContent(context.Request.Body);
				requestMessage.Content = streamContent;
			}

			foreach (var header in context.Request.Headers)
			{
				requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
			}
		}

		private void CopyFromTargetResponseHeaders(HttpContext context, HttpResponseMessage responseMessage)
		{
			foreach (var header in responseMessage.Headers)
			{
				context.Response.Headers[header.Key] = header.Value.ToArray();
			}

			foreach (var header in responseMessage.Content.Headers)
			{
				context.Response.Headers[header.Key] = header.Value.ToArray();
			}

			context.Response.Headers.Remove("transfer-encoding");
		}
	}
}