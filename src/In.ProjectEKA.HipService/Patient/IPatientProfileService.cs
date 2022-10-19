using System.Collections.Generic;
using System.Threading.Tasks;
using In.ProjectEKA.HipService.Patient.Model;

namespace In.ProjectEKA.HipService.Patient
{
    public interface IPatientProfileService
    {
        Task<int> SavePatient(ShareProfileRequest shareProfileRequest);
        bool IsValidRequest(ShareProfileRequest shareProfileRequest);
        Task<List<PatientQueue>> GetPatientQueue();
    }
}