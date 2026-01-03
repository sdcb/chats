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

COPY skills.md /tmp/skills.template.md
COPY render-skills.py /tmp/render-skills.py

WORKDIR /app
RUN python3 /tmp/render-skills.py /tmp/skills.template.md /app/skills.md \
    && rm -f /tmp/skills.template.md /tmp/render-skills.py
