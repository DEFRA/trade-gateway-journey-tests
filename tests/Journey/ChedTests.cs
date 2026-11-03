using System.Net;
using Api.TradeTracesNTStub.TestKit;

namespace TradeGateway.Tests;

/// <summary>
/// CHED journeys through Trade Gateway against the TRACES NT simulator. Each test resets the simulator
/// and creates exactly the CHEDs it needs, so the suite passes in any order and on any machine.
/// </summary>
[Collection("Traces Gateway")]
public class ChedTests(TracesGatewayFactory factory)
{
    // The simulator issues IDs from a serial that reset does not rewind, so a created CHED's ID
    // differs run to run. Tests use the ID the create returns and scrub it from snapshots.
    private const string UnknownChedId = "CHEDA.XI.2026.9999999";

    private static CertificateBuilder ANewChedA() =>
        Ched.ChedA()
            .WithStatus("NEW")
            .WithDeclaration(declaration => declaration.Declaring("FREE_CIRCULATION", "FATTENING"))
            .WithConsignment(consignment =>
                consignment
                    .ArrivingAt("GBBEL", "XI")
                    .ExportedFrom("AF")
                    .ImportedTo("XI")
                    .WithConsignor(party => party.Operator("770198").InCountry("XI"))
                    .WithConsignee(party => party.Operator("899361").InCountry("XI"))
                    .CarryingCargoType("12")
                    .WithCommodity(commodity =>
                        commodity.CnCode("0101").OriginCountry("AF").Packages(2, "BX").NetWeightKg(900)
                    )
            );

    [Fact]
    public async Task GetChedValid()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);
        var chedId = await factory.Simulator.CreateChed(ANewChedA(), token);

        var chedResponse = await factory.TracesGatewayChedClient.GetChedCertification(chedId, token);

        chedResponse
            .IsSuccessStatusCode.Should()
            .BeTrue($"the chedResponse should succeed: {chedResponse.ProblemBody()}");
        await Verify(chedResponse.Content).AddScrubber(text => text.Replace(chedId, "{ChedId}"));
    }

    [Fact]
    public async Task GetChedDecided()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);
        var chedId = await factory.Simulator.CreateChed(ANewChedA(), token);
        await factory.Simulator.PatchChed(
            chedId,
            Ched.ChedA().WithStatus("VALIDATED").WithClearance(clearance => clearance.Acceptable()),
            token
        );

        var chedResponse = await factory.TracesGatewayChedClient.GetChedCertification(chedId, token);

        chedResponse
            .IsSuccessStatusCode.Should()
            .BeTrue($"the chedResponse should succeed: {chedResponse.ProblemBody()}");
        await Verify(chedResponse.Content).AddScrubber(text => text.Replace(chedId, "{ChedId}"));
    }

    [Fact]
    public async Task GetChedNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);

        var chedResponse = await factory.TracesGatewayChedClient.GetChedCertification(UnknownChedId, token);

        chedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetChedNotAccessible()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);
        var chedId = await factory.Simulator.CreateChed(ANewChedA().NotAccessible(), token);

        var chedResponse = await factory.TracesGatewayChedClient.GetChedCertification(chedId, token);

        chedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FindChedUpdates()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);

        // The simulator stamps the update time as it stores, so a window around the create holds it.
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var chedId = await factory.Simulator.CreateChed(ANewChedA(), token);
        var to = DateTimeOffset.UtcNow.AddMinutes(1);

        var updatesResponse = await factory.TracesGatewayChedClient.FindChedUpdates(
            from,
            to,
            pageSize: 10,
            offset: 1,
            cancellationToken: token
        );

        updatesResponse
            .IsSuccessStatusCode.Should()
            .BeTrue($"the updatesResponse should succeed: {updatesResponse.ProblemBody()}");
        await Verify(updatesResponse.Content).AddScrubber(text => text.Replace(chedId, "{ChedId}"));
    }
}
