FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

# リポジトリ全体をコピー
COPY . .

# ★ ルート直下に csproj があるので移動しない
RUN dotnet restore "Discord-bot.csproj"

# ビルド＆発行
RUN dotnet publish "Discord-bot.csproj" -c Release -o /app

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app .

ENTRYPOINT ["dotnet", "Discord-bot.dll"]
