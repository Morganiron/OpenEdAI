using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace OpenEdAI.Client.Services
{
    public class ApiAuthorizationMessageHandler : DelegatingHandler
    {
        private readonly IJSRuntime _js;

        public ApiAuthorizationMessageHandler(IJSRuntime js)
        {
            _js = js;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Get the token from localStorage using JS interop
            var token = await _js.InvokeAsync<string>("localStorage.getItem", "access_token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                // Attach the token as a Bearer token in the Authorization header
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
