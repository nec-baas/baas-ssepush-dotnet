using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;

namespace Nec.Nebula.Test
{
    class MockRestResponse : HttpResponseMessage
    {
        public string ContentType { get; set; }
        public long ContentLength { get; set; }
        public string ContentEncoding { get; set; }
        public string StatusDescription { get; set; }
        public byte[] RawBytes { get; set; }
        public Uri ResponseUri { get; set; }
        public string Server { get; set; }
        public string ErrorMessage { get; set; }
        public Exception ErrorException { get; set; }

        public MockRestResponse(HttpStatusCode statusCode = HttpStatusCode.OK, string content = "{}")
        {
            StatusCode = statusCode;

            Content = new StringContent(content);

        }

        public MockRestResponse(HttpStatusCode statusCode, Byte[] content, string contentType)
        {

            StatusCode = statusCode;
            if (content != null)
            {
                Content = new ByteArrayContent(content);
                Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }
        }
    }
}
