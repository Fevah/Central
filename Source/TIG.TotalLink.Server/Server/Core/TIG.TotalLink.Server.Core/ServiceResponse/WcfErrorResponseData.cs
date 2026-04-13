using System.Collections.Generic;
using System.Net;

namespace TIG.TotalLink.Server.Core.ServiceResponse
{
    internal class WcfErrorResponseData
    {
        public WcfErrorResponseData(HttpStatusCode status, string body = null, params KeyValuePair<string, string>[] headers)
        {
            StatusCode = status;
            Body = body;
            Headers = headers;
        }


        public HttpStatusCode StatusCode
        {
            private set;
            get;
        }

        public string Body
        {
            private set;
            get;
        }

        public IList<KeyValuePair<string, string>> Headers
        {
            private set;
            get;
        }
    }
}