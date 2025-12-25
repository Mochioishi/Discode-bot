FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. すべてのファイルを一旦コピー
COPY . .

# 2. プロジェクトファイル (*.csproj) がある場所を自動で探して、その階層へ移動
# これで「Discode-bot」フォルダの中にいても自動で見つけます
RUN export PROJECT_PATH=$(find . -name "*.csproj" -print -quit) && \
    export PROJECT_DIR=$(dirname "$PROJECT_PATH") && \
    cd "$PROJECT_DIR" && \
    dotnet restore && \
    dotnet publish -c Release -o /app/publish

# --- 実行用イメージ ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# 3. 実行するDLLも自動で探して起動
ENTRYPOINT ["sh", "-c", "dotnet $(ls *.dll | head -n 1)"]
