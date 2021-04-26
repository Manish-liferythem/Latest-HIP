using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.UserAuth.Model;
using Microsoft.Extensions.Logging;
using Optional;
using static In.ProjectEKA.HipService.Common.Constants;

namespace In.ProjectEKA.HipService.UserAuth
{
    public class UserAuthService : IUserAuthService
    {
        private readonly IUserAuthRepository userAuthRepository;
        private readonly ILogger<UserAuthController> logger;

        public UserAuthService(IUserAuthRepository userAuthRepository, ILogger<UserAuthController> logger)
        {
            this.userAuthRepository = userAuthRepository;
            this.logger = logger;
        }

        public Tuple<GatewayFetchModesRequestRepresentation, ErrorRepresentation> FetchModeResponse(
            FetchRequest fetchRequest, BahmniConfiguration bahmniConfiguration)
        {
            var healthId = fetchRequest.healthId;
            if (!IsValidHealthId(healthId))
                return new Tuple<GatewayFetchModesRequestRepresentation, ErrorRepresentation>
                    (null, new ErrorRepresentation(new Error(ErrorCode.InvalidHealthId, "HealthId is invalid")));
            var patientIdSplit = healthId.Split("@");
            var cmSuffix = patientIdSplit[1];
            var requester = new Requester(bahmniConfiguration.Id, HIP);
            var purpose = fetchRequest.purpose;
            var query = purpose != null
                ? new FetchQuery(healthId, purpose, requester)
                : new FetchQuery(healthId, requester);
            var timeStamp = DateTime.Now.ToUniversalTime();
            var requestId = Guid.NewGuid();
            return new Tuple<GatewayFetchModesRequestRepresentation, ErrorRepresentation>
                (new GatewayFetchModesRequestRepresentation(requestId, timeStamp, query, cmSuffix), null);
        }

        public Tuple<GatewayAuthInitRequestRepresentation, ErrorRepresentation> AuthInitResponse(
            AuthInitRequest authInitRequest, BahmniConfiguration bahmniConfiguration)
        {
            var healthId = authInitRequest.healthId;
            if (!IsValidHealthId(healthId))
                return new Tuple<GatewayAuthInitRequestRepresentation, ErrorRepresentation>
                    (null, new ErrorRepresentation(new Error(ErrorCode.InvalidHealthId, "HealthId is invalid")));
            var patientIdSplit = healthId.Split("@");
            var cmSuffix = patientIdSplit[1];
            var timeStamp = DateTime.Now.ToUniversalTime();
            var requestId = Guid.NewGuid();
            var requester = new Requester(bahmniConfiguration.Id, HIP);
            var purpose = authInitRequest.purpose;
            var authInitQuery = purpose != null
                ? new AuthInitQuery(healthId, purpose, authInitRequest.authMode, requester)
                : new AuthInitQuery(healthId, authInitRequest.authMode, requester);
            return new Tuple<GatewayAuthInitRequestRepresentation, ErrorRepresentation>
                (new GatewayAuthInitRequestRepresentation(requestId, timeStamp, authInitQuery, cmSuffix), null);
        }

        public Tuple<GatewayAuthConfirmRequestRepresentation, ErrorRepresentation> AuthConfirmResponse(
            AuthConfirmRequest authConfirmRequest)
        {
            var healthId = authConfirmRequest.healthId;
            if (!(IsValidHealthId(healthId) && IsPresentInMap(healthId)))
                return new Tuple<GatewayAuthConfirmRequestRepresentation, ErrorRepresentation>
                    (null, new ErrorRepresentation(new Error(ErrorCode.InvalidHealthId, "HealthId is invalid")));
            var credential = new AuthConfirmCredential(GetDecodedOtp(authConfirmRequest.authCode));
            var transactionId = UserAuthMap.HealthIdToTransactionId[healthId];
            var timeStamp = DateTime.Now.ToUniversalTime();
            var requestId = Guid.NewGuid();
            return new Tuple<GatewayAuthConfirmRequestRepresentation, ErrorRepresentation>
            (new GatewayAuthConfirmRequestRepresentation(requestId, timeStamp, transactionId, credential),
                null);
        }

        public string GetCmSuffix(string healthId)
        {
            if (IsValidHealthId(healthId))
            {
                var patientIdSplit = healthId.Split("@");
                var cmSuffix = patientIdSplit[1];
                return cmSuffix;
            }
            return "";
        }
        
        private static string GetDecodedOtp(String authCode)
        {
            var decodedOtp = Convert.FromBase64String(authCode);
            var otp = Encoding.UTF8.GetString(decodedOtp);
            return otp;
        }

        private static bool IsValidHealthId(string healthId)
        {
            string pattern = @"\w+\S\w+@\w+";
            return Regex.Match(healthId, pattern).Success;
        }

        private static bool IsPresentInMap(string healthId)
        {
            return UserAuthMap.HealthIdToTransactionId.ContainsKey(healthId);
        }

        public async Task<Tuple<AuthConfirm, ErrorRepresentation>> OnAuthConfirmResponse(
            OnAuthConfirmRequest onAuthConfirmRequest)
        {
            var accessToken = onAuthConfirmRequest.auth.accessToken;
            var healthId = onAuthConfirmRequest.auth.patient.id;
            var authConfirm = new AuthConfirm(healthId, accessToken);
            var savedAuthConfirm = userAuthRepository.Get(healthId).Result;
            if (savedAuthConfirm.Equals(Option.Some<AuthConfirm>(null)))
            {
                var authConfirmResponse = await userAuthRepository.Add(authConfirm).ConfigureAwait(false);
                if (!authConfirmResponse.HasValue)
                {
                    return new Tuple<AuthConfirm, ErrorRepresentation>(null,
                        new ErrorRepresentation(new Error(ErrorCode.DuplicateAuthConfirmRequest,
                            "Auth confirm request already exists")));
                }
            }
            else
            {
                userAuthRepository.Update(authConfirm);
            }

            UserAuthMap.HealthIdToTransactionId.Remove(healthId);
            var requestId = Guid.Parse(onAuthConfirmRequest.resp.RequestId);
            UserAuthMap.RequestIdToAccessToken.Add(requestId, accessToken);
            UserAuthMap.RequestIdToPatientDetails.Add(requestId, onAuthConfirmRequest.auth.patient);
            return new Tuple<AuthConfirm, ErrorRepresentation>(authConfirm, null);
        }
    }
}