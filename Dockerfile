# ビルド環境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# リポジトリ全体をコピー
COPY . ./

# 三重階層の奥にあるプロジェクトを指定してリストア
# パス：Discord-bot/Discord-bot/Discord-bot.csproj
RUN dotnet restore "Discord-bot/Discord-bot/Discord-bot.csproj"

# ビルド実行
RUN dotnet publish "Discord-bot/Discord-bot/Discord-bot.csproj" -c Release -o out

# 実行環境
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# 実行（出力されるdll名はプロジェクト名と同じ）
ENTRYPOINT ["dotnet", "Discord-bot.dll"]
