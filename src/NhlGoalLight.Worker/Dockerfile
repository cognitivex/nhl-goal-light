# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine
WORKDIR /app
# tzdata only — InvariantGlobalization=true in csproj means ICU is unused, so
# icu-libs would be dead weight. tzdata is enough for IANA time zone lookups
# (.NET reads /usr/share/zoneinfo directly).
# The dotnet alpine image already ships with a non-root `app` user/group, so we
# just reuse it instead of recreating (which would fail with "group in use").
RUN apk add --no-cache tzdata \
    && mkdir -p /app/data && chown -R app:app /app
USER app
COPY --from=build --chown=app:app /app .
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "NhlGoalLight.Worker.dll"]
