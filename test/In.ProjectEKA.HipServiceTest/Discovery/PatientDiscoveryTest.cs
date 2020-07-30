namespace In.ProjectEKA.HipServiceTest.Discovery
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using HipLibrary.Matcher;
    using HipLibrary.Patient;
    using HipLibrary.Patient.Model;
    using HipService.Discovery;
    using HipService.Link;
    using HipService.Link.Model;
    using Moq;
    using Optional;
    using Xunit;
    using Match = HipLibrary.Patient.Model.Match;
    using static Builder.TestBuilders;
    using In.ProjectEKA.HipServiceTest.Discovery.Builder;

    public class PatientDiscoveryTest
    {
        private readonly PatientDiscovery patientDiscovery;

        private readonly Mock<IDiscoveryRequestRepository> discoveryRequestRepository =
            new Mock<IDiscoveryRequestRepository>();
        private readonly Mock<ILinkPatientRepository> linkPatientRepository = new Mock<ILinkPatientRepository>();
        private readonly Mock<IMatchingRepository> matchingRepository = new Mock<IMatchingRepository>();
        private readonly Mock<IPatientRepository> patientRepository = new Mock<IPatientRepository>();
        private readonly Mock<ICareContextRepository> careContextRepository = new Mock<ICareContextRepository>();

        DiscoveryRequestPayloadBuilder discoveryRequestBuilder;

        string openMrsPatientReferenceNumber;
        string name;
        string phoneNumber;
        string consentManagerUserId;
        string transactionId;
        ushort yearOfBirth;
        Gender gender;

        public PatientDiscoveryTest()
        {
            patientDiscovery = new PatientDiscovery(
                matchingRepository.Object,
                discoveryRequestRepository.Object,
                linkPatientRepository.Object,
                patientRepository.Object,
                careContextRepository.Object);

            openMrsPatientReferenceNumber = Faker().Random.String();
            name = Faker().Random.String();
            phoneNumber = Faker().Phone.PhoneNumber();
            consentManagerUserId = Faker().Random.String();
            transactionId = Faker().Random.String();
            yearOfBirth = 2019;
            gender = Gender.M;

            discoveryRequestBuilder = new DiscoveryRequestPayloadBuilder();

            discoveryRequestBuilder
                .WithPatientId(consentManagerUserId)
                .WithPatientName(name)
                .WithPatientGender(gender)
                .WithVerifiedIdentifiers(IdentifierType.MOBILE, phoneNumber)
                .WithUnverifiedIdentifiers(IdentifierType.MR, openMrsPatientReferenceNumber)
                .WithTransactionId(transactionId);

        }        

        [Fact]
        private async void ShouldReturnPatientForAlreadyLinkedPatient()
        {
            var alreadyLinked =
                new CareContextRepresentation(Faker().Random.Uuid().ToString(), Faker().Random.String());
            var unlinkedCareContext =
                new CareContextRepresentation(Faker().Random.Uuid().ToString(), Faker().Random.String());
            var expectedPatient = BuildExpectedPatientByExpectedMatchTypes(
                expectedCareContextRepresentation: unlinkedCareContext,
                expectedMatchTypes: Match.ConsentManagerUserId);
            var discoveryRequest = discoveryRequestBuilder.Build();
            SetupPatientRepository(alreadyLinked, unlinkedCareContext);
            SetupLinkRepositoryWithLinkedPatient(alreadyLinked, openMrsPatientReferenceNumber);

            var (discoveryResponse, error) = await patientDiscovery.PatientFor(discoveryRequest);

            discoveryResponse.Patient.Should().BeEquivalentTo(expectedPatient);
            discoveryRequestRepository.Verify(
                x => x.Add(It.Is<HipService.Discovery.Model.DiscoveryRequest>(
                    r => r.TransactionId == transactionId && r.ConsentManagerUserId == consentManagerUserId)),
                Times.Once);
            error.Should().BeNull();
        }

        [Fact]
        private async void ShouldReturnAPatientWhichIsNotLinkedAtAll()
        {
            var expectedPatient = BuildExpectedPatientByExpectedMatchTypes(
                Match.Mobile,
                Match.Name,
                Match.Gender,
                Match.Mr);
            var discoveryRequest = discoveryRequestBuilder.Build();
            SetupLinkRepositoryWithLinkedPatient();
            SetupMatchingRepositoryForDiscoveryRequest(discoveryRequest);

            var (discoveryResponse, error) = await patientDiscovery.PatientFor(discoveryRequest);

            discoveryResponse.Patient.Should().BeEquivalentTo(expectedPatient);
            discoveryRequestRepository.Verify(
                x => x.Add(It.Is<HipService.Discovery.Model.DiscoveryRequest>(
                    r => r.TransactionId == transactionId && r.ConsentManagerUserId == consentManagerUserId)),
                Times.Once);
            error.Should().BeNull();
        }

        [Fact]
        private async void ShouldReturnAPatientWhenUnverifiedIdentifierIsNull()
        {
            var referenceNumber = Faker().Random.String();
            var consentManagerUserId = Faker().Random.String();
            var transactionId = Faker().Random.String();
            var name = Faker().Name.FullName();
            const ushort yearOfBirth = 2019;
            var phoneNumber = Faker().Phone.PhoneNumber();
            var expectedPatient = new PatientEnquiryRepresentation(
                referenceNumber,
                name,
                new List<CareContextRepresentation>(),
                new List<string>
                {
                    Match.Mobile.ToString(),
                    Match.Name.ToString(),
                    Match.Gender.ToString()
                });
            var verifiedIdentifiers = new[] {new Identifier(IdentifierType.MOBILE, phoneNumber)};
            var patientRequest = new PatientEnquiry(consentManagerUserId,
                verifiedIdentifiers,
                null,
                name,
                Gender.M,
                yearOfBirth);
            var discoveryRequest = new DiscoveryRequest(patientRequest, RandomString(),transactionId, DateTime.Now);
            linkPatientRepository.Setup(e => e.GetLinkedCareContexts(consentManagerUserId))
                .ReturnsAsync(new Tuple<IEnumerable<LinkedAccounts>, Exception>(new List<LinkedAccounts>(), null));
            matchingRepository
                .Setup(repo => repo.Where(discoveryRequest))
                .Returns(Task.FromResult(new List<Patient>
                {
                    new Patient
                    {
                        Gender = Gender.M,
                        Identifier = referenceNumber,
                        Name = name,
                        CareContexts = new List<CareContextRepresentation>(),
                        PhoneNumber = phoneNumber,
                        YearOfBirth = yearOfBirth
                    }
                }.AsQueryable()));

            var (discoveryResponse, error) = await patientDiscovery.PatientFor(discoveryRequest);

            discoveryResponse.Patient.Should().BeEquivalentTo(expectedPatient);
            discoveryRequestRepository.Verify(
                x => x.Add(It.Is<HipService.Discovery.Model.DiscoveryRequest>(
                    r => r.TransactionId == transactionId && r.ConsentManagerUserId == consentManagerUserId)),
                Times.Once);
            error.Should().BeNull();
        }

        [Theory]
        [ClassData(typeof(EmptyIdentifierTestData))]
        private async void ReturnMultiplePatientsErrorWhenUnverifiedIdentifierIs(IEnumerable<Identifier> identifiers)
        {
            var expectedError =
                new ErrorRepresentation(new Error(ErrorCode.MultiplePatientsFound, "Multiple patients found"));
            var verifiedIdentifiers = new[] {new Identifier(IdentifierType.MOBILE, Faker().Phone.PhoneNumber())};
            var consentManagerUserId = Faker().Random.String();
            const ushort yearOfBirth = 2019;
            var gender = Faker().PickRandom<Gender>();
            var name = Faker().Name.FullName();
            var patientRequest = new PatientEnquiry(consentManagerUserId,
                verifiedIdentifiers,
                identifiers,
                name,
                gender,
                yearOfBirth);
            var discoveryRequest = new DiscoveryRequest(patientRequest, Faker().Random.String(), RandomString(), DateTime.Now);
            linkPatientRepository.Setup(e => e.GetLinkedCareContexts(consentManagerUserId))
                .ReturnsAsync(new Tuple<IEnumerable<LinkedAccounts>, Exception>(new List<LinkedAccounts>(), null));

            matchingRepository
                .Setup(repo => repo.Where(discoveryRequest))
                .Returns(Task.FromResult(new List<Patient>
                {
                    new Patient
                    {
                        YearOfBirth = yearOfBirth,
                        Gender = gender,
                        Name = name
                    },
                    new Patient
                    {
                        YearOfBirth = yearOfBirth,
                        Gender = gender,
                        Name = name
                    }
                }.AsQueryable()));

            var (discoveryResponse, error) = await patientDiscovery.PatientFor(discoveryRequest);

            discoveryResponse.Should().BeNull();
            error.Should().BeEquivalentTo(expectedError);
        }

        [Fact]
        private async void ShouldGetMultiplePatientsFoundErrorWhenSameUnverifiedIdentifiersAlsoMatch()
        {
            var expectedError =
                new ErrorRepresentation(new Error(ErrorCode.MultiplePatientsFound, "Multiple patients found"));
            var patientReferenceNumber = Faker().Random.String();
            var consentManagerUserId = Faker().Random.String();
            const ushort yearOfBirth = 2019;
            var gender = Faker().PickRandom<Gender>();
            var name = Faker().Name.FullName();
            var verifiedIdentifiers = new[] {new Identifier(IdentifierType.MOBILE, Faker().Phone.PhoneNumber())};
            var unverifiedIdentifiers = new[] {new Identifier(IdentifierType.MR, patientReferenceNumber)};
            var patientRequest = new PatientEnquiry(consentManagerUserId,
                verifiedIdentifiers,
                unverifiedIdentifiers,
                name,
                gender,
                yearOfBirth);
            var discoveryRequest = new DiscoveryRequest(patientRequest, Faker().Random.String(), RandomString(), DateTime.Now);
            linkPatientRepository.Setup(e => e.GetLinkedCareContexts(consentManagerUserId))
                .ReturnsAsync(new Tuple<IEnumerable<LinkedAccounts>, Exception>(new List<LinkedAccounts>(), null));

            matchingRepository
                .Setup(repo => repo.Where(discoveryRequest))
                .Returns(Task.FromResult(new List<Patient>
                {
                    new Patient
                    {
                        Identifier = patientReferenceNumber,
                        YearOfBirth = yearOfBirth,
                        Gender = gender,
                        Name = name
                    },
                    new Patient
                    {
                        Identifier = patientReferenceNumber,
                        YearOfBirth = yearOfBirth,
                        Gender = gender,
                        Name = name
                    }
                }.AsQueryable()));

            var (discoveryResponse, error) = await patientDiscovery.PatientFor(discoveryRequest);

            discoveryResponse.Should().BeNull();
            error.Should().BeEquivalentTo(expectedError);
        }

        [Fact]
        private async void ShouldGetNoPatientFoundErrorWhenVerifiedIdentifierDoesNotMatch()
        {
            var consentManagerUserId = Faker().Random.String();
            var expectedError = new ErrorRepresentation(new Error(ErrorCode.NoPatientFound, "No patient found"));
            var verifiedIdentifiers = new List<Identifier>
            {
                new Identifier(IdentifierType.MR, Faker().Phone.PhoneNumber())
            };
            var patientRequest = new PatientEnquiry(consentManagerUserId,
                verifiedIdentifiers,
                new List<Identifier>(),
                null,
                Gender.M,
                2019);
            var discoveryRequest = new DiscoveryRequest(patientRequest, Faker().Random.String(), RandomString(), DateTime.Now);
            linkPatientRepository.Setup(e => e.GetLinkedCareContexts(consentManagerUserId))
                .ReturnsAsync(new Tuple<IEnumerable<LinkedAccounts>, Exception>(new List<LinkedAccounts>(), null));

            var (discoveryResponse, error) = await patientDiscovery.PatientFor(discoveryRequest);

            discoveryResponse.Should().BeNull();
            error.Should().BeEquivalentTo(expectedError);
        }

        [Theory]
        [ClassData(typeof(EmptyIdentifierTestData))]
        private async void ReturnNoPatientFoundErrorWhenVerifiedIdentifierIs(IEnumerable<Identifier> identifiers)
        {
            var consentManagerUserId = Faker().Random.String();
            var expectedError = new ErrorRepresentation(new Error(ErrorCode.NoPatientFound, "No patient found"));
            var patientRequest = new PatientEnquiry(consentManagerUserId,
                identifiers,
                new List<Identifier>(),
                null,
                Gender.M,
                2019);
            var discoveryRequest = new DiscoveryRequest(patientRequest, Faker().Random.String(), RandomString(), DateTime.Now);
            linkPatientRepository.Setup(e => e.GetLinkedCareContexts(consentManagerUserId))
                .ReturnsAsync(new Tuple<IEnumerable<LinkedAccounts>, Exception>(new List<LinkedAccounts>(), null));

            var (discoveryResponse, error) = await patientDiscovery.PatientFor(discoveryRequest);

            discoveryResponse.Should().BeNull();
            error.Should().BeEquivalentTo(expectedError);
        }

        [Fact]
        private async void ShouldReturnAnErrorWhenDiscoveryRequestAlreadyExists()
        {
            var expectedError =
                new ErrorRepresentation(new Error(ErrorCode.DuplicateDiscoveryRequest, "Discovery Request already exists"));
            var transactionId = RandomString();
            var discoveryRequest = new DiscoveryRequest(null, RandomString(),transactionId, DateTime.Now);
            discoveryRequestRepository.Setup(repository => repository.RequestExistsFor(transactionId))
                .ReturnsAsync(true);

            var (discoveryResponse, error) = await patientDiscovery.PatientFor(discoveryRequest);

            discoveryResponse.Should().BeNull();
            error.Should().BeEquivalentTo(expectedError);
        }

        [Fact]
        private async void ShouldReturnPatientWithCareContexts()
        {
            var referenceNumber = Faker().Random.String();
            var consentManagerUserId = Faker().Random.String();
            var transactionId = Faker().Random.String();
            var name = Faker().Name.FullName();
            const ushort yearOfBirth = 2019;
            var phoneNumber = Faker().Phone.PhoneNumber();
            var careContextRepresentations = new[]
            {
                new CareContextRepresentation(Faker().Random.String(), Faker().Random.String()),
                new CareContextRepresentation(Faker().Random.String(), Faker().Random.String())
            };
            var expectedPatient = new PatientEnquiryRepresentation(
                referenceNumber,
                name,
                careContextRepresentations,
                new List<string>
                {
                    Match.Mobile.ToString(),
                    Match.Name.ToString(),
                    Match.Gender.ToString()
                });
            var verifiedIdentifiers = new[] {new Identifier(IdentifierType.MOBILE, phoneNumber)};
            var patientRequest = new PatientEnquiry(consentManagerUserId,
                verifiedIdentifiers,
                null,
                name,
                Gender.M,
                yearOfBirth);
            var discoveryRequest = new DiscoveryRequest(patientRequest, RandomString(),transactionId, DateTime.Now);
            linkPatientRepository.Setup(e => e.GetLinkedCareContexts(consentManagerUserId))
                .ReturnsAsync(new Tuple<IEnumerable<LinkedAccounts>, Exception>(new List<LinkedAccounts>(), null));
            careContextRepository.Setup(e => e.GetCareContexts(referenceNumber))
                .Returns(Task.FromResult(new List<CareContextRepresentation>(careContextRepresentations).AsEnumerable()));
            matchingRepository
                .Setup(repo => repo.Where(discoveryRequest))
                .Returns(Task.FromResult(new List<Patient>
                {
                    new Patient
                    {
                        Gender = Gender.M,
                        Identifier = referenceNumber,
                        Name = name,
                        CareContexts = new List<CareContextRepresentation>(),
                        PhoneNumber = phoneNumber,
                        YearOfBirth = yearOfBirth
                    }
                }.AsQueryable()));

            var (discoveryResponse, error) = await patientDiscovery.PatientFor(discoveryRequest);

            discoveryResponse.Patient.Should().BeEquivalentTo(expectedPatient);
            discoveryRequestRepository.Verify(
                x => x.Add(It.Is<HipService.Discovery.Model.DiscoveryRequest>(
                    r => r.TransactionId == transactionId && r.ConsentManagerUserId == consentManagerUserId)),
                Times.Once);
            error.Should().BeNull();
        }

        private PatientEnquiryRepresentation BuildExpectedPatientByExpectedMatchTypes(
            params Match[] expectedMatchTypes)
        {
            return BuildExpectedPatientByExpectedMatchTypes(null, expectedMatchTypes);
        }

        private PatientEnquiryRepresentation BuildExpectedPatientByExpectedMatchTypes(
            CareContextRepresentation expectedCareContextRepresentation, params Match[] expectedMatchTypes)
        {
            var expectedCareContexts =
                expectedCareContextRepresentation switch
                {
                    null => new List<CareContextRepresentation>(),
                    _ => new List<CareContextRepresentation> { expectedCareContextRepresentation }
                };

            return new PatientEnquiryRepresentation(openMrsPatientReferenceNumber,
                name,
                expectedCareContexts,
                expectedMatchTypes?.Select(m => m.ToString()));
        }

        private void SetupLinkRepositoryWithLinkedPatient(params string[] patientIds)
        {
            SetupLinkRepositoryWithLinkedPatient(null, patientIds);
        }

        private void SetupLinkRepositoryWithLinkedPatient(
            CareContextRepresentation linkedCareContextRepresentation, params string[] patientIds)
        {
            var linkedCareContexts =
                linkedCareContextRepresentation switch
                {
                    null => new List<CareContextRepresentation> {
                            new CareContextRepresentation(Faker().Random.Uuid().ToString(), Faker().Random.String())
                        },
                    _ => new List<CareContextRepresentation> { linkedCareContextRepresentation }
                };
            var linkedAccounts = patientIds.Select(p =>
                new LinkedAccounts(p,
                    Faker().Random.Hash(),
                    consentManagerUserId,
                    It.IsAny<string>(),
                    linkedCareContexts.Select(c => c.ReferenceNumber).ToList())
            );

            linkPatientRepository.Setup(e => e.GetLinkedCareContexts(consentManagerUserId))
                .ReturnsAsync(new Tuple<IEnumerable<LinkedAccounts>, Exception>(linkedAccounts, null));
        }

        private void SetupMatchingRepositoryForDiscoveryRequest(DiscoveryRequest discoveryRequest)
        {
            matchingRepository
                .Setup(repo => repo.Where(discoveryRequest))
                .Returns(Task.FromResult(new List<Patient>
                {
                    new Patient
                    {
                        Gender = gender,
                        Identifier = openMrsPatientReferenceNumber,
                        Name = name,
                        PhoneNumber = phoneNumber,
                        YearOfBirth = yearOfBirth
                    }
                }.AsQueryable()));

        }

        private void SetupPatientRepository(CareContextRepresentation alreadyLinked, CareContextRepresentation unlinkedCareContext)
        {
            var testPatient =
                new Patient
                {
                    PhoneNumber = phoneNumber,
                    Identifier = openMrsPatientReferenceNumber,
                    Gender = Faker().PickRandom<Gender>(),
                    Name = name,
                    CareContexts = new[]
                    {
                        alreadyLinked,
                        unlinkedCareContext
                    }
                };
            patientRepository.Setup(x => x.PatientWith(testPatient.Identifier))
                .Returns(Option.Some(testPatient));
        }
    }

    internal class EmptyIdentifierTestData : TheoryData<IEnumerable<Identifier>>
    {
        public EmptyIdentifierTestData()
        {
            Add(null);
            Add(new Identifier[] { });
        }
    }
}