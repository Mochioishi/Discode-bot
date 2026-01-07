# ビルド環境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# 1. 最初にcsprojだけコピーしてリストア（ビルド時間の短縮）
COPY *.csproj ./
RUN dotnet restore

# 2. 残りのソースコードをすべてコピー
COPY . ./

# 3. ビルド実行
RUN dotnet publish -c Release -o out

# 実行環境
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# 4. 実行 (プロジェクト名が Discord-bot なので dll名もこれになります)
ENTRYPOINT ["dotnet", "Discord-bot.dll"]
