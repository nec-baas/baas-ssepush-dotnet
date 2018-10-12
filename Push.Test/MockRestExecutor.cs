using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Nec.Nebula.Internal;

namespace Nec.Nebula.Test
{
    class MockRestExecutor : NbRestExecutor
    {
        public NbRestRequest LastRequest { get; set; }

        private readonly Queue<HttpResponseMessage> _responseQueue = new Queue<HttpResponseMessage>();

        protected override Task<HttpResponseMessage> _ExecuteRequest(NbRestRequest request)
        {
            LastRequest = request;

            var task = new TaskCompletionSource<HttpResponseMessage>();
            var response = _responseQueue.Dequeue();
            task.TrySetResult(response);
            return task.Task;
        }

        public void ClearResponses()
        {
            _responseQueue.Clear();
        }

        public void SetResponse(HttpResponseMessage response)
        {
            _responseQueue.Clear();
            _responseQueue.Enqueue(response);
        }

        public void AddResponse(HttpResponseMessage response)
        {
            _responseQueue.Enqueue(response);
        }
    }
}
