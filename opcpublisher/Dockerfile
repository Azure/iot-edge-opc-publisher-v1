#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["opcpublisher/Microsoft.Azure.IIoT.Modules.OpcUa.Publisher.csproj", "opcpublisher/"]
RUN dotnet restore "opcpublisher/Microsoft.Azure.IIoT.Modules.OpcUa.Publisher.csproj"
COPY . .
WORKDIR "/src/opcpublisher"
RUN dotnet build "Microsoft.Azure.IIoT.Modules.OpcUa.Publisher.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Microsoft.Azure.IIoT.Modules.OpcUa.Publisher.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "opcpublisher.dll"]