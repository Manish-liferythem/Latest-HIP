using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Link.Model;
using In.ProjectEKA.HipService.UserAuth.Model;

namespace In.ProjectEKA.HipService.Link
{
    public interface ICareContextService
    {
        public Tuple<GatewayAddContextsRequestRepresentation, ErrorRepresentation> AddContextsResponse(
            AddContextsRequest addContextsRequest);

        public Tuple<GatewayNotificationContextRepresentation, ErrorRepresentation> NotificationContextResponse(
            NotifyContextRequest notifyContextRequest);

        public Task CallNotifyContext(NewContextRequest newContextRequest,
            CareContextRepresentation context);

        public Task CallAddContext(NewContextRequest newContextRequest);
        public bool IsLinkedContext(List<string> careContexts, string context);
    }
}