FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AuditService.csproj", "."]
RUN dotnet restore "./AuditService.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "AuditService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AuditService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create logs directory
RUN mkdir -p logs

ENTRYPOINT ["dotnet", "AuditService.dll"]
