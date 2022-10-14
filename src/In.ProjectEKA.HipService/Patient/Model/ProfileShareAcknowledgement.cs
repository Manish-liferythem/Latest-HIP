namespace In.ProjectEKA.HipService.Patient.Model
{
    public class ProfileShareAcknowledgement
    {
        public ProfileShareAcknowledgement(string status,string healthId, string tokenNumber)
        {
            Status = status;
            HealthId = healthId;
            TokenNumber = tokenNumber;
        }
        public string Status { get; }
        public string HealthId { get; }
        public string TokenNumber { get; }
    }
}