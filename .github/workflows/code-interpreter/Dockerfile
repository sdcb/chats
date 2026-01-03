FROM mcr.microsoft.com/dotnet/sdk:10.0

ARG APT_PACKAGES
ARG PIP_PACKAGES
ARG NUGET_PRECACHE

RUN apt-get update \
    && apt-get install -y ${APT_PACKAGES} \
    && rm -rf /var/lib/apt/lists/*

RUN pip install --break-system-packages ${PIP_PACKAGES}

ENV PRECACHE_PACKAGES="${NUGET_PRECACHE}"

COPY download-nuget.sh /tmp/download-nuget.sh
RUN chmod +x /tmp/download-nuget.sh \
    && /tmp/download-nuget.sh \
    && rm /tmp/download-nuget.sh

COPY NuGet.Config /etc/nuget/NuGet.Config

WORKDIR /app
COPY skills.md /app/skills.md
