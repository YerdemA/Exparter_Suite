# The API as a container image (docs/deployment.md).
#
# The build stage runs on the machine that builds and publishes for the target
# architecture, so an arm64 image (the Oracle Cloud server) is built on an x64 runner
# without emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Restore first, from the project files alone, so that the layer is reused while only
# code changes.
COPY Directory.Build.props ./
COPY src/Exparter.Domain/Exparter.Domain.csproj src/Exparter.Domain/
COPY src/Exparter.Application/Exparter.Application.csproj src/Exparter.Application/
COPY src/Exparter.Infrastructure/Exparter.Infrastructure.csproj src/Exparter.Infrastructure/
COPY src/Exparter.Api/Exparter.Api.csproj src/Exparter.Api/
RUN dotnet restore src/Exparter.Api/Exparter.Api.csproj -a $TARGETARCH

COPY src/ src/
RUN dotnet publish src/Exparter.Api/Exparter.Api.csproj -a $TARGETARCH --no-restore -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./

# The image's built-in unprivileged user; the API listens on 8080, which needs no root.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Exparter.Api.dll"]
