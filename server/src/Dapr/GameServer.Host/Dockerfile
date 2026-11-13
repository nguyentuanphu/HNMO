FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app

# HNMO uses localized texts, which require the culture data of ICU.
# The alpine images don't contain it and run in the globalization-invariant mode by default.
# tzdata provides the IANA time zones (e.g. Europe/Berlin), which the alpine images don't contain either.
RUN apk add --no-cache icu-libs tzdata
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["Dapr/GameServer.Host/HNMO.GameServer.Host.csproj", "Dapr/GameServer.Host/"]
RUN dotnet restore "Dapr/GameServer.Host/HNMO.GameServer.Host.csproj"
COPY . .
WORKDIR "/src/Dapr/GameServer.Host"
RUN dotnet build "HNMO.GameServer.Host.csproj" -c Release -o /app/build  -p:ci=true

FROM build AS publish
RUN dotnet publish "HNMO.GameServer.Host.csproj" -c Release -o /app/publish  -p:ci=true

FROM base AS final

WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "HNMO.GameServer.Host.dll"]
