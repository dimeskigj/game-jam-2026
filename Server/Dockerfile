# Dockerfile for Godot 4 Server (C#)

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

# Download Godot .NET
ENV GODOT_VERSION=4.3
ENV GODOT_RELEASE=stable
RUN apt-get update && apt-get install -y wget unzip libfontconfig1
# Detector for architecture
RUN ARCH=$(uname -m) && \
    if [ "$ARCH" = "x86_64" ]; then \
        GODOT_ARCH="x86_64"; \
    elif [ "$ARCH" = "aarch64" ] || [ "$ARCH" = "arm64" ]; then \
        GODOT_ARCH="arm64"; \
    else \
        echo "Unsupported architecture: $ARCH" && exit 1; \
    fi && \
    wget https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-${GODOT_RELEASE}/Godot_v${GODOT_VERSION}-${GODOT_RELEASE}_mono_linux_${GODOT_ARCH}.zip \
    && unzip Godot_v${GODOT_VERSION}-${GODOT_RELEASE}_mono_linux_${GODOT_ARCH}.zip \
    && mv Godot_v${GODOT_VERSION}-${GODOT_RELEASE}_mono_linux_${GODOT_ARCH}/Godot_v${GODOT_VERSION}-${GODOT_RELEASE}_mono_linux.${GODOT_ARCH} /usr/local/bin/godot \
    && mv Godot_v${GODOT_VERSION}-${GODOT_RELEASE}_mono_linux_${GODOT_ARCH}/GodotSharp /usr/local/bin/GodotSharp \
    && rm -rf Godot_v${GODOT_VERSION}-${GODOT_RELEASE}_mono_linux_${GODOT_ARCH}.zip Godot_v${GODOT_VERSION}-${GODOT_RELEASE}_mono_linux_${GODOT_ARCH}

WORKDIR /app
COPY . .

# Build the .NET solution
RUN dotnet build

# Run Godot in headless server mode
ENTRYPOINT ["godot", "--headless", "--server"]
