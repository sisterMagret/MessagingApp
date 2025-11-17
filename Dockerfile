# Use the official .NET 9.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file
COPY MessagingApp.sln .

# Copy project files
COPY src/Api/Api.csproj src/Api/
COPY src/Core/Core.csproj src/Core/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/

# Restore dependencies
RUN dotnet restore

# Copy all source files
COPY . .

# Build the application
WORKDIR /src/src/Api
RUN dotnet build -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Use the official .NET 9.0 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create uploads directory
RUN mkdir -p uploads

# Copy published application
COPY --from=publish /app/publish .

# Expose port
EXPOSE 80

# Set entry point
ENTRYPOINT ["dotnet", "Api.dll"]