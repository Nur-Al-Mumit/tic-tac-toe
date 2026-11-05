FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json TicTacToe.slnx ./
COPY src/TicTacToe.Domain/TicTacToe.Domain.csproj src/TicTacToe.Domain/
COPY src/TicTacToe.Application/TicTacToe.Application.csproj src/TicTacToe.Application/
COPY src/TicTacToe.Infrastructure/TicTacToe.Infrastructure.csproj src/TicTacToe.Infrastructure/
COPY src/TicTacToe.Api/TicTacToe.Api.csproj src/TicTacToe.Api/
RUN dotnet restore TicTacToe.slnx

COPY src/ src/
RUN dotnet publish src/TicTacToe.Api/TicTacToe.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
RUN adduser --disabled-password --gecos "" --uid 1000 appuser && chown -R appuser /app
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "TicTacToe.Api.dll"]
