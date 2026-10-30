# 1. Берем SDK .NET 8 для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 2. Копируем только csproj
COPY ["Orders.csproj", "./"]

# 3. Восстанавливаем зависимости (кэшируется, если csproj не менялся)
RUN dotnet restore "Orders.csproj"

# 4. Копируем ВООБЩЕ ВСЁ из папки src в контейнер
COPY . ./

# 5. Просто собираем проект и кладем результат в папку /app/out
RUN dotnet publish -c Release -o /app/out --no-restore

# 6. Берем легкий образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 7. Устанавливаем curl для проверки здоровъя
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# 8. Забираем готовые файлы из этапа сборки
COPY --from=build /app/out ./

# 8. Указываем порт и точку входа (как в вашем примере)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# 10. Создаем папку для данных (тома)
RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "Orders.dll"]