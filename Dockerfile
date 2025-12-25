FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. すべてのファイルをコピー
COPY . .

# 2. .csproj ファイルを自動で見つけて、そのディレクトリに移動してビルド
# 階層が Discode-main/ なのか Discode-main/Discode-main/ なのかを自動判別させます
RUN dotnet restore $(find . -name "*.csproj")
RUN dotnet publish $(find . -name "*.csproj") -c Release -o /app/publish /p:UseAppHost=false

# --- 実行用イメージ ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# 実行するDLL名を確認してください。
# プロジェクト名が Discode-main.csproj なら Discode-main.dll になります。
ENTRYPOINT ["dotnet", "Discode-main.dll"]
