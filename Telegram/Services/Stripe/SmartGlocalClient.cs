//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.Data.Json;
using JsonValue = Windows.Data.Json.JsonValue;

namespace Telegram.Services.Stripe
{
    public partial class SmartGlocalClient : IDisposable
    {
        private readonly string _publicToken;
        private HttpClient _client;

        public SmartGlocalClient(string publicToken)
        {
            _publicToken = publicToken;
            _client = new HttpClient();
        }

        public async Task<string> CreateTokenAsync(Card card, bool test)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            if (_client != null)
            {
                try
                {
                    var parameters = new JsonObject
                    {
                        {
                            "card", new JsonObject
                            {
                                { "number", JsonValue.CreateStringValue(card.Number) },
                                { "expiration_month", JsonValue.CreateStringValue(card.ExpiryMonth.ToString("D2")) },
                                { "expiration_year", JsonValue.CreateStringValue(card.ExpiryYear.ToString("D2")) },
                                { "security_code", JsonValue.CreateStringValue(card.CVC) }
                            }
                        }
                    };

                    var body = parameters.ToString();
                    var url = test ? "https://tgb-playground.smart-glocal.com/cds/v1/tokenize/card" : "https://tgb.smart-glocal.com/cds/v1/tokenize/card";

                    var request = new HttpRequestMessage(HttpMethod.Post, url);
                    var requestContent = new StringContent(body, Encoding.UTF8, "application/json");

                    request.Headers.TryAddWithoutValidation("X-PUBLIC-TOKEN", _publicToken);
                    request.Content = requestContent;

                    var response = await _client.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonObject.Parse(content);

                    var resultData = json.GetNamedObject("data", null);
                    if (resultData == null)
                    {
                        Client.Execute(new AddLogMessage(5, $"Failed to process payment using Smart Glocal: {content}"));
                        return null;
                    }

                    var token = resultData.GetNamedString("token", string.Empty);
                    if (token == null)
                    {
                        Client.Execute(new AddLogMessage(5, $"Failed to process payment using Smart Glocal: {content}"));
                        return null;
                    }

                    return token;
                }
                catch (Exception ex)
                {
                    Client.Execute(new AddLogMessage(5, $"Failed to process payment using Smart Glocal: {ex}"));
                    return null;
                }
            }

            return null;
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
