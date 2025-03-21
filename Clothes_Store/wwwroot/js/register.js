const nameInput = document.getElementById("name");
const emailInput = document.getElementById("email");
const passwordInput = document.getElementById("password");
const confirmPasswordInput = document.getElementById("confirmPassword");
const registerButton = document.getElementById("registerButton");
const registerText = document.getElementById("registerText");
const loadingSpinner = document.getElementById("loadingSpinner");
const passwordError = document.getElementById("passwordError");

window.onload = function () {
    nameInput.focus();
};

function checkInputs() {
    if (nameInput.value.trim() !== "" && emailInput.value.trim() !== "" && passwordInput.value.trim() !== "" && confirmPasswordInput.value.trim() !== "") {
        registerButton.removeAttribute("disabled");
    } else {
        registerButton.setAttribute("disabled", "true");
    }
}

nameInput.addEventListener("input", checkInputs);
emailInput.addEventListener("input", checkInputs);
passwordInput.addEventListener("input", checkInputs);
confirmPasswordInput.addEventListener("input", checkInputs);

confirmPasswordInput.addEventListener("input", function () {
    if (passwordInput.value !== confirmPasswordInput.value) {
        passwordError.classList.remove("hidden");
        registerButton.setAttribute("disabled", "true");
    } else {
        passwordError.classList.add("hidden");
        checkInputs();
    }
});

document.getElementById("registerForm").addEventListener("submit", function (event) {
    registerText.classList.add("hidden");
    loadingSpinner.classList.remove("hidden");
    registerButton.setAttribute("disabled", "true");
});