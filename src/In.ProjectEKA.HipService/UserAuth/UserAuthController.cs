using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Common;
using In.ProjectEKA.HipService.Common.Model;
using In.ProjectEKA.HipService.Gateway;
using In.ProjectEKA.HipService.Link.Model;
using In.ProjectEKA.HipService.OpenMrs;
using In.ProjectEKA.HipService.UserAuth.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Optional.Collections;
using Serilog;
using static In.ProjectEKA.HipService.UserAuth.UserAuthMap;

namespace In.ProjectEKA.HipService.UserAuth
{
    using static Constants;

    [ApiController]
    public class UserAuthController : Controller
    {
        private readonly IGatewayClient gatewayClient;
        private readonly ILogger<UserAuthController> logger;
        private readonly BahmniConfiguration bahmniConfiguration;
        private readonly IUserAuthService userAuthService;
        private readonly GatewayConfiguration gatewayConfiguration;
        private readonly HttpClient httpClient;
        private readonly OpenMrsConfiguration openMrsConfiguration;

        public UserAuthController(IGatewayClient gatewayClient,
            ILogger<UserAuthController> logger,
            IUserAuthService userAuthService,
            BahmniConfiguration bahmniConfiguration,
            GatewayConfiguration gatewayConfiguration,
            HttpClient httpClient,
            OpenMrsConfiguration openMrsConfiguration)
        {
            this.gatewayClient = gatewayClient;
            this.logger = logger;
            this.userAuthService = userAuthService;
            this.bahmniConfiguration = bahmniConfiguration;
            this.gatewayConfiguration = gatewayConfiguration;
            this.httpClient = httpClient;
            this.openMrsConfiguration = openMrsConfiguration;
        }

        [Route(PATH_FETCH_MODES)]
        public async Task<ActionResult> GetAuthModes(
            [FromHeader(Name = CORRELATION_ID)] string correlationId, [FromBody] FetchRequest fetchRequest)
        {
            if (Request != null)
            {
                if (Request.Cookies.ContainsKey(REPORTING_SESSION))
                {
                    string sessionId = Request.Cookies[REPORTING_SESSION];

                    Task<StatusCodeResult> statusCodeResult = IsAuthorised(sessionId);
                    if (!statusCodeResult.Result.StatusCode.Equals(StatusCodes.Status200OK))
                    {
                        return statusCodeResult.Result;
                    }
                }
                else
                {
                    return StatusCode(StatusCodes.Status401Unauthorized);
                }
            }


            var (gatewayFetchModesRequestRepresentation, error) =
                userAuthService.FetchModeResponse(fetchRequest, bahmniConfiguration);
            if (error != null)
                return StatusCode(StatusCodes.Status400BadRequest, error);
            Guid requestId = gatewayFetchModesRequestRepresentation.requestId;
            var cmSuffix = gatewayConfiguration.CmSuffix;

            try
            {
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth,
                    "Request for fetch-modes to gateway: {@GatewayResponse}",
                    gatewayFetchModesRequestRepresentation.dump(gatewayFetchModesRequestRepresentation));
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth, $"cmSuffix: {{cmSuffix}}, correlationId: {{correlationId}}," +
                                        $" healthId: {{healthId}}, requestId: {{requestId}}",
                    cmSuffix, correlationId, gatewayFetchModesRequestRepresentation.query.id, requestId);
                await gatewayClient.SendDataToGateway(PATH_FETCH_AUTH_MODES, gatewayFetchModesRequestRepresentation,
                    cmSuffix, correlationId);

