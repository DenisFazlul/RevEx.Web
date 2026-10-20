# RevEx Frontend

React/Vite-интерфейс с двумя страницами:

- `/` — Home, приветствие и назначение приложения.
- `/admin` — Admin, адрес API для подключения десктопа и ссылка на консоль администратора Keycloak.

```bash
npm install
npm run dev
npm run build
```

Home доступна всем. Admin требует входа через Keycloak realm `revex` (клиент `revex-frontend`) и роли `administrator`; обычным пользователям показывается отказ в доступе. Это ограничение интерфейса; серверные операции должны дополнительно проверять роли в API. Учётная запись bootstrap-администратора realm `master` используется отдельно для консоли Keycloak. По умолчанию Admin показывает `http://localhost:8080/` для десктопа и `http://localhost:8081/admin/master/console/` для Keycloak.

Для других адресов создайте `.env` по примеру `.env.example`: `VITE_DESKTOP_API_URL` задаёт отображаемый адрес подключения, `VITE_KEYCLOAK_URL` — ссылку на Keycloak. Если `VITE_DESKTOP_API_URL` не задан, используется `VITE_API_URL`, затем локальный адрес по умолчанию. Переменные Vite применяются при сборке; после изменения пересоберите фронтенд. Для подключения с другого компьютера укажите доступный ему адрес сервера вместо localhost.

Общая документация: [RevEx.Web/docs](../docs/README.md). Для текущего Compose API опубликован на 8082; исходные frontend defaults/proxy на 8080 требуют согласования, см. [разработку](../docs/DEVELOPMENT.md).
