﻿/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    /// <summary>
    /// Base class for all exchange API
    /// </summary>
    public abstract class ExchangeAPI : IExchangeAPI
    {
        /// <summary>
        /// Bitfinex
        /// </summary>
        public const string ExchangeNameBitfinex = "Bitfinex";

        /// <summary>
        /// Gemini
        /// </summary>
        public const string ExchangeNameGemini = "Gemini";

        /// <summary>
        /// GDAX
        /// </summary>
        public const string ExchangeNameGDAX = "GDAX";

        /// <summary>
        /// Kraken
        /// </summary>
        public const string ExchangeNameKraken = "Kraken";

        /// <summary>
        /// Bittrex
        /// </summary>
        public const string ExchangeNameBittrex = "Bittrex";

        /// <summary>
        /// Base URL for the exchange API
        /// </summary>
        public abstract string BaseUrl { get; set; }

        /// <summary>
        /// Public API key - only needs to be set if you are using private authenticated end points
        /// </summary>
        public string PublicApiKey { get; set; }

        /// <summary>
        /// Private API key - only needs to be set if you are using private authenticated end points
        /// </summary>
        public string PrivateApiKey { get; set; }

        /// <summary>
        /// Rate limiter - set this to a new limit if you are seeing your ip get blocked by the exchange
        /// </summary>
        public JackLeitch.RateGate.RateGate RateLimit { get; set; } = new JackLeitch.RateGate.RateGate(5, TimeSpan.FromSeconds(15.0d));

        /// <summary>
        /// Default request method
        /// </summary>
        public string RequestMethod { get; set; } = "GET";

        /// <summary>
        /// Content type for requests
        /// </summary>
        public string RequestContentType { get; set; } = "text/plain";

        /// <summary>
        /// User agent for requests
        /// </summary>
        public string RequestUserAgent { get; set; } = "ExchangeSharp";

        /// <summary>
        /// Cache policy - defaults to no cache, don't change unless you have specific needs
        /// </summary>
        public System.Net.Cache.RequestCachePolicy CachePolicy { get; set; } = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

        /// <summary>
        /// Process a request url
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="payload">Payload</param>
        /// <returns>Updated url</returns>
        protected virtual Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload)
        {
            return url.Uri;
        }

        /// <summary>
        /// Additional handling for request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="payload">Payload</param>
        protected virtual void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {

        }

        /// <summary>
        /// Additional handling for response
        /// </summary>
        /// <param name="response">Response</param>
        protected virtual void ProcessResponse(HttpWebResponse response)
        {

        }

        protected void AppendFormToRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            if (payload != null && payload.Count != 0)
            {
                StringBuilder form = new StringBuilder();
                foreach (KeyValuePair<string, object> keyValue in payload)
                {
                    form.AppendFormat("{0}={1}&", keyValue.Key, keyValue.Value);
                }
                form.Length--; // trim ampersand
                using (StreamWriter writer = new StreamWriter(request.GetRequestStream(), Encoding.ASCII))
                {
                    writer.Write(form);
                }
            }
        }

        /// <summary>
        /// Make a request to a path on the API
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null and should at least be an empty dictionary for private API end points</param>
        /// <returns>Raw response in JSON</returns>
        public string MakeRequest(string url, string baseUrl = null, Dictionary<string, object> payload = null)
        {
            RateLimit.WaitToProceed();
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }
            else if (url[0] != '/')
            {
                url = "/" + url;
            }

            string fullUrl = (baseUrl ?? BaseUrl) + url;
            Uri uri = ProcessRequestUrl(new UriBuilder(fullUrl), payload);
            HttpWebRequest request = HttpWebRequest.CreateHttp(uri);
            request.Method = RequestMethod;
            request.ContentType = RequestContentType;
            request.UserAgent = RequestUserAgent;
            request.CachePolicy = CachePolicy;
            ProcessRequest(request, payload);
            HttpWebResponse response;
            try
            {
                response = request.GetResponse() as HttpWebResponse;
            }
            catch (WebException we)
            {
                response = we.Response as HttpWebResponse;
            }
            string responseString = (response == null ? null : new StreamReader(response.GetResponseStream()).ReadToEnd());
            ProcessResponse(response);
            return responseString;
        }

        /// <summary>
        /// Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Url</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null and should at least be an empty dictionary for private API end points</param>
        /// <returns></returns>
        public T MakeJsonRequest<T>(string url, string baseUrl = null, Dictionary<string, object> payload = null)
        {
            string response = MakeRequest(url, baseUrl, payload);
            return JsonConvert.DeserializeObject<T>(response);
        }

        /// <summary>
        /// Get an exchange API given an exchange name (see public constants at top of this file)
        /// </summary>
        /// <param name="exchangeName">Exchange name</param>
        /// <returns>Exchange API or null if not found</returns>
        public static IExchangeAPI GetExchangeAPI(string exchangeName)
        {
            GetExchangeAPIDictionary().TryGetValue(exchangeName, out IExchangeAPI api);
            return api;
        }

        /// <summary>
        /// Get a dictionary of exchange APIs for all exchanges
        /// </summary>
        /// <returns>Dictionary of string exchange name and value exchange api</returns>
        public static Dictionary<string, IExchangeAPI> GetExchangeAPIDictionary()
        {
            return new Dictionary<string, IExchangeAPI>
            {
                { ExchangeNameGemini, new ExchangeGeminiAPI() },
                { ExchangeNameBitfinex, new ExchangeBitfinexAPI() },
                { ExchangeNameGDAX, new ExchangeGdaxAPI() },
                { ExchangeNameKraken, new ExchangeKrakenAPI() },
                { ExchangeNameBittrex, new ExchangeBittrexAPI() }
            };
        }

        /// <summary>
        /// Get exchange symbols
        /// </summary>
        /// <returns>Array of symbols</returns>
        public virtual string[] GetSymbols() { throw new NotImplementedException(); }

        /// <summary>
        /// Get exchange ticker
        /// </summary>
        /// <param name="symbol">Symbol to get ticker for</param>
        /// <returns>Ticker or null if failure</returns>
        public virtual ExchangeTicker GetTicker(string symbol) { throw new NotImplementedException(); }

        /// <summary>
        /// Get exchange order book
        /// </summary>
        /// <param name="symbol">Symbol to get order book for</param>
        /// <param name="maxCount">Max count, not all exchanges will honor this parameter</param>
        /// <returns>Exchange order book or null if failure</returns>
        public virtual ExchangeOrderBook GetOrderBook(string symbol, int maxCount = 100) { throw new NotImplementedException(); }

        /// <summary>
        /// Get historical trades for the exchange
        /// </summary>
        /// <param name="symbol">Symbol to get historical data for</param>
        /// <param name="sinceDateTime">Optional date time to start getting the historical data at, null for the most recent data</param>
        /// <returns>An enumerator that iterates all historical data, this can take quite a while depending on how far back the sinceDateTime parameter goes</returns>
        public virtual IEnumerable<ExchangeTrade> GetHistoricalTrades(string symbol, DateTime? sinceDateTime = null) { throw new NotImplementedException(); }

        /// <summary>
        /// Get recent trades on the exchange
        /// </summary>
        /// <param name="symbol">Symbol to get recent trades for</param>
        /// <returns>An enumerator that loops through all trades</returns>
        public virtual IEnumerable<ExchangeTrade> GetRecentTrades(string symbol) { return GetHistoricalTrades(symbol, null); }

        /// <summary>
        /// Get amounts available to trade, symbol / amount dictionary
        /// </summary>
        /// <returns>Symbol / amount dictionary</returns>
        public virtual Dictionary<string, double> GetAmountsAvailableToTrade() { throw new NotImplementedException(); }

        /// <summary>
        /// Place an order
        /// </summary>
        /// <param name="symbol">Symbol</param>
        /// <param name="amount">Amount</param>
        /// <param name="price">Price</param>
        /// <param name="buy">True to buy, false to sell</param>
        /// <returns>Result</returns>
        public virtual ExchangeOrderResult PlaceOrder(string symbol, double amount, double price, bool buy) { throw new NotImplementedException(); }

        /// <summary>
        /// Get order details
        /// </summary>
        /// <param name="orderId">Order id to get details for</param>
        /// <returns>Order details</returns>
        public virtual ExchangeOrderResult GetOrderDetails(string orderId) { throw new NotImplementedException(); }

        /// <summary>
        /// Cancel an order
        /// </summary>
        /// <param name="orderId">Order id of the order to cancel</param>
        /// <returns>Null/empty if success, otherwise an error message</returns>
        public virtual string CancelOrder(string orderId) { throw new NotImplementedException(); }

        /// <summary>
        /// Gets the name of the exchange
        /// </summary>
        public virtual string Name { get { return "NullExchange"; } }
    }
}