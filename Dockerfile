FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. フォルダ構造を維持してコピー
# Discode-mainフォルダ内のcsprojをコピーします
COPY ["Discode-main/Discode-main.csproj", "Discode-main/"]

# 2. 依存関係の復元 (csprojの場所を明示)
RUN dotnet restore "Discode-main/Discode-main.csproj"

# 3. すべてのソースコードをコピー
COPY . .

# 4. ビルドと発行
WORKDIR "/src/Discode-main"
RUN dotnet publish "Discode-main.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- 実行用イメージ ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# 実行するDLL名をプロジェクト名に合わせる
ENTRYPOINT ["dotnet", "Discode-main.dll"]
