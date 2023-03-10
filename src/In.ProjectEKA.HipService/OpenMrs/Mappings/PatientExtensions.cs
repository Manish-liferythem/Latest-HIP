namespace In.ProjectEKA.HipService.OpenMrs.Mappings
{
    using System.Linq;
    using HipLibrary.Patient.Model;

    public static class PatientExtensions
    {
        public static Patient ToHipPatient(this Hl7.Fhir.Model.Patient openMrsPatient, string patientSearchedName)
        {
            return new Patient()
            {
                Name = patientSearchedName,
                Gender = openMrsPatient.Gender.HasValue ? (Gender)((int)openMrsPatient.Gender) : (Gender?)null,
                YearOfBirth = (ushort?)openMrsPatient.BirthDateElement?.ToDateTimeOffset()?.Year,
                Identifier = GetIdentifier(openMrsPatient),
                Uuid = openMrsPatient.Id
            };
        }

        public static Hl7.Fhir.Model.AdministrativeGender? ToOpenMrsGender(this Gender? gender)
        {
            return gender.HasValue ? (Hl7.Fhir.Model.AdministrativeGender)((int)gender) : (Hl7.Fhir.Model.AdministrativeGender?)null;
        }

        private static string GetIdentifier(Hl7.Fhir.Model.Patient openMrsPatient) {
            return openMrsPatient.Identifier
                .Select(identifier =>identifier.Value )
                .FirstOrDefault();
        }
    }
}