# HMIS API image. Build context: repository root.
#   target "migrator" applies EF Core migrations for every module, then exits.
#   target "api" (default) runs the published API.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props Hmis.sln ./
COPY src/Api src/Api
COPY src/BuildingBlocks src/BuildingBlocks
COPY src/Modules src/Modules
RUN dotnet restore src/Api/Hmis.Api/Hmis.Api.csproj
RUN dotnet build src/Api/Hmis.Api/Hmis.Api.csproj -c Release --no-restore

FROM build AS publish
RUN dotnet publish src/Api/Hmis.Api/Hmis.Api.csproj -c Release --no-build -o /out

FROM build AS migrator
RUN dotnet tool install --global dotnet-ef --version 10.0.14
ENV PATH="$PATH:/root/.dotnet/tools"
COPY docker/api/migrate.sh /migrate.sh
RUN sed -i 's/\r$//' /migrate.sh && chmod +x /migrate.sh
ENTRYPOINT ["/migrate.sh"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
# libgssapi: optional Kerberos support probed by Npgsql at startup.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=publish /out .
RUN mkdir -p /app/logs /app/storage /home/app/.aspnet/DataProtection-Keys     && chown -R app:app /app/logs /app/storage /home/app/.aspnet
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Hmis.Api.dll"]
