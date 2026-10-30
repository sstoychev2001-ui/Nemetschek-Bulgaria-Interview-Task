FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props ./
COPY Nemetschek.Archive/Nemetschek.Archive.Api/*.csproj            Nemetschek.Archive/Nemetschek.Archive.Api/
COPY Nemetschek.Archive/Nemetschek.Archive.Core/*.csproj           Nemetschek.Archive/Nemetschek.Archive.Core/
COPY Nemetschek.Archive/Nemetschek.Archive.Infrastructure/*.csproj Nemetschek.Archive/Nemetschek.Archive.Infrastructure/
RUN dotnet restore Nemetschek.Archive/Nemetschek.Archive.Api

COPY Nemetschek.Archive/Nemetschek.Archive.Api/            Nemetschek.Archive/Nemetschek.Archive.Api/
COPY Nemetschek.Archive/Nemetschek.Archive.Core/           Nemetschek.Archive/Nemetschek.Archive.Core/
COPY Nemetschek.Archive/Nemetschek.Archive.Infrastructure/ Nemetschek.Archive/Nemetschek.Archive.Infrastructure/
RUN dotnet publish Nemetschek.Archive/Nemetschek.Archive.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

RUN mkdir -p /var/lib/archive && chown -R $APP_UID /var/lib/archive

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Nemetschek.Archive.Api.dll"]
