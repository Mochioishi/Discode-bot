FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# プロジェクトファイルをコピーして復元
COPY *.csproj ./
RUN dotnet restore

# 全ファイルをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# プロジェクト名に合わせて変更してください（例: DiscordBot.dll）
ENTRYPOINT ["dotnet", "DiscordTimeSignal.dll"]
