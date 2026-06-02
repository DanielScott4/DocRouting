FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/PadesSign.Api/PadesSign.Api.csproj",            "src/PadesSign.Api/"]
COPY ["src/PadesSign.Application/PadesSign.Application.csproj", "src/PadesSign.Application/"]
COPY ["src/PadesSign.Infrastructure/PadesSign.Infrastructure.csproj", "src/PadesSign.Infrastructure/"]
COPY ["src/PadesSign.Domain/PadesSign.Domain.csproj",      "src/PadesSign.Domain/"]
RUN dotnet restore "src/PadesSign.Api/PadesSign.Api.csproj"
COPY . .
RUN dotnet publish "src/PadesSign.Api/PadesSign.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PadesSign.Api.dll"]