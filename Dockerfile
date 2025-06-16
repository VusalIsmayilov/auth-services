# Use the official .NET 8 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy csproj and restore dependencies
COPY AuthService.csproj .
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish AuthService.csproj -c Release -o /app

# Use the official .NET 8 runtime image for final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy the published app
COPY --from=build /app .

# Create directory for application data
RUN mkdir -p /app/data

# Expose port 80
EXPOSE 80

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "AuthService.dll"]