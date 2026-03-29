FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Central.Core/Central.Core.csproj Central.Core/
COPY Central.Data/Central.Data.csproj Central.Data/
COPY Central.Api.Client/Central.Api.Client.csproj Central.Api.Client/
COPY Central.Api/Central.Api.csproj Central.Api/
COPY NuGet.config .
RUN dotnet restore Central.Api/Central.Api.csproj

COPY Central.Core/ Central.Core/
COPY Central.Data/ Central.Data/
COPY Central.Api.Client/ Central.Api.Client/
COPY Central.Api/ Central.Api/
RUN dotnet publish Central.Api/Central.Api.csproj -c Release -o /app --no-restore

COPY db/migrations/ /app/migrations/

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:5000
ENV CENTRAL_DSN="Host=postgres;Port=5432;Database=central;Username=central;Password=central"
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD curl -sf http://localhost:5000/api/health || exit 1

ENTRYPOINT ["dotnet", "Central.Api.dll"]
