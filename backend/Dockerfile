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
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "TaxNavi.Api.dll"]
