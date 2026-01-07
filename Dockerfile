# ビルド環境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# プロジェクトファイルをコピーしてリストア
COPY *.csproj ./
RUN dotnet restore

# 全ファイルをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# 実行環境
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# 実行ファイルの名称に合わせて書き換えてください
ENTRYPOINT ["dotnet", "Discord-bot.dll"]
