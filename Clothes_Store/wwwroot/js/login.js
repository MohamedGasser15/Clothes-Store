const emailInput = document.getElementById("email");
const passwordInput = document.getElementById("password");
const loginButton = document.getElementById("loginButton");
const loginText = document.getElementById("loginText");
const loadingSpinner = document.getElementById("loadingSpinner");

window.onload = function () {
    emailInput.focus();
};

function checkInputs() {
    if (emailInput.value.trim() !== "" && passwordInput.value.trim() !== "") {
        loginButton.removeAttribute("disabled");
    } else {
        loginButton.setAttribute("disabled", "true");
    }
}

emailInput.addEventListener("input", checkInputs);
passwordInput.addEventListener("input", checkInputs);

document.getElementById("loginForm").addEventListener("submit", function (event) {
    event.preventDefault();
    loginText.classList.add("hidden");
    loadingSpinner.classList.remove("hidden");
    loginButton.setAttribute("disabled", "true");

    setTimeout(() => {
        this.submit();
    }, 2000);
});