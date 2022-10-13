using System;
using Newtonsoft.Json;

namespace In.ProjectEKA.HipService.Link.Model
{
    public class GatewayAddContextsRequestRepresentation
    {
        public Guid requestId { get; }
        public DateTime timestamp { get; }
        public AddCareContextsLink link { get; }

        public GatewayAddContextsRequestRepresentation(Guid RequestId, DateTime Timestamp, AddCareContextsLink Link)
        {
            requestId = RequestId;
            timestamp = Timestamp;
            link = Link;
        }

        public string dump(Object o)
        {
            return JsonConvert.SerializeObject(o);
        }
    }
}