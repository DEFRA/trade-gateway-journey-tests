using System.Net;
using Api.TradeTracesNTStub.TestKit;

namespace TradeGateway.Tests;

/// <summary>
/// INTRA journeys through Trade Gateway against the TRACES NT simulator. Each test resets the
/// simulator and creates exactly the INTRA it needs, so the suite passes in any order.
/// </summary>
[Collection("Traces Gateway")]
public class IntraTests(TracesGatewayFactory factory)
{
    // Issued by the simulator and different each run; see ChedTests.
    private const string UnknownIntraId = "INTRA.XI.2026.9999999";

    private static CertificateBuilder AnIntra() =>
        Intra
            .OfModel("64/432 (2016/2008) F1 Bovine")
            .WithStatus("VALIDATED")
            .WithConsignment(consignment =>
                consignment
                    .ExportedFrom("AF")
                    .ImportedTo("XI")
                    .WithConsignor(party => party.Operator("770198").InCountry("XI"))
                    .WithConsignee(party => party.Operator("899361").InCountry("XI"))
                    .WithDespatchParty(party => party.Operator("770198").InCountry("XI"))
                    .CarryingCargoType("12")
                    .WithCommodity(commodity =>
                        commodity.CnCode("0101").OriginCountry("AF").Packages(2, "BX").NetWeightKg(900)
                    )
            );

    [Fact]
    public async Task GetIntraValid()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);
        var intraId = await factory.Simulator.CreateIntra(AnIntra(), token);

        var response = await factory.TracesGatewayIntraClient.GetIntraCertification(intraId, token);

        Assert.True(response.IsSuccessStatusCode, $"Response was not successful: {response.Error}");
        await Verify(response.Content).AddScrubber(text => text.Replace(intraId, "{IntraId}"));
    }

    [Fact]
    public async Task GetIntraNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);

        var response = await factory.TracesGatewayIntraClient.GetIntraCertification(UnknownIntraId, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetIntraNotAccessible()
    {
        var token = TestContext.Current.CancellationToken;
        await factory.Simulator.Reset(token);
        var intraId = await factory.Simulator.CreateIntra(AnIntra().NotAccessible(), token);

        var response = await factory.TracesGatewayIntraClient.GetIntraCertification(intraId, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
