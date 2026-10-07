<!doctype html>
<html lang="${locale.currentLanguageTag!'ru'}">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="color-scheme" content="light">
    <title>${msg("loginTitle", realm.displayName!'RevEx')}</title>
    <link rel="icon" type="image/svg+xml" href="${url.resourcesPath}/img/revex-mark.svg">
    <link rel="stylesheet" href="${url.resourcesPath}/css/login.css">
</head>
<body>
<main class="login-page">
    <section class="login-card" aria-labelledby="login-title">
        <header class="brand">
            <img class="brand__icon" src="${url.resourcesPath}/img/revex-mark.svg"
                 width="64" height="64" alt="">
            <span class="brand__name">RevEx</span>
        </header>

        <div class="login-heading">
            <h1 id="login-title">Вход в аккаунт</h1>
            <p>Введите данные, чтобы продолжить работу.</p>
        </div>

        <#if message?has_content && message.type != 'warning'>
            <div class="alert alert--${message.type}" role="alert">${message.summary}</div>
        </#if>

        <form class="login-form" action="${url.loginAction}" method="post">
            <#if !usernameHidden??>
                <label class="field" for="username">
                    <span>Логин или email</span>
                    <input id="username" name="username" type="text"
                           value="${(login.username!'')}" autocomplete="username" autofocus required>
                </label>
            </#if>

            <label class="field" for="password">
                <span>Пароль</span>
                <span class="password-field">
                    <input id="password" name="password" type="password"
                           autocomplete="current-password" required>
                    <button class="password-toggle" type="button" aria-label="Показать пароль"
                            aria-controls="password" data-password-toggle>
                        <span aria-hidden="true">Показать</span>
                    </button>
                </span>
            </label>

            <#if realm.rememberMe && !usernameHidden??>
                <label class="remember-me" for="rememberMe">
                    <input id="rememberMe" name="rememberMe" type="checkbox"
                           <#if login.rememberMe??>checked</#if>>
                    <span>${msg("rememberMe")}</span>
                </label>
            </#if>

            <button class="submit-button" id="kc-login" name="login" type="submit">Войти</button>
        </form>
    </section>

    <p class="login-footer">Безопасный вход через RevEx</p>
</main>
<script src="${url.resourcesPath}/js/login.js"></script>
</body>
</html>
