# ビルド環境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# すべてのファイルをビルドコンテナにコピー
COPY . ./

# プロジェクト名だけでビルド（パスをつけない）
RUN dotnet restore Discord-bot.csproj
RUN dotnet publish Discord-bot.csproj -c Release -o out

# 実行環境
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# 実行
ENTRYPOINT ["dotnet", "Discord-bot.dll"]
