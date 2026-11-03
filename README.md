# trade-gateway-journey-tests

Journey tests for Trade Gateway services.

- [trade-gateway](https://github.com/DEFRA/trade-gateway)
- [trade-gateway-publisher](https://github.com/DEFRA/trade-gateway-publisher)

## Prerequisites

### Dependencies

Install the following:

- [.NET 10 (SDK)](https://dotnet.microsoft.com/)
- [Docker](https://docs.docker.com/engine/) (optional)

### Environment variables

Create `.env` file in the root of the project and provide necessary secrets (copy `.env.example`).

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
dotnet test --results-directory reports --report-html --report-html-filename index.html
```

The test report is available from the `reports` directory. See [path/to/project/reports/index.html](path/to/project/reports/index.html) in your browser.

### Docker

Build as follows:

```bash
docker build . -t trade-gateway-journey-tests --secret id=DEFRA_NUGET_PAT
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

### Service pull requests

Service repositories can run the journey tests against an image built from a pull request by calling the reusable
[journey-tests.yml](.github/workflows/journey-tests.yml) workflow. The calling job builds its image, uploads it as an
artifact with `docker save`, and passes:

| Input            | Description                                                                                      |
|------------------|--------------------------------------------------------------------------------------------------|
| `image-artifact` | Name of the artifact containing the `docker save` tarball                                        |
| `image-variable` | Image tag variable in the trade-gateway-local-environment `compose.yml`, e.g. `TRADE_GATEWAY` |
| `image-tag`      | Tag the image was saved with, e.g. `pr-123`                                                      |
| `journey-tests-ref` | Branch, tag or SHA of the journey tests to run (default `main`)                               |

The image must be tagged with the same repository name that `compose.yml` uses, e.g. `defradigital/trade-gateway:pr-123`.
The calling job must grant `contents: read`, `id-token: write` and `packages: read`.

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
