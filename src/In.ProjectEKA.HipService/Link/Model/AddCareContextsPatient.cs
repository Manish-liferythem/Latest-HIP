using System.Collections.Generic;
using In.ProjectEKA.HipLibrary.Patient.Model;

namespace In.ProjectEKA.HipService.Link.Model
{
    public class AddCareContextsPatient
    {
        public string referenceNumber { get; }
        public string display { get; }
        public List<CareContextRepresentation> careContexts { get; }

        public AddCareContextsPatient(string ReferenceNumber, string Display, List<CareContextRepresentation> CareContexts)
        {
            referenceNumber = ReferenceNumber;
            display = Display;
            careContexts = CareContexts;
        }
    }
}