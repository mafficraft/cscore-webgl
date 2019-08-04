using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using com.csutil.http;

namespace com.csutil.http {

    internal class UriRestRequest : RestRequest, IDisposable {

        private Uri uri;
        public Action<UriRestRequest, HttpResponseMessage> handleResult;
        public IJsonReader jsonReader = JsonReader.GetReader();
        private Task sendTask;
        private Headers requestHeaders;

        public UriRestRequest(Uri uri) { this.uri = uri; }

        public async Task<T> GetResult<T>(Action<T> successCallback) {
            Task<string> readResultTask = null;
            T result = default(T); // Init in case the request fails
            handleResult = (self, resp) => {
                using (readResultTask = resp.Content.ReadAsStringAsync()) {
                    result = ParseResultStringInto<T>(readResultTask.Result);
                    successCallback.InvokeIfNotNull(result);
                }
            };
            await sendTask;
            if (readResultTask != null) { readResultTask.Wait(); }
            if (sendTask.Status != TaskStatus.RanToCompletion) {
                Log.e("Web-request failed, returned result will be null");
            }
            return result;
        }

        private T ParseResultStringInto<T>(string result) { return jsonReader.Read<T>(result); }

        public RestRequest Send(HttpMethod method) {
            sendTask = Task.Run(async () => {
                await Task.Delay(5); // wait 5ms so that the created RestRequest can be modified before its sent
                using (var c = new HttpClient()) {
                    AddRequestHeaders(c, requestHeaders);
                    var asyncRestRequest = await c.SendAsync(new HttpRequestMessage(method, uri));
                    handleResult.InvokeIfNotNull(this, asyncRestRequest);
                }
            });
            return this;
        }

        public void Dispose() { sendTask.Dispose(); }

        private static bool AddRequestHeaders(HttpClient self, Headers requestHeadersToAdd) {
            if (requestHeadersToAdd.IsNullOrEmpty()) { return false; }
            bool r = true;
            foreach (var h in requestHeadersToAdd) {
                if (!self.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value)) {
                    Log.e("Could not add header to request: " + h);
                    r = false;
                }
            }
            return r;
        }

        public RestRequest WithRequestHeaders(Headers requestHeaders) { this.requestHeaders = requestHeaders; return this; }

    }

}