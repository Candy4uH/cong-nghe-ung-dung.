FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Object-Detection-ASP.NETMVC/Object-Detection-ASP.NETMVC.csproj Object-Detection-ASP.NETMVC/
RUN dotnet restore Object-Detection-ASP.NETMVC/Object-Detection-ASP.NETMVC.csproj

COPY Object-Detection-ASP.NETMVC/. Object-Detection-ASP.NETMVC/
WORKDIR /src/Object-Detection-ASP.NETMVC
RUN dotnet publish Object-Detection-ASP.NETMVC.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Object-Detection-ASP.NETMVC.dll"]