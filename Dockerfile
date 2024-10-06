FROM mcr.microsoft.com/dotnet/runtime-deps:8.0

# Set the working directory
WORKDIR /app

# Copy the published files
COPY bin/Release/net8.0/linux-arm64/publish/ .

# Set the entry point
ENTRYPOINT ["./netatmo-prom-net"]