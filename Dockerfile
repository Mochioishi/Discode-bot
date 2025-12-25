FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# *.csproj と書くことで、スペースがあっても確実にコピーします
COPY *.csproj ./
RUN dotnet restore

# すべてのソースをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# 実行用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# ここも実際のファイル名に合わせる必要があります
# もしファイル名にスペースがあるなら、ワイルドカードで実行します
ENTRYPOINT ["sh", "-c", "dotnet $(ls *.dll | head -n 1)"]
