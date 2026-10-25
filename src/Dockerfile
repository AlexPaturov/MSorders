# 1. Берем SDK .NET 8 для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 2. Копируем ВООБЩЕ ВСЁ из папки src в контейнер
COPY . ./

# 3. Просто собираем проект и кладем результат в папку /app/out
RUN dotnet publish -c Release -o /app/out

# 4. Берем легкий образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 5. Забираем готовые файлы из этапа сборки
COPY --from=build /app/out ./

# 6. Указываем порт и точку входа (как в вашем примере)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Создаем папку для данных (тома)
RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "Orders.dll"]