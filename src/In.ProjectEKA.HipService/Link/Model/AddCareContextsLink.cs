namespace In.ProjectEKA.HipService.Link.Model
{
    public class AddCareContextsLink
    {
        public string accessToken { get; }
        public AddCareContextsPatient patient { get; }

        public AddCareContextsLink(string AccessToken, AddCareContextsPatient addCareContextsPatient)
        {
            accessToken = AccessToken;
            patient = addCareContextsPatient;
        }
    }   
}