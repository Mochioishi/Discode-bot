FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

COPY . .

# ★ ここが違う（1階層深くする）
WORKDIR /src/Discord-bot/Discord-bot

RUN dotnet restore "Discord-bot.csproj"
RUN dotnet publish "Discord-bot.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app .

ENTRYPOINT ["dotnet", "Discord-bot.dll"]
