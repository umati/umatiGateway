# Use the official .NET SDK image to build the project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory
WORKDIR /source

# Copy the .sln file and restore dependencies
COPY *.sln ./
COPY umatiGateway/*.csproj ./umatiGateway/
RUN dotnet restore

# Copy the entire project and build it
COPY umatiGateway/. ./umatiGateway/
WORKDIR /source/umatiGateway
RUN dotnet publish -c Release -o /app

# Use the official .NET runtime image for the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Expose port 80 for the application
EXPOSE 8080

# Set the entry point to the application
ENTRYPOINT ["dotnet", "umatiGateway.dll"]
