using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using In.ProjectEKA.HipLibrary.Patient.Model;
using In.ProjectEKA.HipService.Logger;
using In.ProjectEKA.HipService.OpenMrs;
using In.ProjectEKA.HipService.Patient.Database;
using In.ProjectEKA.HipService.Patient.Model;
using Optional;
using Optional.Unsafe;
using Task = System.Threading.Tasks.Task;

namespace In.ProjectEKA.HipService.Patient
{

    public class PatientProfileService : IPatientProfileService
    {
        private readonly OpenMrsConfiguration _openMrsConfiguration;
        private readonly PatientContext patientContext;

        public PatientProfileService(OpenMrsConfiguration openMrsConfiguration, PatientContext patientContext)
        {
            this._openMrsConfiguration = openMrsConfiguration;
            this.patientContext = patientContext;
        }

        public async Task<int> SavePatient(ShareProfileRequest shareProfileRequest)
        {
            var requesId = shareProfileRequest.RequestId;
            var timeStamp = shareProfileRequest.Timestamp.ToString();
            var hipCode = shareProfileRequest.Profile.HipCode;
            var patient = shareProfileRequest.Profile.PatientDemographics;
            var response = await Save(new PatientQueue(requesId, timeStamp, patient, hipCode));
            if(response.HasValue)
                Log.Information("Patient saved to queue");
            return response.ValueOrDefault();
        }

        private async Task<Option<int>> Save(PatientQueue patientQueue)
        {
            try
            {
                await patientContext.PatientQueue.AddAsync(patientQueue).ConfigureAwait(false);
                await patientContext.SaveChangesAsync();
                return Option.Some(patientQueue.TokenNumber);
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return Option.Some(patientQueue.TokenNumber);
            }
        }
        public bool IsValidRequest(ShareProfileRequest shareProfileRequest)
        {
            var profile = shareProfileRequest?.Profile;
            var demographics = profile?.PatientDemographics;
            return demographics is {HealthId: { }, Identifiers: { }, Name: { },Gender:{}} && Enum.IsDefined(typeof(Gender), demographics.Gender) && demographics.YearOfBirth != 0;
        }
        public async Task<List<PatientQueue>> GetPatientQueue()
        {
            try
            {
                var patientQueueRequest = patientContext.PatientQueue.ToList().FindAll(
                    patient => DateTime.Now.Subtract (DateTime.Parse(patient.DateTimeStamp)).TotalMinutes < _openMrsConfiguration.PatientQueueTimeLimit
                );

                return patientQueueRequest;
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, exception.StackTrace);
                return new List<PatientQueue>();
            }
        }

  
    }
}