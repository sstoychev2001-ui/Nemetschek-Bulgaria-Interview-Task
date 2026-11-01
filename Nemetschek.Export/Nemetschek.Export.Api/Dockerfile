FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props ./
COPY Nemetschek.Export/Nemetschek.Export.Api/*.csproj            Nemetschek.Export/Nemetschek.Export.Api/
COPY Nemetschek.Export/Nemetschek.Export.Core/*.csproj           Nemetschek.Export/Nemetschek.Export.Core/
COPY Nemetschek.Export/Nemetschek.Export.Infrastructure/*.csproj Nemetschek.Export/Nemetschek.Export.Infrastructure/
RUN dotnet restore Nemetschek.Export/Nemetschek.Export.Api

COPY Nemetschek.Export/Nemetschek.Export.Api/            Nemetschek.Export/Nemetschek.Export.Api/
COPY Nemetschek.Export/Nemetschek.Export.Core/           Nemetschek.Export/Nemetschek.Export.Core/
COPY Nemetschek.Export/Nemetschek.Export.Infrastructure/ Nemetschek.Export/Nemetschek.Export.Infrastructure/
RUN dotnet publish Nemetschek.Export/Nemetschek.Export.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app .

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Nemetschek.Export.Api.dll"]
