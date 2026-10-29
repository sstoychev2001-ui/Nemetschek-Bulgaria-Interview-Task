# Build context is the repository root, so Directory.Build.props and global.json are available.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Project files first, so a source-only change reuses the restored package layer.
COPY global.json Directory.Build.props ./
COPY Nemetschek.Archive/Nemetschek.Archive.Api/*.csproj            Nemetschek.Archive/Nemetschek.Archive.Api/
COPY Nemetschek.Archive/Nemetschek.Archive.Core/*.csproj           Nemetschek.Archive/Nemetschek.Archive.Core/
COPY Nemetschek.Archive/Nemetschek.Archive.Infrastructure/*.csproj Nemetschek.Archive/Nemetschek.Archive.Infrastructure/
RUN dotnet restore Nemetschek.Archive/Nemetschek.Archive.Api

COPY Nemetschek.Archive/Nemetschek.Archive.Api/            Nemetschek.Archive/Nemetschek.Archive.Api/
COPY Nemetschek.Archive/Nemetschek.Archive.Core/           Nemetschek.Archive/Nemetschek.Archive.Core/
COPY Nemetschek.Archive/Nemetschek.Archive.Infrastructure/ Nemetschek.Archive/Nemetschek.Archive.Infrastructure/
RUN dotnet publish Nemetschek.Archive/Nemetschek.Archive.Api -c Release -o /app --no-restore

# Debian-based rather than Alpine on purpose: the export rules interpret the user's calendar dates in
# their own time zone, which needs the ICU and tzdata the Alpine images leave out.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# curl is here only so the compose healthcheck has something to call; the aspnet image ships no client.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# cabinets.json is deliberately NOT copied in. It is mounted read-only by compose, so the same image
# runs with a different cabinet set, and nothing about a particular deployment is baked into the image.

# The mount point for the data volume has to exist in the image and be owned by the user the process
# runs as. An empty named volume takes its ownership from the image's directory the first time it is
# mounted, so without this the volume root would belong to root, the non-root process could not write
# to it, and the first upload would fail with a permission error.
RUN mkdir -p /var/lib/archive && chown -R $APP_UID /var/lib/archive

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Nemetschek.Archive.Api.dll"]
