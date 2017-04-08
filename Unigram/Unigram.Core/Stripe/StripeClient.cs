using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace Unigram.Core.Stripe
{
    public class StripeClient : IDisposable
    {
        private readonly string _publishableKey;
        private HttpClient _client;

        public StripeClient(string publishableKey)
        {
            _publishableKey = publishableKey;
            _client = new HttpClient();
        }

        public async Task<StripeToken> CreateTokenAsync(Card card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            if (_client != null)
            {
                try
                {
                    var parameters = StripeNetworkUtils.HashMapFromCard(card);

                    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/tokens");
                    var requestContent = new FormUrlEncodedContent(parameters);

                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", GetAuthorizationHeaderValue(_publishableKey));
                    request.Content = requestContent;

                    var response = await _client.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonObject.Parse(content);

                    var token = new StripeToken();
                    token.Id = json.GetNamedString("id", string.Empty);
                    token.Type = json.GetNamedString("type", string.Empty);
                    token.Content = content;

                    return token;
                }
                catch
                {

                }
            }

            return null;
        }

        private string GetAuthorizationHeaderValue(string apiKey)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:"));
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }
    }
}
