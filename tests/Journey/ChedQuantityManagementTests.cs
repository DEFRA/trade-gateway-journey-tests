using System.Net;
using Api.TradeTracesNTStub.TestKit;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Customs;

namespace TradeGateway.Tests;

/// <summary>
/// Customs quantity journeys through Trade Gateway against the TRACES NT simulator: read the ledger,
/// reserve against a declaration, then release or delete it. The ledger is stateful, so each journey
/// reads it back after acting rather than trusting the response to the action alone.
/// </summary>
/// <remarks>
/// Each test creates its own CHED of 900 kg on one line. What TRACES answers in each case was
/// captured from acceptance; see the stub's <c>Captures/Customs</c>.
/// </remarks>
[Collection("Traces Gateway")]
public class ChedQuantityManagementTests(TracesGatewayFactory factory)
{
    private const string Mrn = "26GBJOURNEY0000001";
    private const string OtherMrn = "26GBJOURNEY0000002";
    private const string UnknownChedId = "CHEDA.XI.2026.9999999";

    private static CertificateBuilder AValidatedChedOf900Kg() =>
        Ched.ChedA()
            .WithStatus("VALIDATED")
            .WithConsignment(consignment =>
                consignment
                    .ArrivingAt("GBBEL", "XI")
                    .ExportedFrom("AF")
                    .ImportedTo("XI")
                    .WithCommodity(commodity => commodity.CnCode("0101").OriginCountry("AF").NetWeightKg(900))
            );

    /// <summary>Two live bovine animals, counted in pieces as TRACES counts a CHED-A.</summary>
    private static CertificateBuilder AValidatedChedOfTwoAnimals() =>
        Ched.ChedA()
            .WithStatus("VALIDATED")
            .WithConsignment(consignment =>
                consignment
                    .ArrivingAt("GBBEL", "XI")
                    .ExportedFrom("AF")
                    .ImportedTo("XI")
                    .WithCommodity(commodity => commodity.CnCode("0102").OriginCountry("AF").Pieces(2))
            );

    private static ChedReservationRequest AReservationOfKilograms(decimal quantity) =>
        AReservationByWeight(quantity, "KGM");

    private static ChedReservationRequest AReservationByWeight(
        decimal quantity,
        string unitOfMeasure,
        int certificateLineNumber = 1,
        string classCode = "0101"
    ) =>
        new()
        {
            Items =
            [
                new ReservationCommodityItem
                {
                    GoodsItemNumber = 1,
                    CertificateLineNumber = certificateLineNumber,
                    ClassCode = classCode,
                    NetWeightQuantity = quantity,
                    NetWeightUnitOfMeasure = unitOfMeasure,
                },
            ],
        };

    /// <summary>Animals travel as a net volume in pieces (<c>H87</c>), as the CHED-A carries them.</summary>
    private static ChedReservationRequest AReservationOfAnimals(decimal count) =>
        new()
        {
            Items =
            [
                new ReservationCommodityItem
                {
                    GoodsItemNumber = 1,
                    CertificateLineNumber = 1,
                    ClassCode = "0102",
                    NetVolumeQuantity = count,
                    NetVolumeUnitOfMeasure = "H87",
                },
            ],
        };

    private ITracesGatewayChedClient CustomsClient => factory.TracesGatewayChedClient;

