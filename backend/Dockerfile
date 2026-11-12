# Backend Dockerfile for .NET 8 API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src
COPY ["src/Api/TaxNavi.Api.csproj", "src/Api/"]
COPY ["src/Application/TaxNavi.Application.csproj", "src/Application/"]
COPY ["src/Domain/TaxNavi.Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/TaxNavi.Infrastructure.csproj", "src/Infrastructure/"]

RUN dotnet restore "src/Api/TaxNavi.Api.csproj"

COPY . .
WORKDIR "/src/src/Api"
RUN dotnet build "TaxNavi.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TaxNavi.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# The base image has no fonts; without them QuestPDF renders PDFs with no text.
# DejaVu covers the Naira sign (U+20A6).
RUN apt-get update     && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core fonts-liberation     && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish /app/publish .
# Seed corpus for the AI knowledge base (official gazettes + manifest), indexed on first start.
COPY knowledge-base ./knowledge-base

# Render sets PORT (the app binds to it); 8080 is the fallback for other hosts.
ENV ASPNETCORE_URLS=http://+:8080     ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "TaxNavi.Api.dll"]
