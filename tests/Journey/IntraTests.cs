namespace TradeGateway.Tests;

[Collection("Traces Gateway")]
public class IntraTests(TracesGatewayFactory factory)
{
    [Fact]
    public async Task GetIntraValid()
    {
        //setup place intra into system
        // The Stub already has a response for this, so we don't need to do anything here. In a real test, you would set up the expected data in the system before making the call.

        // act make intra get call for what was placed into system
        var response = await factory.TracesGatewayIntraClient.GetIntraCertification(
            "CHEDA.GB.2026.1234567",
            CancellationToken.None
        );

        // assert that response is good
        Assert.True(response.IsSuccessStatusCode, $"Response was not successful: {response.Error}");
        await Verify(response.Content);
    }
}
