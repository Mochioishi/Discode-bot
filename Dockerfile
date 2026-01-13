FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /src

# まずcsprojをコピーしてリストア（キャッシュを利用）
COPY ["Discord-bot.csproj", "./"]
RUN dotnet restore "./Discord-bot.csproj"

# 残りのソースコードをすべてコピー
COPY . .

# ビルド＆発行
# ※出力ファイル名が Discord-bot.dll になることを確実にする
RUN dotnet publish "Discord-bot.csproj" -c Release -o /app /p:AssemblyName=Discord-bot

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app .

# 環境変数の設定（必要に応じて）
ENV TZ=Asia/Tokyo

ENTRYPOINT ["dotnet", "Discord-bot.dll"]
