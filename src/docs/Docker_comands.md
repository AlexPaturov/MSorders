# Команды остановки и запуска

Сервисы проекта:

- `postgres` — база данных, контейнер `orders-postgres-container`.
- `orders-container` — приложение, контейнер `orders-container`.

Все команды выполняются из каталога `src/`.

## 1. Остановить только базу

```bash
docker compose -f src/compose.yaml stop postgres
```

Останавливает контейнер базы. Если приложение запущено, сначала останови приложение, затем базу.

## 2. Запустить только базу

```bash
docker compose -f src/compose.yaml start postgres
```

Запускает ранее остановленный контейнер базы. Приложение остаётся в текущем состоянии.

## 3. Остановить только приложение

```bash
docker compose -f src/compose.yaml stop orders-container
```

Останавливает контейнер приложения. База продолжает работать.

## 4. Запустить только приложение

```bash
docker compose -f src/compose.yaml start orders-container
```

Запускает ранее остановленный контейнер приложения. База должна быть запущена, иначе healthcheck приложения будет падать.

## 5. Остановить весь проект

```bash
docker compose -f src/compose.yaml stop
```

Останавливает приложение и базу. Контейнеры, логи и том `orders-postgres-data` сохраняются.

## 6. Запустить весь проект

```bash
docker compose -f src/compose.yaml start
```

Запускает ранее остановленные контейнеры приложения и базы.

## Дополнительно

- `start` запускает существующие остановленные контейнеры. Если контейнер удалён, используй `docker compose up -d <service>` или `docker compose up -d` для всего проекта.
- `docker compose down` останавливает и удаляет контейнеры и сеть; том `orders-postgres-data` сохраняется.
- `docker compose down -v` удаляет том `orders-postgres-data` вместе с данными.
- Проверка состояния: `docker compose ps` или `docker compose ps -a`.
- Логи: `docker compose logs -f <service>`.
