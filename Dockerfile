# ビルド環境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# すべてのファイルをコピー
COPY . ./

# プロジェクトファイルを自動検索してビルド
RUN dotnet restore
RUN dotnet publish -c Release -o out

# 実行環境
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "Discord-bot.dll"]
