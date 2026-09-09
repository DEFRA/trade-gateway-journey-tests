FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /app

COPY . .

RUN dotnet tool restore
RUN dotnet restore
RUN dotnet csharpier check .

FROM build AS publish

RUN dotnet publish tests/Journey -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS final

RUN apk add --no-cache aws-cli

WORKDIR /app

COPY --from=publish /app/publish .
COPY .config .config
COPY scripts scripts
COPY tests/Journey/*.verified.txt tests/Journey/
COPY global.json global.json

ENV HOME=/home/app
ENV PATH="$PATH:/home/app/.dotnet/tools"
RUN chown -R app:app /app
USER app
RUN dotnet tool restore

ENTRYPOINT [ "./scripts/entrypoint.sh" ]
