# trade-gateway-journey-tests

Journey tests for Trade Gateway services.

- [trade-gateway](https://github.com/DEFRA/trade-gateway)
- [trade-gateway-publisher](https://github.com/DEFRA/trade-gateway-publisher)

## Prerequisites

### Dependencies

Install the following:
- [.NET 10 (SDK)](https://dotnet.microsoft.com/)
- [Docker](https://docs.docker.com/engine/) (optional)

### Services

See [trade-gateway-local-environment](https://github.com/DEFRA/trade-gateway-local-environment) for instructions.

## Tests

### Local

Build as follows:

```bash
dotnet build
```

Run as follows:

```bash
dotnet test
```

### Docker

Build as follows:

```bash
docker build . -t trade-gateway-journey-tests
```

Run as follows:

```bash
docker run -it --rm --net=host \
  -e S3_ENDPOINT='http://localhost:4566' \
  -e RESULTS_OUTPUT_S3_PATH='s3://reports' \
  -e AWS_ACCESS_KEY_ID='test' \
  -e AWS_DEFAULT_REGION='eu-west-2' \
  -e AWS_SECRET_ACCESS_KEY='test' \
  -e AWS_SECRET_KEY='test' \
  -e AWS_REGION='eu-west-2' \
  trade-gateway-journey-tests
```

The test report is available from the `reports` S3 bucket. See [s3://reports](http://localhost:4566/reports/index.html) in your browser.

## Linting and formatting

[CSharpier](https://csharpier.com/) is used for linting and formatting.

Install .NET local tools as follows:

```bash
dotnet tool restore
```

Format all project files as follows:

```bash
dotnet csharpier format .
```

## Licence

THIS INFORMATION IS LICENSED UNDER THE CONDITIONS OF THE OPEN GOVERNMENT LICENCE found at:

<http://www.nationalarchives.gov.uk/doc/open-government-licence/version/3>

The following attribution statement MUST be cited in your products and applications when using this information.

> Contains public sector information licensed under the Open Government licence v3

### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable
information providers in the public sector to license the use and re-use of their information under a common open
licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
