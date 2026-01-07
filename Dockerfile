# ビルド環境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# ソースコード全体をコピー
COPY . ./

# サブフォルダ内のプロジェクトを指定してリストア
RUN dotnet restore Discord-bot/Discord-bot.csproj

# ビルド（出力先を out に指定）
RUN dotnet publish Discord-bot/Discord-bot.csproj -c Release -o out

# 実行環境
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# 実行
ENTRYPOINT ["dotnet", "Discord-bot.dll"]