                var i = 0;
                do
                {
                    Thread.Sleep(gatewayConfiguration.TimeOut);
                    if (RequestIdToErrorMessage.ContainsKey(requestId))
                    {
                        var gatewayError = RequestIdToErrorMessage[requestId];
                        RequestIdToErrorMessage.Remove(requestId);
                        return StatusCode(StatusCodes.Status400BadRequest,
                            new ErrorRepresentation(gatewayError));
                    }

                    if (RequestIdToAuthModes.ContainsKey(requestId))
                    {
                        logger.LogInformation(LogEvents.UserAuth,
                            "Response about to be send for requestId: {RequestId} with authModes: {AuthModes}",
                            requestId, RequestIdToAuthModes[requestId]
                        );
                        List<Mode> authModes = RequestIdToAuthModes[requestId];
                        FetchModeResponse fetchModeResponse = new FetchModeResponse(authModes);
                        return Json(fetchModeResponse);
                    }

                    i++;
                } while (i < gatewayConfiguration.Counter);
            }
            catch (Exception exception)
            {
                logger.LogError(LogEvents.UserAuth, exception, "Error happened for requestId: {RequestId} for" +
                                                               " fetch-mode request", requestId);
            }

            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new ErrorRepresentation(new Error(ErrorCode.GatewayTimedOut, "Gateway timed out")));
        }

        [Authorize]
        [HttpPost(PATH_ON_FETCH_AUTH_MODES)]
        public AcceptedResult SetAuthModes(OnFetchAuthModeRequest request)
        {
            logger.Log(LogLevel.Information,
                LogEvents.UserAuth, "On fetch mode request received." +
                                    $" RequestId:{request.RequestId}, " +
                                    $" Timestamp:{request.Timestamp}," +
                                    $" ResponseRequestId:{request.Resp.RequestId}, ");
            if (request.Error != null)
            {
                RequestIdToErrorMessage.Add(Guid.Parse(request.Resp.RequestId), request.Error);
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth, $" Error Code:{request.Error.Code}," +
                                        $" Error Message:{request.Error.Message}.");
            }
            else if (request.Auth != null)
            {
                RequestIdToAuthModes.Add(Guid.Parse(request.Resp.RequestId), request.Auth.Modes);
            }

            return Accepted();
        }

        [Route(PATH_HIP_AUTH_INIT)]
        public async Task<ActionResult> GetTransactionId(
            [FromHeader(Name = CORRELATION_ID)] string correlationId, [FromBody] AuthInitRequest authInitRequest)
        {
            if (Request != null)
            {
                Log.Information("cookies count" + Request.Cookies.Count);
                foreach (String a in Request.Cookies.Keys)
                {
                    Log.Information(a + "cookie" + Request.Cookies[a] + " " + Request.Cookies[a].Length);
                }
                if (Request.Cookies.ContainsKey(REPORTING_SESSION))
                {
                    string sessionId = Request.Cookies[REPORTING_SESSION];
            
                    Task<StatusCodeResult> statusCodeResult = IsAuthorised(sessionId);
                    if (!statusCodeResult.Result.StatusCode.Equals(StatusCodes.Status200OK))
                    {
                        return statusCodeResult.Result;
                    }
                }
                else
                {
                    return StatusCode(StatusCodes.Status401Unauthorized);
                }
            }

            var (gatewayAuthInitRequestRepresentation, error) =
                userAuthService.AuthInitResponse(authInitRequest, bahmniConfiguration);
            if (error != null)
                return StatusCode(StatusCodes.Status400BadRequest, error);
            Guid requestId = gatewayAuthInitRequestRepresentation.requestId;
            var cmSuffix = gatewayConfiguration.CmSuffix;

            try
            {
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth,
                    "Request for auth-init to gateway: {@GatewayResponse}",
                    gatewayAuthInitRequestRepresentation.dump(gatewayAuthInitRequestRepresentation));
                logger.Log(LogLevel.Information, LogEvents.UserAuth, $"cmSuffix: {{cmSuffix}}," +
                                                                     $" correlationId: {{correlationId}}, " +
                                                                     $"healthId: {{healthId}}, requestId: {{requestId}}",
                    cmSuffix, correlationId, gatewayAuthInitRequestRepresentation.query.id, requestId);
                await gatewayClient.SendDataToGateway(PATH_AUTH_INIT, gatewayAuthInitRequestRepresentation, cmSuffix,
                    correlationId);
                var i = 0;
                do
                {
                    Thread.Sleep(gatewayConfiguration.TimeOut);
                    if (RequestIdToErrorMessage.ContainsKey(requestId))
                    {
                        var gatewayError = RequestIdToErrorMessage[requestId];
                        RequestIdToErrorMessage.Remove(requestId);
                        return StatusCode(StatusCodes.Status400BadRequest,
                            new ErrorRepresentation(gatewayError));
                    }

                    if (RequestIdToTransactionIdMap.ContainsKey(requestId))
                    {
                        logger.LogInformation(LogEvents.UserAuth,
                            "Response about to be send for requestId: {RequestId} with transactionId: {TransactionId}",
                            requestId, RequestIdToTransactionIdMap[requestId]
                        );
                        if (HealthIdToTransactionId.ContainsKey(authInitRequest.healthId))
                        {
                            HealthIdToTransactionId[authInitRequest.healthId] = RequestIdToTransactionIdMap[requestId];
                        }
                        else
                        {
                            HealthIdToTransactionId.Add(authInitRequest.healthId,
                                RequestIdToTransactionIdMap[requestId]);
                        }

                        return Accepted();
                    }

                    i++;
                } while (i < gatewayConfiguration.Counter);
            }
            catch (Exception exception)
            {
                logger.LogError(LogEvents.UserAuth, exception, "Error happened for requestId: {RequestId} for" +
                                                               " auth-init request", requestId);
            }

            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new ErrorRepresentation(new Error(ErrorCode.GatewayTimedOut, "Gateway timed out")));
        }

        [Authorize]
        [HttpPost(PATH_ON_AUTH_INIT)]
        public AcceptedResult SetTransactionId(AuthOnInitRequest request)
        {
            logger.Log(LogLevel.Information,
                LogEvents.UserAuth, "Auth on init request received." +
                                    $" RequestId:{request.RequestId}, " +
                                    $" Timestamp:{request.Timestamp},");
            if (request.Error != null)
            {
                RequestIdToErrorMessage.Add(Guid.Parse(request.Resp.RequestId), request.Error);
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth, $" Error Code:{request.Error.Code}," +
                                        $" Error Message:{request.Error.Message}.");
            }
            else if (request.Auth != null)
            {
                string transactionId = request.Auth.TransactionId;
                RequestIdToTransactionIdMap.Add(Guid.Parse(request.Resp.RequestId), transactionId);
            }

            logger.Log(LogLevel.Information,
                LogEvents.UserAuth, $"Response RequestId:{request.Resp.RequestId}");
            return Accepted();
        }

        [Route(PATH_HIP_AUTH_CONFIRM)]
        public async Task<ActionResult> GetAccessToken(
            [FromHeader(Name = CORRELATION_ID)] string correlationId, [FromBody] AuthConfirmRequest authConfirmRequest)
        {
            if (Request != null)
            {
                if (Request.Cookies.ContainsKey(REPORTING_SESSION))
                {
                    string sessionId = Request.Cookies[REPORTING_SESSION];
            
                    Task<StatusCodeResult> statusCodeResult = IsAuthorised(sessionId);
                    if (!statusCodeResult.Result.StatusCode.Equals(StatusCodes.Status200OK))
                    {
                        return statusCodeResult.Result;
                    }
                }
                else
                {
                    return StatusCode(StatusCodes.Status401Unauthorized);
                }
            }

            var (gatewayAuthConfirmRequestRepresentation, error) =
                userAuthService.AuthConfirmResponse(authConfirmRequest);
            if (error != null)
                return StatusCode(StatusCodes.Status400BadRequest, error);
            var requestId = gatewayAuthConfirmRequestRepresentation.requestId;
            var cmSuffix = gatewayConfiguration.CmSuffix;

            try
            {
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth,
                    "Request for auth-confirm to gateway: {@GatewayResponse}",
                    gatewayAuthConfirmRequestRepresentation.dump(gatewayAuthConfirmRequestRepresentation));
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth, $" : {{cmSuffix}}, correlationId: {{correlationId}}," +
                                        $" authCode: {{authCode}}, transactionId: {{transactionId}} requestId: {{requestId}}",
                    cmSuffix, correlationId, gatewayAuthConfirmRequestRepresentation.credential.authCode,
                    gatewayAuthConfirmRequestRepresentation.transactionId, requestId);
                await gatewayClient.SendDataToGateway(PATH_AUTH_CONFIRM, gatewayAuthConfirmRequestRepresentation
                    , cmSuffix, correlationId);
                var i = 0;
                do
                {
                    Thread.Sleep(gatewayConfiguration.TimeOut + 8000);
                    if (RequestIdToErrorMessage.ContainsKey(requestId))
                    {
                        var gatewayError = RequestIdToErrorMessage[requestId];
                        RequestIdToErrorMessage.Remove(requestId);
                        return StatusCode(StatusCodes.Status400BadRequest,
                            new ErrorRepresentation(gatewayError));
                    }

                    if (RequestIdToAccessToken.ContainsKey(requestId) &&
                        RequestIdToPatientDetails.ContainsKey(requestId))
                    {
                        logger.LogInformation(LogEvents.UserAuth,
                            "Response about to be send for requestId: {RequestId} with accessToken: {AccessToken}",
                            requestId, RequestIdToAccessToken[requestId]
                        );
                        return Accepted(new AuthConfirmResponse(RequestIdToPatientDetails[requestId]));
                    }

                    i++;
                } while (i < gatewayConfiguration.Counter);
            }
            catch (Exception exception)
            {
                logger.LogError(LogEvents.UserAuth, exception, "Error happened for requestId: {RequestId}", requestId);
            }

            return StatusCode(StatusCodes.Status504GatewayTimeout,
                new ErrorRepresentation(new Error(ErrorCode.GatewayTimedOut, "Gateway timed out")));
        }

        [Authorize]
        [HttpPost(PATH_ON_AUTH_CONFIRM)]
        public async Task<ActionResult> SetAccessToken(OnAuthConfirmRequest request)
        {
            logger.Log(LogLevel.Information,
                LogEvents.UserAuth, "Auth on confirm request received." +
                                    $" RequestId:{request.requestID}, " +
                                    $" Timestamp:{request.timestamp}," +
                                    $" ResponseRequestId:{request.resp.RequestId}, ");
            if (request.error != null)
            {
                RequestIdToErrorMessage.Add(Guid.Parse(request.resp.RequestId), request.error);
                logger.Log(LogLevel.Information,
                    LogEvents.UserAuth, $" Error Code:{request.error.Code}," +
                                        $" Error Message:{request.error.Message}.");
            }
            else if (request.auth != null)
            {
                var (response, error) = await userAuthService.OnAuthConfirmResponse(request);
                if (error != null)
                    return StatusCode(StatusCodes.Status400BadRequest, error);
            }

            logger.Log(LogLevel.Information,
                LogEvents.UserAuth, $"Response RequestId:{request.resp.RequestId}");
            return Accepted();
        }

        [NonAction]
        public async Task<StatusCodeResult> IsAuthorised(String sessionId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, openMrsConfiguration.Url + WHO_AM_I);
            request.Headers.Add("Cookie", OPENMRS_SESSION_ID_COOKIE_NAME + "=" + sessionId);

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(StatusCodes.Status401Unauthorized);
            }

            return StatusCode(StatusCodes.Status200OK);
        }

        [Route(PATH_ADD_NDHM_DEMOGRAPHICS)]
        public async Task SetDemographicDetails(
            [FromBody] NDHMDemographicRequest ndhmDemographicRequest)
        {
            var healthId = ndhmDemographicRequest.HealthId;
            var name = ndhmDemographicRequest.Name;
            var gender = ndhmDemographicRequest.Gender;
            var dateOfBirth = ndhmDemographicRequest.DateOfBirth;
            var phoneNumber = ndhmDemographicRequest.PhoneNumber;
            var ndhmDemographics = new NdhmDemographics(healthId, name, gender, dateOfBirth, phoneNumber);
            await userAuthService.Dump(ndhmDemographics);
        }

        [Route(PATH_DEMOGRAPHICS)]
        public async Task DemographicAuth([FromBody] NdhmDemographics ndhmDemographics)
        {
            await userAuthService.CallAuthInit(ndhmDemographics.HealthId);
            await userAuthService.CallAuthConfirm(ndhmDemographics);
        }
    }
}