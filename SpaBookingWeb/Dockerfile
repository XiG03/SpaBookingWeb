# ========= BUILD STAGE =========
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file
COPY SpaBookingWeb/SpaBookingWeb.csproj SpaBookingWeb/
RUN dotnet restore SpaBookingWeb/SpaBookingWeb.csproj

# Copy source code
COPY . .
WORKDIR /src/SpaBookingWeb

# Build and publish
RUN dotnet publish \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:TreatWarningsAsErrors=false

# ========= RUNTIME STAGE =========
# Use Debian-based image to ensure ICU/globalization support
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install timezone data (Debian)
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata locales \
    && sed -i 's/# en_US.UTF-8 UTF-8/en_US.UTF-8 UTF-8/' /etc/locale.gen \
    && locale-gen en_US.UTF-8 \
    && update-locale LANG=en_US.UTF-8 \
    && rm -rf /var/lib/apt/lists/*

# Copy published app from build stage
COPY --from=build /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/healthz || exit 1

# Expose port
EXPOSE 8080

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Ensure full globalization support inside container
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LANG en_US.UTF-8

# Security: Run as non-root user (Debian)
RUN groupadd -r appgroup \
    && useradd -m -r -g appgroup -s /bin/bash appuser \
    && mkdir -p /home/appuser/.aspnet/DataProtection-Keys \
    && chown -R appuser:appgroup /home/appuser /app
USER appuser

# Entry point
ENTRYPOINT ["dotnet", "SpaBookingWeb.dll"]
