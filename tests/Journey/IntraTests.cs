
namespace TradeGateway.Tests;

[Collection("Traces Gateway")]
public class IntraTests(TracesGatewayFactory factory)
{
    [Fact]
    public async Task GetIntraValid()
    {
        //setup place intra into system


        // act make intra get call for what was placed into system
        var response = await factory.TracesGatewayIntraClient.GetIntraCertification("CHEDA.GB.2026.1234567", CancellationToken.None);


        // assert that response is good
        Assert.True(response.IsSuccessStatusCode);
        await Verify(response.Content);
    }
}