# Build stage — uses the full SDK image, only needed to compile the app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy just the project file first so dependency restore is cached
# separately from your actual code — faster rebuilds when only .cs files change
COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

# Runtime stage — much smaller image, only what's needed to actually run
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./

# Render assigns its own port via the PORT env var — Program.cs reads this
EXPOSE 8080

ENTRYPOINT ["dotnet", "TCGRider.Api.dll"]