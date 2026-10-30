# Multi-stage build for the Assignment Tracker API.
# Build: docker build -t assignment-tracker-api .

# ---- build stage: restore and publish with the full SDK ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until the .csproj changes
COPY AssignmentTracker/AssignmentTracker.csproj AssignmentTracker/
RUN dotnet restore AssignmentTracker/AssignmentTracker.csproj

COPY AssignmentTracker/ AssignmentTracker/
RUN dotnet publish AssignmentTracker/AssignmentTracker.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage: small ASP.NET image, runs as non-root ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "AssignmentTracker.dll"]
