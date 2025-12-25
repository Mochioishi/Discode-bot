FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 1. すべてのファイルを一度作業ディレクトリにコピー
COPY . ./

# 2. プロジェクトファイルがある場所に移動してリストア
# もしフォルダ名が違う場合は、ここを実際のフォルダ名に変更してください
RUN dotnet restore Discode-main/*.csproj

# 3. ビルドと発行
RUN dotnet publish Discode-main/*.csproj -c Release -o out

# --- 実行用イメージ ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# プロジェクト名が「Discode.csproj」なら「Discode.dll」になります
ENTRYPOINT ["dotnet", "Discode.dll"]
