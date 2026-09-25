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

    private static CertificateBuilder AChedA() =>
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
        var chedId = await factory.Simulator.CreateChed(AChedA(), token);

        var response = await factory.TracesGatewayChedClient.GetChedCertification(chedId, token);

        Assert.True(response.IsSuccessStatusCode, $"Response was not successful: {response.Error}");
        await Verify(response.Content).AddScrubber(text => text.Replace(chedId, "{ChedId}"));
    }

    [Fact]
    public async Task GetChedDecided()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);
        var chedId = await factory.Simulator.CreateChed(AChedA(), token);
        await factory.Simulator.PatchChed(
            chedId,
            Ched.ChedA().WithStatus("VALIDATED").WithClearance(clearance => clearance.Acceptable()),
            token
        );

        var response = await factory.TracesGatewayChedClient.GetChedCertification(chedId, token);

        Assert.True(response.IsSuccessStatusCode, $"Response was not successful: {response.Error}");
        await Verify(response.Content).AddScrubber(text => text.Replace(chedId, "{ChedId}"));
    }

    [Fact]
    public async Task GetChedNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);

        var response = await factory.TracesGatewayChedClient.GetChedCertification(UnknownChedId, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetChedNotAccessible()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);
        var chedId = await factory.Simulator.CreateChed(AChedA().NotAccessible(), token);

        var response = await factory.TracesGatewayChedClient.GetChedCertification(chedId, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FindChedUpdates()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);

        // The simulator stamps the update time as it stores, so a window around the create holds it.
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var chedId = await factory.Simulator.CreateChed(AChedA(), token);
        var to = DateTimeOffset.UtcNow.AddMinutes(1);

        var response = await factory.TracesGatewayChedClient.FindChedUpdates(
            from,
            to,
            pageSize: 10,
            offset: 1,
            cancellationToken: token
        );

        Assert.True(response.IsSuccessStatusCode, $"Response was not successful: {response.Error}");
        await Verify(response.Content).AddScrubber(text => text.Replace(chedId, "{ChedId}"));
    }
}
