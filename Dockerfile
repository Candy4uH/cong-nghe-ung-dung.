FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Object-Detection.csproj", "./"]
COPY ["Object-Detection.Api/Object-Detection.Api.csproj", "Object-Detection.Api/"]
RUN dotnet restore "Object-Detection.Api/Object-Detection.Api.csproj"

COPY . .
RUN dotnet publish "Object-Detection.Api/Object-Detection.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libglib2.0-0 \
        libsm6 \
        libxext6 \
        libxrender1 \
        libfontconfig1 \
        libgl1 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:10000
ENV ObjectDetection__ModelFilePath=model/object-detection-model.json

COPY --from=build /app/publish .
COPY ["model", "./model"]

EXPOSE 10000

ENTRYPOINT ["dotnet", "Object-Detection.Api.dll"]
