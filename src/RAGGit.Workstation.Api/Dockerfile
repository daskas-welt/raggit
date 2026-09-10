FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/RAGGit.Workstation.Api/RAGGit.Workstation.Api.csproj
RUN dotnet publish src/RAGGit.Workstation.Api -c Release -o /app/publish --no-restore

FROM base AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:5001
ENTRYPOINT ["dotnet", "RAGGit.Workstation.Api.dll"]
