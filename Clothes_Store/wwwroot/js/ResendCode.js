let resendButton = document.getElementById("resendButton");
let countdown = 60;

function updateButton() {
    if (countdown > 0) {
        resendButton.innerText = `Resend Code (${countdown}s)`;
        countdown--;
        setTimeout(updateButton, 1000);
    } else {
        resendButton.innerText = "Resend Code";
        resendButton.disabled = false;
    }
}

updateButton();