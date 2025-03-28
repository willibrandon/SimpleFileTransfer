# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["test/SimpleFileTransfer.Tests/SimpleFileTransfer.Tests.csproj", "test/SimpleFileTransfer.Tests/"]
COPY ["src/SimpleFileTransfer/SimpleFileTransfer.csproj", "src/SimpleFileTransfer/"]
RUN dotnet restore "./test/SimpleFileTransfer.Tests/SimpleFileTransfer.Tests.csproj"
COPY . .
WORKDIR "/src/test/SimpleFileTransfer.Tests"
RUN dotnet build "./SimpleFileTransfer.Tests.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Skip running tests during build to make the build process faster
# Tests will be run when the container is executed

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SimpleFileTransfer.Tests.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Change the entrypoint to run tests with detailed output
ENTRYPOINT ["dotnet", "test", "SimpleFileTransfer.Tests.dll", "--logger", "console;verbosity=detailed"]