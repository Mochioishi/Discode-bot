FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# 1. すべてのファイルをコピー
COPY . ./

# 2. 直接ファイルを指定してビルド（フォルダ名は書かない！）
RUN dotnet restore Discord-bot.csproj
RUN dotnet publish Discord-bot.csproj -c Release -o out

# 3. 実行環境へ
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "Discord-bot.dll"]
