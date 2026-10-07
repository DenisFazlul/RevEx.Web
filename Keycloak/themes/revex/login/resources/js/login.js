const passwordInput = document.querySelector("#password");
const passwordToggle = document.querySelector("[data-password-toggle]");

passwordToggle?.addEventListener("click", () => {
  const showPassword = passwordInput.type === "password";

  passwordInput.type = showPassword ? "text" : "password";
  passwordToggle.setAttribute(
    "aria-label",
    showPassword ? "Скрыть пароль" : "Показать пароль"
  );
  passwordToggle.firstElementChild.textContent = showPassword ? "Скрыть" : "Показать";
});
