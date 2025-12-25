FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. ファイルを検索してコピー (階層が深くても対応できるようにします)
# リポジトリ内のすべてのファイルを一旦ビルド環境にコピーします
COPY . .

# 2. プロジェクトファイルがあるディレクトリに移動
# リポジトリ構造に合わせて Discode-main/Discode-main に移動します
WORKDIR "/src/Discode-main/Discode-main"

# 3. 復元とビルド
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# --- 実行用イメージ ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# 実行
ENTRYPOINT ["dotnet", "Discode-main.dll"]