    [Fact]
    public async Task ReadTheLedger()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);

        var ledger = await ReadLedger(chedId, token);

        var availableLine = ledger.Available.Should().ContainSingle().Subject;
        availableLine.Quantity.Should().Be(900m);
        availableLine.CertificateLineNumber.Should().Be(1);
        availableLine.UnitOfMeasure.Should().Be("KGM");
        ledger.Allocations!.Reserved.Should().BeEmpty();
        ledger.Allocations.Consumed.Should().BeEmpty();
    }

    [Fact]
    public async Task ReserveThenRelease()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);

        var reservationResponse = await CustomsClient.PutChedReservation(
            chedId,
            Mrn,
            AReservationOfKilograms(300),
            token
        );
        reservationResponse
            .IsSuccessStatusCode.Should()
            .BeTrue($"the reservation should succeed: {reservationResponse.ProblemBody()}");
        reservationResponse.Content!.Reserved.Should().ContainSingle().Which.Quantity.Should().Be(300m);

        var ledgerAfterReserve = await ReadLedger(chedId, token);
        ledgerAfterReserve.Available.Should().ContainSingle().Which.Quantity.Should().Be(600m);
        ledgerAfterReserve
            .Allocations!.Reserved.Should()
            .ContainSingle()
            .Which.DeclarationReference!.Value.Should()
            .Be(Mrn);

        using var releaseResponse = await CustomsClient.ReleaseChedReservation(chedId, Mrn, token);
        releaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var ledgerAfterRelease = await ReadLedger(chedId, token);
        ledgerAfterRelease
            .Available.Should()
            .ContainSingle()
            .Which.Quantity.Should()
            .Be(600m, "releaseResponse quantity is consumed for good, not given back");
        ledgerAfterRelease.Allocations!.Reserved.Should().BeEmpty();
        ledgerAfterRelease.Allocations.Consumed.Should().ContainSingle().Which.Quantity.Should().Be(300m);
    }

    [Fact]
    public async Task ReserveThenDelete()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);
        await CustomsClient.PutChedReservation(chedId, Mrn, AReservationOfKilograms(300), token);

        using var deleteResponse = await CustomsClient.DeleteChedReservation(chedId, Mrn, token);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ledgerAfterDelete = await ReadLedger(chedId, token);
        ledgerAfterDelete
            .Available.Should()
            .ContainSingle()
            .Which.Quantity.Should()
            .Be(900m, "a deleteResponse reservation gives its quantity back");
        ledgerAfterDelete.Allocations!.Reserved.Should().BeEmpty();
        ledgerAfterDelete.Allocations.Consumed.Should().BeEmpty();
    }

    [Fact]
    public async Task ReservingAgainReplacesTheHold()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);
        await CustomsClient.PutChedReservation(chedId, Mrn, AReservationOfKilograms(300), token);

        await CustomsClient.PutChedReservation(chedId, Mrn, AReservationOfKilograms(100), token);

        var ledgerAfterReplacement = await ReadLedger(chedId, token);
        ledgerAfterReplacement.Available.Should().ContainSingle().Which.Quantity.Should().Be(800m);
        ledgerAfterReplacement.Allocations!.Reserved.Should().ContainSingle().Which.Quantity.Should().Be(100m);
    }

    [Fact]
    public async Task ReservingMoreThanIsAvailableIsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);
        await CustomsClient.PutChedReservation(chedId, OtherMrn, AReservationOfKilograms(800), token);

        var reservationResponse = await CustomsClient.PutChedReservation(
            chedId,
            Mrn,
            AReservationOfKilograms(200),
            token
        );

        reservationResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        reservationResponse.ProblemBody().Should().Contain("\"05\"", "05 is Quantities insufficient");
        (await ReadLedger(chedId, token)).Available.Should().ContainSingle().Which.Quantity.Should().Be(100m);
    }

    [Fact]
    public async Task ARefusedReplacementEndsTheExistingHold()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);
        await CustomsClient.PutChedReservation(chedId, Mrn, AReservationOfKilograms(300), token);

        var replacementResponse = await CustomsClient.PutChedReservation(
            chedId,
            Mrn,
            AReservationOfKilograms(1000),
            token
        );

        replacementResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var ledgerAfterRefusal = await ReadLedger(chedId, token);
        ledgerAfterRefusal.Available.Should().ContainSingle().Which.Quantity.Should().Be(900m);
        ledgerAfterRefusal.Allocations!.Reserved.Should().BeEmpty();
    }

    [Fact]
    public async Task ARefusalOnACheckKeepsTheExistingHold()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);
        await CustomsClient.PutChedReservation(chedId, Mrn, AReservationOfKilograms(300), token);

        var replacementResponse = await CustomsClient.PutChedReservation(
            chedId,
            Mrn,
            AReservationByWeight(1, "KGM", certificateLineNumber: 9),
            token
        );

        replacementResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        replacementResponse.ProblemBody().Should().Contain("\"07\"", "07 is Line numbers mismatch");
        var ledgerAfterRefusal = await ReadLedger(chedId, token);
        ledgerAfterRefusal
            .Allocations!.Reserved.Should()
            .ContainSingle("only a refusal for want of quantity ends the hold")
            .Which.Quantity.Should()
            .Be(300m);
    }

    [Fact]
    public async Task ReserveInGramsAgainstAKilogramLine()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);

        var reservationResponse = await CustomsClient.PutChedReservation(
            chedId,
            Mrn,
            AReservationByWeight(500, "GRM"),
            token
        );

        var reservation = reservationResponse.Content!.Reserved.Should().ContainSingle().Subject;
        reservation.UnitOfMeasure.Should().Be("GRM", "TRACES reports a reservation in the unit it was made in");
        reservation.Quantity.Should().Be(500m);
        var ledgerAfterReserve = await ReadLedger(chedId, token);
        var availableLine = ledgerAfterReserve.Available.Should().ContainSingle().Subject;
        availableLine.UnitOfMeasure.Should().Be("KGM");
        availableLine.Quantity.Should().Be(899.5m);
    }

    [Fact]
    public async Task ReserveInTonnesAgainstAKilogramLine()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);

        await CustomsClient.PutChedReservation(chedId, Mrn, AReservationByWeight(0.1m, "TNE"), token);

        var ledgerAfterReserve = await ReadLedger(chedId, token);
        ledgerAfterReserve.Available.Should().ContainSingle().Which.Quantity.Should().Be(800m);
    }

    [Fact]
    public async Task ReserveAnimalsInPieces()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOfTwoAnimals(), token);

        var reservationResponse = await CustomsClient.PutChedReservation(chedId, Mrn, AReservationOfAnimals(1), token);

        reservationResponse
            .IsSuccessStatusCode.Should()
            .BeTrue($"the reservation should succeed: {reservationResponse.ProblemBody()}");
        var ledgerAfterReserve = await ReadLedger(chedId, token);
        var availableLine = ledgerAfterReserve.Available.Should().ContainSingle().Subject;
        availableLine.UnitOfMeasure.Should().Be("H87");
        availableLine.Quantity.Should().Be(1m);
        ledgerAfterReserve
            .Allocations!.Reserved.Should()
            .ContainSingle()
            .Which.CommodityCode!.HarmonizedSystemSubheadingCode.Should()
            .Be("010200", "TRACES reports the declared code as a six-digit subheading");
    }

    [Fact]
    public async Task KilogramsCannotBeReservedAgainstAnimals()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOfTwoAnimals(), token);
        await CustomsClient.PutChedReservation(chedId, Mrn, AReservationOfAnimals(1), token);

        var replacementResponse = await CustomsClient.PutChedReservation(
            chedId,
            Mrn,
            AReservationByWeight(1, "KGM", classCode: "0102"),
            token
        );

        replacementResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        replacementResponse.ProblemBody().Should().Contain("\"10\"", "10 is Measurement unit mismatch");
        var ledgerAfterRefusal = await ReadLedger(chedId, token);
        ledgerAfterRefusal.Allocations!.Reserved.Should().ContainSingle("a unit mismatch leaves the hold alone");
    }

    [Fact]
    public async Task AChedThatIsNotValidatedCannotBeReservedAgainst()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg().WithStatus("NEW"), token);

        var reservationResponse = await CustomsClient.PutChedReservation(
            chedId,
            Mrn,
            AReservationOfKilograms(100),
            token
        );

        reservationResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        reservationResponse.ProblemBody().Should().Contain("\"04\"", "04 is Inappropriate status");
    }

    [Fact]
    public async Task ReleasingTwiceIsAConflict()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);
        await CustomsClient.PutChedReservation(chedId, Mrn, AReservationOfKilograms(300), token);
        using var firstReleaseResponse = await CustomsClient.ReleaseChedReservation(chedId, Mrn, token);

        using var secondReleaseResponse = await CustomsClient.ReleaseChedReservation(chedId, Mrn, token);

        secondReleaseResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReleasingWithNothingReservedIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var chedId = await factory.Simulator.CreateChed(AValidatedChedOf900Kg(), token);

        using var releaseResponse = await CustomsClient.ReleaseChedReservation(chedId, Mrn, token);

        releaseResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnUnknownChedIsNotFound()
    {
        var token = TestContext.Current.CancellationToken;

        var quantitiesResponse = await CustomsClient.GetChedQuantities(UnknownChedId, token);

        quantitiesResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<ChedQuantityLedger> ReadLedger(string chedId, CancellationToken token)
    {
        var quantitiesResponse = await CustomsClient.GetChedQuantities(chedId, token);
        quantitiesResponse
            .IsSuccessStatusCode.Should()
            .BeTrue($"reading the ledger should succeed: {quantitiesResponse.ProblemBody()}");
        return quantitiesResponse.Content!;
    }
}
